namespace DemaConsulting.Plexus.Avalonia.PropertyGrid;

/// <summary>
///     Edits a collection property through a working copy: elements can be added, inserted, removed, replaced and
///     reordered. Obtain one from <see cref="PropertyEditSession.BeginCollectionEdit"/>.
/// </summary>
/// <remarks>
///     <para>
///         The working copy is a shallow copy of the collection: existing element instances are shared with the target,
///         so editing a property of an element (a path such as <c>Items[0].Name</c>) is a separate edit of the session.
///         New elements are created with the element type's public default constructor (strings start empty, value
///         types default) or with <see cref="PropertyEditSession.CollectionItemFactory"/>.
///     </para>
///     <para>
///         In a staged session every change updates one pending <see cref="PendingEditKind.Collection"/> edit and the
///         target is untouched until commit; if the working copy again equals the target the pending edit disappears.
///         In a live session each change is written to the target immediately and the working copy is re-read from
///         the target afterwards. When the session commits or cancels, the editor becomes inactive and
///         every operation fails; begin a new edit.
///     </para>
///     <para>
///         How the target is updated is described by <see cref="PropertyEditSession.Commit"/>: lists and
///         <c>ICollection&lt;T&gt;</c> are cleared and refilled in place, arrays of unchanged length are updated in
///         place, and everything else is replaced through the property setter (see <see cref="CanResize"/> and
///         <see cref="CanReorder"/> for what is possible with a read-only property).
///     </para>
/// </remarks>
/// <example>
///     <code><![CDATA[
///     using DemaConsulting.Plexus.Avalonia.PropertyGrid;
///
///     var settings = new Settings();
///     using var source = new ReflectionPropertySource(settings);
///     var session = new PropertyEditSession(source, PropertyEditMode.Staged);
///     var tagsItem = session.FindItem("Tags")!;                 // a collection property
///
///     CollectionEditor editor = session.BeginCollectionEdit(tagsItem);
///     editor.Add("draft");                 // a new element
///     editor.Move(0, editor.Count - 1);    // reorder
///     editor.RemoveAt(1);
///     session.Commit();                    // staged: the target list changes now
///
///     public class Settings
///     {
///         public List<string> Tags { get; set; } = ["a", "b", "c"];
///     }
///     ]]></code>
/// </example>
public sealed class CollectionEditor
{
    /// <summary>
    ///     The session the editor belongs to.
    /// </summary>
    private readonly PropertyEditSession _session;

    /// <summary>
    ///     The working copy.
    /// </summary>
    private readonly List<object?> _working;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CollectionEditor"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="item">The collection property.</param>
    /// <param name="itemType">The element type.</param>
    internal CollectionEditor(PropertyEditSession session, PropertyItem item, Type itemType)
    {
        _session = session;
        Item = item;
        ItemType = itemType;
        _working = [.. item.GetCollectionValues()];
        CollectionApplier.GetCapabilities(item, out var canResize, out var canReorder);
        CanResize = canResize;
        CanReorder = canReorder;
        IsActive = true;
    }

    /// <summary>
    ///     Gets the collection property being edited.
    /// </summary>
    public PropertyItem Item { get; }

    /// <summary>
    ///     Gets the element type.
    /// </summary>
    public Type ItemType { get; }

    /// <summary>
    ///     Gets the elements of the working copy in order.
    /// </summary>
    public IReadOnlyList<object?> Items => _working;

    /// <summary>
    ///     Gets the number of elements in the working copy.
    /// </summary>
    public int Count => _working.Count;

    /// <summary>
    ///     Gets a value indicating whether elements can be added and removed. <see langword="false"/> for a read-only
    ///     collection and for an array (or other fixed-size collection) behind a read-only property.
    /// </summary>
    public bool CanResize { get; }

    /// <summary>
    ///     Gets a value indicating whether elements can be replaced and reordered (an array can always be reordered
    ///     in place; a read-only collection cannot).
    /// </summary>
    public bool CanReorder { get; }

    /// <summary>
    ///     Gets a value indicating whether the editor can still be used; it becomes <see langword="false"/> after the
    ///     session commits or cancels.
    /// </summary>
    public bool IsActive { get; internal set; }

    /// <summary>
    ///     Appends a new element created with the default constructor or the session's factory.
    /// </summary>
    /// <returns>The outcome.</returns>
    public PropertyResult Add() => Insert(_working.Count);

    /// <summary>
    ///     Appends an element.
    /// </summary>
    /// <param name="value">The element.</param>
    /// <returns>The outcome.</returns>
    public PropertyResult Add(object? value) => Insert(_working.Count, value);

    /// <summary>
    ///     Inserts a new element, created with the default constructor or the session's factory, at an index.
    /// </summary>
    /// <param name="index">The index, from 0 to <see cref="Count"/>.</param>
    /// <returns>The outcome.</returns>
    public PropertyResult Insert(int index)
    {
        var ready = Prepare(resize: true);
        if (!ready.Succeeded)
        {
            return ready;
        }

        var created = _session.CreateElement(this, out var element);
        return created.Succeeded ? Insert(index, element) : created;
    }

    /// <summary>
    ///     Inserts an element at an index.
    /// </summary>
    /// <param name="index">The index, from 0 to <see cref="Count"/>.</param>
    /// <param name="value">The element, which must be an instance of <see cref="ItemType"/>.</param>
    /// <returns>The outcome.</returns>
    public PropertyResult Insert(int index, object? value)
    {
        var ready = Prepare(resize: true);
        if (!ready.Succeeded)
        {
            return ready;
        }

        if (index < 0 || index > _working.Count)
        {
            return PropertyResult.Failure($"Index {index} is outside the collection.");
        }

        var valid = ValidateElement(value);
        return valid.Succeeded ? Mutate(list => list.Insert(index, value)) : valid;
    }

    /// <summary>
    ///     Removes the element at an index.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <returns>The outcome.</returns>
    public PropertyResult RemoveAt(int index)
    {
        var ready = Prepare(resize: true);
        if (!ready.Succeeded)
        {
            return ready;
        }

        return index < 0 || index >= _working.Count
            ? PropertyResult.Failure($"Index {index} is outside the collection.")
            : Mutate(list => list.RemoveAt(index));
    }

    /// <summary>
    ///     Replaces the whole working copy in one step (one write in live mode, one pending edit in staged mode). This is
    ///     what a dialog uses to apply the list a user arranged: adding, removing, reordering and replacing elements all
    ///     happen on the dialog's own list and reach the session only through this call.
    /// </summary>
    /// <param name="items">The new elements, each an instance of <see cref="ItemType"/>.</param>
    /// <returns>The outcome; a collection that cannot be resized fails when the number of elements differs.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is <see langword="null"/>.</exception>
    public PropertyResult SetItems(IEnumerable<object?> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var replacement = items.ToList();
        var ready = Prepare(resize: replacement.Count != _working.Count);
        if (!ready.Succeeded)
        {
            return ready;
        }

        foreach (var element in replacement)
        {
            var valid = ValidateElement(element);
            if (!valid.Succeeded)
            {
                return valid;
            }
        }

        return Mutate(list =>
        {
            list.Clear();
            list.AddRange(replacement);
        });
    }

    /// <summary>
    ///     Replaces the element at an index.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <param name="value">The new element, which must be an instance of <see cref="ItemType"/>.</param>
    /// <returns>The outcome.</returns>
    public PropertyResult SetItem(int index, object? value)
    {
        var ready = Prepare(resize: false);
        if (!ready.Succeeded)
        {
            return ready;
        }

        if (index < 0 || index >= _working.Count)
        {
            return PropertyResult.Failure($"Index {index} is outside the collection.");
        }

        var valid = ValidateElement(value);
        return valid.Succeeded ? Mutate(list => list[index] = value) : valid;
    }

    /// <summary>
    ///     Moves an element to another position.
    /// </summary>
    /// <param name="fromIndex">The current index.</param>
    /// <param name="toIndex">The index the element has afterwards.</param>
    /// <returns>The outcome.</returns>
    public PropertyResult Move(int fromIndex, int toIndex)
    {
        var ready = Prepare(resize: false);
        if (!ready.Succeeded)
        {
            return ready;
        }

        if (fromIndex < 0 || fromIndex >= _working.Count || toIndex < 0 || toIndex >= _working.Count)
        {
            return PropertyResult.Failure("An index is outside the collection.");
        }

        return fromIndex == toIndex
            ? PropertyResult.Success
            : Mutate(list =>
            {
                var element = list[fromIndex];
                list.RemoveAt(fromIndex);
                list.Insert(toIndex, element);
            });
    }

    /// <summary>
    ///     Re-reads the working copy from the target.
    /// </summary>
    internal void Reload()
    {
        _working.Clear();
        _working.AddRange(Item.GetCollectionValues());
    }

    /// <summary>
    ///     Reports whether the working copy holds the same elements, in the same order, as the target.
    /// </summary>
    /// <returns><see langword="true"/> when nothing differs.</returns>
    internal bool MatchesTarget()
    {
        var target = Item.GetCollectionValues();
        return target.Count == _working.Count && target.Zip(_working).All(pair => Equals(pair.First, pair.Second));
    }

    /// <summary>
    ///     Checks that the editor can be used for a kind of change.
    /// </summary>
    /// <param name="resize">Whether the change adds or removes elements.</param>
    /// <returns>The outcome.</returns>
    private PropertyResult Prepare(bool resize)
    {
        if (!IsActive)
        {
            return PropertyResult.Failure("This collection edit has ended; begin a new one.");
        }

        if (resize ? !CanResize : !CanReorder)
        {
            return PropertyResult.Failure($"'{Item.DisplayName}' is read-only; its elements cannot be {(resize ? "added or removed" : "changed")}.");
        }

        return PropertyResult.Success;
    }

    /// <summary>
    ///     Checks that a value can be an element.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The outcome.</returns>
    private PropertyResult ValidateElement(object? value)
    {
        if (value is null)
        {
            return ItemType.IsValueType && Nullable.GetUnderlyingType(ItemType) is null
                ? PropertyResult.Failure($"A {ItemType.Name} element cannot be empty.")
                : PropertyResult.Success;
        }

        return ItemType.IsInstanceOfType(value)
            ? PropertyResult.Success
            : PropertyResult.Failure($"A value of type {value.GetType().Name} is not valid for a {ItemType.Name} element.");
    }

    /// <summary>
    ///     Changes the working copy and tells the session.
    /// </summary>
    /// <param name="change">The change.</param>
    /// <returns>The outcome of recording or applying the change.</returns>
    private PropertyResult Mutate(Action<List<object?>> change)
    {
        var before = _working.ToList();
        change(_working);
        var result = _session.CollectionEdited(this);
        if (!result.Succeeded && _session.Mode == PropertyEditMode.Staged)
        {
            _working.Clear();
            _working.AddRange(before);
        }

        return result;
    }
}

using System.Globalization;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid;

/// <summary>
///     Applies the edits a user makes in a grid to the edited object, either immediately (<see cref="PropertyEditMode.Live"/>)
///     or buffered until <see cref="Commit"/> (<see cref="PropertyEditMode.Staged"/>), with per-property validation
///     errors and an optional host hook to wrap writes in an undo transaction.
/// </summary>
/// <remarks>
///     <para>
///         <b>Semantics.</b> Edits are keyed by <see cref="PropertyItem.Path"/>, so nested properties
///         (<c>Address.City</c>) and collection elements (<c>Tags[2]</c>, <c>Items[0].Name</c>) are edited like top-level
///         properties. In staged mode the target is not touched until <see cref="Commit"/>; <see cref="GetValue"/> and
///         <see cref="GetText"/> return the pending value so a grid can show it, and an edit that sets a property back to
///         its current value removes the pending edit. Editing the same path again replaces the pending value but keeps
///         its position. <see cref="Commit"/> applies the pending edits in the order they were first made. Paths of
///         collection elements refer to the indices of the target collection when the edit is applied; stage structural
///         changes with a <see cref="CollectionEditor"/> and element edits in the order you want them applied.
///     </para>
///     <para>
///         <b>Errors.</b> A value that fails validation, text that the type converter rejects, a read-only property and
///         a setter that throws are all reported as a failed <see cref="PropertyResult"/> and remembered per path
///         (<see cref="GetError"/>, <see cref="GetErrors"/>) until the property is edited successfully, discarded or
///         cancelled. Nothing is thrown at the caller for user-code failures.
///     </para>
///     <para>
///         <b>Commit scope.</b> The optional <c>commitScope</c> receives the action that performs the writes and must
///         run it exactly once, for example inside an undo transaction:
///         <c>apply =&gt; { using var t = history.BeginTransaction("Edit properties"); apply(); }</c>. Staged commit calls it
///         once for all pending edits; live mode calls it once per edit. A failed edit inside a commit does not stop the
///         others; it stays pending with <see cref="PendingEdit.Error"/>, and <see cref="CommitResult.Succeeded"/> is
///         <see langword="false"/>. If the scope itself throws or never runs the action, no edit counts as
///         committed. A source that implements <see cref="IPropertyCommitScope"/> brackets the commit as well, outside
///         the host's scope; throwing a <see cref="PropertyCommitRefusedException"/> from a scope marks that property's row.
///     </para>
///     <para>
///         <b>Threading.</b> A session is not thread-safe; use it from the UI thread.
///     </para>
/// </remarks>
/// <example>
///     <code><![CDATA[
///     using var source = new ReflectionPropertySource(person);
///     var session = new PropertyEditSession(
///         source,
///         PropertyEditMode.Staged,
///         apply => { using var t = history.BeginTransaction("Edit person"); apply(); });
///
///     PropertyItem age = session.FindItem("Age")!;
///     session.SetText(age, "37");            // pending; person.Age is unchanged
///     session.SetText(age, "oops");          // fails; session.GetError(age) has the message
///     if (session.HasChanges)
///     {
///         CommitResult result = session.Commit();   // writes inside one undo step
///     }
///     ]]></code>
/// </example>
public sealed class PropertyEditSession
{
    /// <summary>
    ///     The pending edits in application order.
    /// </summary>
    private readonly List<PendingEdit> _pending = [];

    /// <summary>
    ///     The errors by item path.
    /// </summary>
    private readonly Dictionary<string, string> _errors = new(StringComparer.Ordinal);

    /// <summary>
    ///     The host hook that wraps writes.
    /// </summary>
    private readonly Action<Action>? _commitScope;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PropertyEditSession"/> class.
    /// </summary>
    /// <param name="source">The source whose items are edited.</param>
    /// <param name="mode">When edits are written to the target.</param>
    /// <param name="commitScope">
    ///     An optional hook that runs the writes (for example inside an undo transaction); it must invoke the action it
    ///     receives exactly once. <see langword="null"/> runs the writes directly.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public PropertyEditSession(IPropertySource source, PropertyEditMode mode = PropertyEditMode.Live, Action<Action>? commitScope = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        Source = source;
        Mode = mode;
        _commitScope = commitScope;
    }

    /// <summary>
    ///     Occurs when pending edits, errors or the target change.
    /// </summary>
    public event EventHandler<PropertySessionChangedEventArgs>? Changed;

    /// <summary>
    ///     Gets the source whose items are edited.
    /// </summary>
    public IPropertySource Source { get; }

    /// <summary>
    ///     Gets when edits are written to the target.
    /// </summary>
    public PropertyEditMode Mode { get; }

    /// <summary>
    ///     Gets or sets the hook that creates new collection elements, called with the collection item; when it is
    ///     <see langword="null"/> (or returns <see langword="null"/> for a reference type) elements are created with
    ///     the element type's public default constructor.
    /// </summary>
    public Func<PropertyItem, object?>? CollectionItemFactory { get; set; }

    /// <summary>
    ///     Gets a value indicating whether there are pending edits.
    /// </summary>
    public bool HasChanges => _pending.Count > 0;

    /// <summary>
    ///     Gets a snapshot of the pending edits in the order they will be applied.
    /// </summary>
    /// <returns>The pending edits.</returns>
    public IReadOnlyList<PendingEdit> GetPendingEdits() => _pending.ToList();

    /// <summary>
    ///     Gets a value indicating whether any property has an error.
    /// </summary>
    public bool HasErrors => _errors.Count > 0;

    /// <summary>
    ///     Gets a snapshot of the current error message of each property that has one, keyed by item path.
    /// </summary>
    /// <returns>The errors.</returns>
    public IReadOnlyDictionary<string, string> GetErrors() => new Dictionary<string, string>(_errors, StringComparer.Ordinal);

    /// <summary>
    ///     Finds an item of the source by path (<c>Name</c>, <c>Address.City</c>, <c>Tags[2]</c>,
    ///     <c>Items[0].Name</c>).
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The item, or <see langword="null"/> when the path does not lead to an item.</returns>
    public PropertyItem? FindItem(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        PropertyItem? current = null;
        var position = 0;
        while (position < path.Length)
        {
            if (path[position] == '[')
            {
                var close = path.IndexOf(']', position);
                if (current is null || close < 0 || !int.TryParse(path.AsSpan(position + 1, close - position - 1), NumberStyles.None, CultureInfo.InvariantCulture, out var index))
                {
                    return null;
                }

                var elements = current.GetCollectionItems();
                current = index < elements.Count ? elements[index] : null;
                position = close + 1;
            }
            else
            {
                if (path[position] == '.')
                {
                    if (current is null)
                    {
                        return null;
                    }

                    position++;
                }

                var end = path.IndexOfAny(['.', '['], position);
                end = end < 0 ? path.Length : end;
                var name = path[position..end];
                current = (current is null ? Source.GetItems() : current.Children).FirstOrDefault(i => i.Name == name);
                position = end;
            }

            if (current is null)
            {
                return null;
            }
        }

        return current;
    }

    /// <summary>
    ///     Sets a property value: written immediately in live mode, buffered in staged mode.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="value">The new value, which must be assignable to the item's type.</param>
    /// <returns>The outcome; a failure is also recorded as the item's error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is <see langword="null"/>.</exception>
    public PropertyResult SetValue(PropertyItem item, object? value)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.IsReadOnly)
        {
            return Fail(item, $"'{item.DisplayName}' is read-only.");
        }

        var valid = item.Validate(value);
        if (!valid.Succeeded)
        {
            return Fail(item, valid.Error!);
        }

        if (Mode == PropertyEditMode.Live)
        {
            return ApplyLive(item, () => item.SetValue(value));
        }

        var existing = Find(item.Path);
        var same = item.TryGetValue(out var current).Succeeded && Equals(current, value);
        if (existing is null)
        {
            if (!same)
            {
                _pending.Add(new PendingEdit(item, PendingEditKind.Value, value, null));
            }
        }
        else if (same)
        {
            Remove(existing);
        }
        else
        {
            ReplaceEdit(existing, PendingEditKind.Value, value, null);
        }

        ClearError(item.Path);
        Raise(PropertySessionChangeKind.PendingChanged, item.Path, false);
        return PropertyResult.Success;
    }

    /// <summary>
    ///     Converts text typed by a user (see <see cref="PropertyItem.TryParse"/>) and sets the result.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="text">The text.</param>
    /// <param name="culture">The culture to parse with; <see langword="null"/> means the current culture.</param>
    /// <returns>The outcome; a failure is also recorded as the item's error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> or <paramref name="text"/> is <see langword="null"/>.</exception>
    public PropertyResult SetText(PropertyItem item, string text, CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(text);
        if (item.IsReadOnly)
        {
            return Fail(item, $"'{item.DisplayName}' is read-only.");
        }

        var parsed = item.TryParse(text, culture, out var value);
        return parsed.Succeeded ? SetValue(item, value) : Fail(item, parsed.Error!);
    }

    /// <summary>
    ///     Restores a property's default value: immediately in live mode, buffered in staged mode.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>The outcome; a failure is also recorded as the item's error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is <see langword="null"/>.</exception>
    public PropertyResult Reset(PropertyItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!item.CanReset)
        {
            return Fail(item, $"'{item.DisplayName}' cannot be reset.");
        }

        if (Mode == PropertyEditMode.Live)
        {
            return ApplyLive(item, item.Reset);
        }

        var existing = Find(item.Path);
        if (existing is null)
        {
            _pending.Add(new PendingEdit(item, PendingEditKind.Reset, null, null));
        }
        else
        {
            ReplaceEdit(existing, PendingEditKind.Reset, null, null);
        }

        ClearError(item.Path);
        Raise(PropertySessionChangeKind.PendingChanged, item.Path, false);
        return PropertyResult.Success;
    }

    /// <summary>
    ///     Gets the value to show for an item: the pending value when one is buffered, else the target's value.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>
    ///     The value; for a pending collection edit the working copy's elements as a read-only list; for a pending reset
    ///     the default value when it is known; <see langword="null"/> when the target's value cannot be read.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is <see langword="null"/>.</exception>
    public object? GetValue(PropertyItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var edit = Find(item.Path);
        if (edit is not null)
        {
            switch (edit.Kind)
            {
                case PendingEditKind.Value:
                    return edit.Value;
                case PendingEditKind.Collection:
                    return edit.Collection!.Items;
                default:
                    if (item.IsDefaultValueKnown)
                    {
                        return item.DefaultValue;
                    }

                    break;
            }
        }

        return item.TryGetValue(out var value).Succeeded ? value : null;
    }

    /// <summary>
    ///     Gets the text to show for an item (see <see cref="GetValue"/>), never throwing.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="culture">The culture to format with; <see langword="null"/> means the current culture.</param>
    /// <returns>The text; <c>(Collection)</c> for a pending collection edit.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is <see langword="null"/>.</exception>
    public string GetText(PropertyItem item, CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (Find(item.Path)?.Kind == PendingEditKind.Collection)
        {
            return PropertyItem.CollectionText;
        }

        return item.TryConvertToString(GetValue(item), out var text, culture).Succeeded ? text : string.Empty;
    }

    /// <summary>
    ///     Reports whether an item has a pending edit.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns><see langword="true"/> when an edit is buffered for the item's path.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is <see langword="null"/>.</exception>
    public bool IsPending(PropertyItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Find(item.Path) is not null;
    }

    /// <summary>
    ///     Records an error for an item without changing its value, for an editor that rejects its own input (for
    ///     example text that is not a valid color). The error stays until the item is edited successfully, discarded or
    ///     cancelled.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="message">The reason shown to the user.</param>
    /// <returns>A failed result carrying the message.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> or <paramref name="message"/> is <see langword="null"/>.</exception>
    public PropertyResult ReportError(PropertyItem item, string message)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(message);
        return Fail(item, message);
    }

    /// <summary>
    ///     Gets the current error message of an item.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>The message, or <see langword="null"/> when the item has no error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is <see langword="null"/>.</exception>
    public string? GetError(PropertyItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return _errors.GetValueOrDefault(item.Path);
    }

    /// <summary>
    ///     Starts (or continues) editing a collection property through a working copy.
    /// </summary>
    /// <param name="item">The collection property (<see cref="PropertyItem.IsCollection"/>).</param>
    /// <returns>
    ///     The editor; when a collection edit is already pending for the item, the same editor is returned.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The item is not a collection.</exception>
    public CollectionEditor BeginCollectionEdit(PropertyItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.CollectionItemType is not { } elementType)
        {
            throw new ArgumentException($"'{item.Name}' is not a collection property.", nameof(item));
        }

        if (Find(item.Path)?.Collection is { IsActive: true } existing)
        {
            return existing;
        }

        return new CollectionEditor(this, item, elementType);
    }

    /// <summary>
    ///     Discards the pending edit and the error of one item.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns><see langword="true"/> when a pending edit was discarded.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is <see langword="null"/>.</exception>
    public bool Discard(PropertyItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var edit = Find(item.Path);
        var hadError = _errors.Remove(item.Path);
        if (edit is not null)
        {
            Remove(edit);
            Deactivate(edit);
        }

        if (edit is not null || hadError)
        {
            Raise(PropertySessionChangeKind.PendingChanged, item.Path, false);
        }

        return edit is not null;
    }

    /// <summary>
    ///     Discards every pending edit and error; the target is left untouched. Active collection editors become inactive.
    /// </summary>
    public void Cancel()
    {
        foreach (var edit in _pending)
        {
            Deactivate(edit);
        }

        _pending.Clear();
        _errors.Clear();
        Raise(PropertySessionChangeKind.Cancelled, null, true);
    }

    /// <summary>
    ///     Writes the pending edits to the target in order, inside the commit scope.
    /// </summary>
    /// <returns>
    ///     The outcome. Applied edits are no longer pending; failed edits stay pending with their error.
    /// </returns>
    public CommitResult Commit()
    {
        if (_pending.Count == 0)
        {
            return new CommitResult(0, [], null);
        }

        var batch = _pending.ToList();
        var applied = new List<PendingEdit>();
        var failed = new List<PendingEdit>();
        var scopeError = RunScoped(() =>
        {
            foreach (var edit in batch)
            {
                var result = Apply(edit);
                edit.Error = result.Error;
                (result.Succeeded ? applied : failed).Add(edit);
            }
        });

        if (scopeError is not null)
        {
            Raise(PropertySessionChangeKind.Committed, null, false);
            return new CommitResult(0, [], scopeError);
        }

        foreach (var edit in applied)
        {
            Remove(edit);
            Deactivate(edit);
            _errors.Remove(edit.Path);
        }

        foreach (var edit in failed)
        {
            _errors[edit.Path] = edit.Error!;
        }

        Raise(PropertySessionChangeKind.Committed, null, applied.Any(e => e.Item.RefreshMode == System.ComponentModel.RefreshProperties.All));
        return new CommitResult(applied.Count, failed, null);
    }

    /// <summary>
    ///     Creates an element for a collection editor.
    /// </summary>
    /// <param name="editor">The editor.</param>
    /// <param name="element">The new element.</param>
    /// <returns>The outcome.</returns>
    internal PropertyResult CreateElement(CollectionEditor editor, out object? element) =>
        CreateElement(editor.Item, editor.ItemType, out element);

    /// <summary>
    ///     Creates an element for a collection property (through <see cref="CollectionItemFactory"/>, else the element
    ///     type's default constructor).
    /// </summary>
    /// <param name="item">The collection property.</param>
    /// <param name="itemType">The element type.</param>
    /// <param name="element">The new element.</param>
    /// <returns>The outcome.</returns>
    internal PropertyResult CreateElement(PropertyItem item, Type itemType, out object? element)
    {
        element = null;
        try
        {
            if (CollectionItemFactory?.Invoke(item) is { } produced)
            {
                element = produced;
                return PropertyResult.Success;
            }
        }
        catch (Exception ex) when (!PropertyErrors.IsFatal(ex))
        {
            return PropertyResult.Failure(PropertyErrors.Message(ex));
        }

        return CollectionApplier.TryCreateElement(itemType, out element, out var error)
            ? PropertyResult.Success
            : PropertyResult.Failure(error!);
    }

    /// <summary>
    ///     Records or applies the change a collection editor made to its working copy.
    /// </summary>
    /// <param name="editor">The editor.</param>
    /// <returns>The outcome.</returns>
    internal PropertyResult CollectionEdited(CollectionEditor editor)
    {
        var item = editor.Item;
        if (Mode == PropertyEditMode.Live)
        {
            var result = ApplyLive(item, () => CollectionApplier.Apply(item, editor.Items));
            editor.Reload();
            return result;
        }

        var existing = Find(item.Path);
        if (editor.MatchesTarget())
        {
            if (existing is not null)
            {
                Remove(existing);
            }
        }
        else if (existing is null)
        {
            _pending.Add(new PendingEdit(item, PendingEditKind.Collection, null, editor));
        }
        else
        {
            ReplaceEdit(existing, PendingEditKind.Collection, null, editor);
        }

        ClearError(item.Path);
        Raise(PropertySessionChangeKind.PendingChanged, item.Path, false);
        return PropertyResult.Success;
    }

    /// <summary>
    ///     Writes one pending edit.
    /// </summary>
    /// <param name="edit">The edit.</param>
    /// <returns>The outcome.</returns>
    private static PropertyResult Apply(PendingEdit edit) => edit.Kind switch
    {
        PendingEditKind.Value => edit.Item.TrySetValue(edit.Value),
        PendingEditKind.Reset => edit.Item.TryReset(),
        _ => edit.Item.IsCollection
            ? PropertyErrors.Run(() => CollectionApplier.Apply(edit.Item, edit.Collection!.Items))
            : PropertyResult.Failure($"'{edit.Item.DisplayName}' is not a collection."),
    };

    /// <summary>
    ///     Performs a write immediately inside the commit scope and records its outcome.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="write">The write.</param>
    /// <returns>The outcome.</returns>
    private PropertyResult ApplyLive(PropertyItem item, Action write)
    {
        var result = PropertyResult.Success;
        var scopeError = RunScoped(() => result = PropertyErrors.Run(write));
        if (scopeError is not null)
        {
            result = PropertyResult.Failure(scopeError);
        }

        if (!result.Succeeded)
        {
            return Fail(item, result.Error!);
        }

        ClearError(item.Path);
        Raise(PropertySessionChangeKind.Applied, item.Path, item.RefreshMode == System.ComponentModel.RefreshProperties.All);
        return result;
    }

    /// <summary>
    ///     Runs a body inside the commit scope.
    /// </summary>
    /// <param name="body">The body.</param>
    /// <returns>An error message when the scope failed or did not run the body; otherwise <see langword="null"/>.</returns>
    private string? RunScoped(Action body)
    {
        var scope = ComposeScope();
        if (scope is null)
        {
            body();
            return null;
        }

        var ran = false;
        try
        {
            scope(() =>
            {
                ran = true;
                body();
            });
        }
        catch (PropertyCommitRefusedException refused)
        {
            // A refusal that names a property is shown on that property's row
            _errors[refused.Path] = refused.Message;
            Raise(PropertySessionChangeKind.ErrorChanged, refused.Path, false);
            return refused.Message;
        }
        catch (Exception ex) when (!PropertyErrors.IsFatal(ex))
        {
            return PropertyErrors.Message(ex);
        }

        return ran ? null : "The commit scope did not apply the changes.";
    }

    /// <summary>
    ///     Combines the source's own commit scope (outermost) with the host's commit scope.
    /// </summary>
    /// <returns>The combined scope, or <see langword="null"/> when there is none.</returns>
    private Action<Action>? ComposeScope()
    {
        if (Source is not IPropertyCommitScope sourceScope)
        {
            return _commitScope;
        }

        var host = _commitScope;
        return host is null ? sourceScope.RunCommit : apply => sourceScope.RunCommit(() => host(apply));
    }

    /// <summary>
    ///     Records an error for an item.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="message">The message.</param>
    /// <returns>A failed result carrying the message.</returns>
    private PropertyResult Fail(PropertyItem item, string message)
    {
        _errors[item.Path] = message;
        Raise(PropertySessionChangeKind.ErrorChanged, item.Path, false);
        return PropertyResult.Failure(message);
    }

    /// <summary>
    ///     Clears the error of a path.
    /// </summary>
    /// <param name="path">The path.</param>
    private void ClearError(string path) => _errors.Remove(path);

    /// <summary>
    ///     Finds the pending edit of a path.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The edit, or <see langword="null"/>.</returns>
    private PendingEdit? Find(string path) => _pending.Find(e => e.Path == path);

    /// <summary>
    ///     Removes a pending edit.
    /// </summary>
    /// <param name="edit">The edit.</param>
    private void Remove(PendingEdit edit) => _pending.Remove(edit);

    /// <summary>
    ///     Changes what a pending edit does, keeping its position.
    /// </summary>
    /// <param name="edit">The edit.</param>
    /// <param name="kind">The new kind.</param>
    /// <param name="value">The new value.</param>
    /// <param name="collection">The new collection editor.</param>
    private static void ReplaceEdit(PendingEdit edit, PendingEditKind kind, object? value, CollectionEditor? collection)
    {
        if (!ReferenceEquals(edit.Collection, collection))
        {
            Deactivate(edit);
        }

        edit.Kind = kind;
        edit.Value = value;
        edit.Collection = collection;
        edit.Error = null;
    }

    /// <summary>
    ///     Ends the collection editor of an edit that is no longer pending.
    /// </summary>
    /// <param name="edit">The edit.</param>
    private static void Deactivate(PendingEdit edit)
    {
        if (edit.Collection is { } editor)
        {
            editor.IsActive = false;
        }
    }

    /// <summary>
    ///     Raises <see cref="Changed"/>.
    /// </summary>
    /// <param name="kind">What changed.</param>
    /// <param name="path">The affected path.</param>
    /// <param name="refreshAll">Whether everything must be re-read.</param>
    private void Raise(PropertySessionChangeKind kind, string? path, bool refreshAll) =>
        Changed?.Invoke(this, new PropertySessionChangedEventArgs(kind, path, refreshAll));
}

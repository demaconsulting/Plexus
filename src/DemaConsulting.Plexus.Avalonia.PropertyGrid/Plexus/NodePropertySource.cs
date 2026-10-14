using System.Text.Json;
using AvaloniaColor = Avalonia.Media.Color;
using PlexusColor = DemaConsulting.Plexus.Geometry.Color;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid.Plexus;

/// <summary>
///     Shows and edits one Plexus <see cref="Node"/> in a property grid: its name, its declared properties and its host
///     data. <c>grid.SelectedObject = node</c> uses it automatically.
/// </summary>
/// <remarks>
///     <para>
///         <b>Rows.</b> The <c>(Name)</c> row and the node's properties without a category are listed under "General"
///         (shown first); properties with a <see cref="PropertyDefinition.Category"/> are listed under it. A property's
///         row follows its <see cref="PropertyDefinition"/>: the display name (with the unit in parentheses), the
///         description, read-only, a bold value when it differs from the default (reset restores the default), a
///         drop-down for choices, a check box for booleans, a color editor for colors, text that is parsed with
///         <see cref="PropertyDefinition.TryParse"/> (invariant culture, a trailing unit is accepted, a mistake is shown
///         as the reason) and the custom editor named by <see cref="PropertyDefinition.EditorHint"/> (register it with
///         <see cref="PropertyGrid.EditorRegistry"/>). Colors appear as <c>Avalonia.Media.Color</c>.
///     </para>
///     <para>
///         <b>Host data.</b> When the node has <see cref="Node.Data"/> of a type its node type can copy (see
///         <see cref="Node.CloneData"/>), the properties of the data object are listed with all their annotations
///         (<c>Category</c>, <c>DefaultValue</c>, <c>TypeConverter</c>, nested objects, collections, ...). The grid edits a
///         working copy, so the live data is never mutated; the copy replaces <see cref="Node.Data"/> on commit.
///     </para>
///     <para>
///         <b>Name.</b> The name is validated while it is typed (not empty, unique when
///         <see cref="Diagram.RequireUniqueNodeNames"/> is set) and goes through the rename pipeline
///         (<see cref="Diagram.TryRename"/>) on commit, so a handler of <see cref="Diagram.NodeRenaming"/> can still veto
///         it; the reason is then shown on the name row.
///     </para>
///     <para>
///         <b>Undo.</b> The source is an <see cref="IPropertyCommitScope"/>: each commit is one undo entry that holds the
///         rename, the changed properties (applied with <see cref="PropertyBag.TrySetAll"/>) and the data, whatever the
///         grid's <see cref="PropertyEditMode"/>. A staged dialog therefore makes OK one undo step; a live panel makes each
///         edit its own step. When the node changes from elsewhere (undo, redo, another editor) the source raises
///         <see cref="Changed"/> so the grid shows the new values. Dispose the source to detach from the node.
///     </para>
///     <para>
///         A property bag value that is not declared by the node type (an ad-hoc value) is not listed.
///     </para>
/// </remarks>
/// <example>
///     <code><![CDATA[
///     // The grid picks the node adapter itself; a live grid makes every edit one undo entry
///     var grid = new PropertyGrid { EditMode = PropertyEditMode.Live, SelectedObject = node };
///
///     // The same through the source, for example to change the grid's content later
///     using var source = new NodePropertySource(node);
///     grid.Source = source;
///     ]]></code>
/// </example>
public sealed class NodePropertySource : IPropertySource, IPropertyCommitScope, IDisposable
{
    /// <summary>
    ///     The name of the name item.
    /// </summary>
    private const string NameItem = "Name";

    /// <summary>
    ///     The category of the name and of the properties without a category.
    /// </summary>
    private const string GeneralCategory = "General";

    /// <summary>
    ///     The edited node.
    /// </summary>
    private readonly Node _node;

    /// <summary>
    ///     The commit plumbing.
    /// </summary>
    private readonly ElementEditing _editing;

    /// <summary>
    ///     The items of the name and the declared properties.
    /// </summary>
    private readonly List<PropertyItem> _declaredItems = [];

    /// <summary>
    ///     The declared properties by name (those that can be published to the node).
    /// </summary>
    private readonly Dictionary<string, PropertyDefinition> _definitions = new(StringComparer.Ordinal);

    /// <summary>
    ///     All items.
    /// </summary>
    private IReadOnlyList<PropertyItem> _items = [];

    /// <summary>
    ///     The working copy of the host data.
    /// </summary>
    private object? _clone;

    /// <summary>
    ///     Whether <see cref="_clone"/> is valid (it is made on demand and thrown away after each commit).
    /// </summary>
    private bool _cloneValid;

    /// <summary>
    ///     The type of the host data the items were built for.
    /// </summary>
    private Type? _dataType;

    /// <summary>
    ///     Initializes a new instance of the <see cref="NodePropertySource"/> class.
    /// </summary>
    /// <param name="node">The node to show and edit; it must be attached to a diagram.</param>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="node"/> is not attached to a diagram.</exception>
    public NodePropertySource(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        _node = node;
        _editing = new ElementEditing(node, [nameof(Node.Name), nameof(Node.Properties), nameof(Node.Data)], Publish, DiscardClone);
        BuildDeclaredItems();
        BuildItems();
        _editing.Changed += OnEditingChanged;
    }

    /// <inheritdoc />
    public event EventHandler<PropertySourceChangedEventArgs>? Changed;

    /// <summary>
    ///     Gets the node that is shown and edited.
    /// </summary>
    public Node Node => _node;

    /// <inheritdoc />
    public IReadOnlyList<PropertyItem> GetItems() => _items;

    /// <inheritdoc />
    public void RunCommit(Action apply) => _editing.RunCommit(apply);

    /// <summary>
    ///     Stops listening to the node.
    /// </summary>
    public void Dispose() => _editing.Dispose();

    /// <summary>
    ///     Gets the working copy of the host data, making it when needed.
    /// </summary>
    private object? WorkingData
    {
        get
        {
            if (!_cloneValid)
            {
                _clone = TryClone();
                _cloneValid = true;
            }

            return _clone;
        }
    }

    /// <summary>
    ///     Copies the host data, or returns <see langword="null"/> when there is none or it cannot be copied.
    /// </summary>
    /// <returns>The copy.</returns>
    private object? TryClone()
    {
        try
        {
            return _node.CloneData();
        }
        catch (InvalidOperationException)
        {
            // The node type declares no way to copy its data, so the data is not editable here
            return null;
        }
    }

    /// <summary>
    ///     Throws the working copy of the data away.
    /// </summary>
    private void DiscardClone()
    {
        _clone = null;
        _cloneValid = false;
    }

    /// <summary>
    ///     Builds the name item and the items of the declared properties.
    /// </summary>
    private void BuildDeclaredItems()
    {
        var work = _editing.Work;
        _declaredItems.Add(new ElementPropertyItem(
            NameItem,
            typeof(string),
            work,
            () => _node.Name,
            new CustomPropertyOptions
            {
                DisplayName = "Name",
                Category = GeneralCategory,
                CategoryOrder = -1,
                Description = "The name of the node, as shown on the diagram.",
                IsParenthesized = true,
            },
            CheckName));

        foreach (var definition in _node.Type.Properties)
        {
            _definitions[definition.Name] = definition;
            _declaredItems.Add(CreateItem(definition, work));
        }
    }

    /// <summary>
    ///     Creates the item of a declared property.
    /// </summary>
    /// <param name="definition">The declaration.</param>
    /// <param name="work">Where written values are kept.</param>
    /// <returns>The item.</returns>
    private ElementPropertyItem CreateItem(PropertyDefinition definition, WorkingValues work)
    {
        var uncategorized = string.IsNullOrEmpty(definition.Category);
        var numberOrChoice = definition.ValueKind is PropertyValueKind.Int32 or PropertyValueKind.Int64 or PropertyValueKind.Double or PropertyValueKind.Choice;
        var options = new CustomPropertyOptions
        {
            DisplayName = string.IsNullOrEmpty(definition.Unit) ? definition.DisplayName : $"{definition.DisplayName} ({definition.Unit})",
            Category = uncategorized ? GeneralCategory : definition.Category,
            CategoryOrder = uncategorized ? -1 : 0,
            Description = definition.Description,
            ForceReadOnly = definition.IsReadOnly,
            EditorKey = definition.EditorHint,
            HasDefault = true,
            DefaultValue = ToItemValue(definition.DefaultValue),
            Converter = numberOrChoice ? new DefinitionConverter(definition) : null,
        };

        return new ElementPropertyItem(
            definition.Name,
            ItemType(definition.ValueKind),
            work,
            () => _node.Properties.TryGetValue(definition.Name, out var value) ? ToItemValue(value) : ToItemValue(definition.DefaultValue),
            options);
    }

    /// <summary>
    ///     Builds the item list: the declared items followed by the properties of the host data.
    /// </summary>
    private void BuildItems()
    {
        var items = new List<PropertyItem>(_declaredItems);
        var names = new HashSet<string>(items.Select(i => i.Name), StringComparer.Ordinal);
        var data = WorkingData;
        _dataType = _node.Data?.GetType();
        if (data is not null)
        {
            foreach (System.ComponentModel.PropertyDescriptor descriptor in System.ComponentModel.TypeDescriptor.GetProperties(data))
            {
                if (descriptor.IsBrowsable && names.Add(descriptor.Name))
                {
                    items.Add(new ReflectionPropertyItem(descriptor, () => WorkingData, null));
                }
            }
        }

        _items = items;
    }

    /// <summary>
    ///     Checks a proposed name against the rules that are known before the rename pipeline runs.
    /// </summary>
    /// <param name="value">The proposed name.</param>
    /// <returns>The reason the name is refused, or <see langword="null"/>.</returns>
    private string? CheckName(object? value)
    {
        var name = (value as string ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            return "A node name cannot be empty.";
        }

        var taken = _editing.Diagram.RequireUniqueNodeNames &&
                    _editing.Diagram.Pages.SelectMany(p => p.Nodes).Any(n => !ReferenceEquals(n, _node) && string.Equals(n.Name, name, StringComparison.Ordinal));
        return taken ? $"The name '{name}' is already used by another node." : null;
    }

    /// <summary>
    ///     Copies the working state to the node (inside the commit's undo transaction).
    /// </summary>
    /// <exception cref="PropertyCommitRefusedException">The rename or a property value was refused.</exception>
    private void Publish()
    {
        PublishName();
        PublishProperties();
        PublishData();
    }

    /// <summary>
    ///     Renames the node through the rename pipeline when the name was edited.
    /// </summary>
    private void PublishName()
    {
        if (_editing.Work.TryGet(NameItem, out var written) && written is string name &&
            !_editing.Diagram.TryRename(_node, name, out var reason))
        {
            throw new PropertyCommitRefusedException(NameItem, reason ?? "The name was refused.");
        }
    }

    /// <summary>
    ///     Applies the edited properties as one batch.
    /// </summary>
    private void PublishProperties()
    {
        var changes = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (name, value) in _editing.Work.Values)
        {
            if (_definitions.ContainsKey(name))
            {
                changes[name] = FromItemValue(value);
            }
        }

        if (changes.Count > 0 && !_node.Properties.TrySetAll(changes, out var errors))
        {
            var first = errors.First();
            throw new PropertyCommitRefusedException(first.Key, first.Value);
        }
    }

    /// <summary>
    ///     Replaces the host data with the working copy when it was edited.
    /// </summary>
    private void PublishData()
    {
        if (_cloneValid && _clone is not null && _node.Data is { } live && Serialize(live) != Serialize(_clone))
        {
            _node.Data = _clone;
        }
    }

    /// <summary>
    ///     Serializes host data the way the node type persists it, so two payloads can be compared.
    /// </summary>
    /// <param name="data">The data.</param>
    /// <returns>The JSON text.</returns>
    private string Serialize(object data) =>
        _node.Type.DataSerializer is { } serializer
            ? serializer.Serialize(data)
            : JsonSerializer.Serialize(data, _node.Type.DataType ?? data.GetType());

    /// <summary>
    ///     Reacts to a change that came from elsewhere (or the end of a commit): rebuilds the data items when the type
    ///     of the data changed and tells the grid to read again.
    /// </summary>
    /// <param name="sender">The commit plumbing.</param>
    /// <param name="e">The change.</param>
    private void OnEditingChanged(object? sender, PropertySourceChangedEventArgs e)
    {
        if (_node.Data?.GetType() != _dataType)
        {
            BuildItems();
        }

        Changed?.Invoke(this, e);
    }

    /// <summary>
    ///     Gets the item type that shows a property kind.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The type.</returns>
    private static Type ItemType(PropertyValueKind kind) => kind switch
    {
        PropertyValueKind.Boolean => typeof(bool),
        PropertyValueKind.Int32 => typeof(int),
        PropertyValueKind.Int64 => typeof(long),
        PropertyValueKind.Double => typeof(double),
        PropertyValueKind.Color => typeof(AvaloniaColor),
        _ => typeof(string),
    };

    /// <summary>
    ///     Converts a stored value to the value the item shows (colors become Avalonia colors).
    /// </summary>
    /// <param name="value">The stored value.</param>
    /// <returns>The item value.</returns>
    private static object? ToItemValue(object? value) =>
        value is PlexusColor color ? AvaloniaColor.FromArgb(color.A, color.R, color.G, color.B) : value;

    /// <summary>
    ///     Converts an item value back to the stored form (Avalonia colors become Plexus colors).
    /// </summary>
    /// <param name="value">The item value.</param>
    /// <returns>The stored value.</returns>
    private static object? FromItemValue(object? value) =>
        value is AvaloniaColor color ? PlexusColor.FromArgb(color.A, color.R, color.G, color.B) : value;
}

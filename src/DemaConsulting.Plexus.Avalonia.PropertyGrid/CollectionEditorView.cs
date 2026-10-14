using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid;

/// <summary>
///     An embeddable editor for the elements of a collection property: a list of the members with Add, Remove and Move
///     buttons, and a nested <see cref="PropertyGrid"/> for the selected member.
/// </summary>
/// <remarks>
///     <para>
///         The view edits its own working list. Adding, removing and reordering change that list only, and the selected
///         member's properties are edited in the nested grid (in live mode, on the member object itself, as in the
///         WinForms collection editor). <see cref="Apply"/> hands the finished list to the owning session through
///         <see cref="CollectionEditor.SetItems"/>, which writes it at once in live mode or records it as one pending
///         edit in staged mode. A host that does not call <see cref="Apply"/> discards the list changes (the cancel
///         path); changes made to the properties of member objects are not undone by that.
///     </para>
///     <para>
///         A member that is an object with properties (a class without a text converter) shows those properties; a member
///         that is a value (string, number, struct, enumeration, nested collection) shows a single <c>Value</c> row. The
///         member text comes from <see cref="ItemFormatter"/>, else <c>ToString()</c>, else the element's default
///         property, its <c>DisplayName</c> annotation or its type name. Nested collections open the same editor
///         recursively. <see cref="CollectionEditorDialog"/> hosts the view in a window with OK and Cancel.
///     </para>
/// </remarks>
/// <example>
///     <code><![CDATA[
///     var view = new CollectionEditorView(grid.Session!, grid.Session!.FindItem("Tags")!);
///     panel.Children.Add(view);
///     // ... later, when the host's own OK button is pressed:
///     PropertyResult result = view.Apply();
///     ]]></code>
/// </example>
public sealed class CollectionEditorView : UserControl
{
    /// <summary>
    ///     The session that owns the collection property.
    /// </summary>
    private readonly PropertyEditSession _session;

    /// <summary>
    ///     The elements as the collection had them when the view was created.
    /// </summary>
    private readonly List<object?> _initial;

    /// <summary>
    ///     The working list.
    /// </summary>
    private readonly List<object?> _list;

    /// <summary>
    ///     The list box of the members.
    /// </summary>
    private readonly ListBox _members = new();

    /// <summary>
    ///     The nested grid of the selected member.
    /// </summary>
    private readonly PropertyGrid _memberGrid;

    /// <summary>
    ///     The Add button.
    /// </summary>
    private readonly Button _add = new();

    /// <summary>
    ///     The Remove button.
    /// </summary>
    private readonly Button _remove = new();

    /// <summary>
    ///     The Move up button.
    /// </summary>
    private readonly Button _up = new();

    /// <summary>
    ///     The Move down button.
    /// </summary>
    private readonly Button _down = new();

    /// <summary>
    ///     The text that shows the last error.
    /// </summary>
    private readonly TextBlock _message = new();

    /// <summary>
    ///     Whether a value member (string, number, struct) was edited through its Value row; an edit of a boxed struct
    ///     happens in place, so comparing with the initial list alone would not see it.
    /// </summary>
    private bool _valueEdited;

    /// <summary>
    ///     The member session whose changes the view already observes.
    /// </summary>
    private PropertyEditSession? _hookedSession;

    /// <summary>
    ///     Whether the list box is being filled by the view (selection events are ignored).
    /// </summary>
    private bool _filling;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CollectionEditorView"/> class.
    /// </summary>
    /// <param name="session">The session that owns the collection property.</param>
    /// <param name="item">The collection property (<see cref="PropertyItem.IsCollection"/>).</param>
    /// <param name="itemFormatter">
    ///     Produces the text of an element; <see langword="null"/> uses <c>ToString()</c> and the fallbacks described in
    ///     the type remarks.
    /// </param>
    /// <param name="editors">
    ///     The editor registry of the nested grid; <see langword="null"/> uses <see cref="PropertyGrid.DefaultEditors"/>.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="item"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The item is not a collection.</exception>
    public CollectionEditorView(
        PropertyEditSession session,
        PropertyItem item,
        Func<object?, string>? itemFormatter = null,
        PropertyEditorRegistry<PropertyEditorFactory>? editors = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(item);
        _session = session;
        Item = item;
        ItemType = item.CollectionItemType ?? throw new ArgumentException($"'{item.Name}' is not a collection property.", nameof(item));
        ItemFormatter = itemFormatter;
        CollectionApplier.GetCapabilities(item, out var canResize, out var canReorder);
        CanResize = canResize;
        CanReorder = canReorder;
        _list = session.GetValue(item) is IEnumerable<object?> pending && session.IsPending(item) ? [.. pending] : [.. item.GetCollectionValues()];
        _initial = [.. _list];

        _memberGrid = new PropertyGrid
        {
            ShowDescription = false,
            ShowSearch = false,
            SortMode = PropertySortMode.Alphabetical,
            CollectionItemFactory = session.CollectionItemFactory,
            CollectionItemFormatter = itemFormatter,
            NameColumnWidth = 130,
        };
        if (editors is not null)
        {
            _memberGrid.EditorRegistry = editors;
        }

        BuildLayout();
        Fill(_list.Count > 0 ? 0 : -1);
        AutomationProperties.SetAutomationId(this, "CollectionEditorView");
    }

    /// <summary>
    ///     Occurs when the working list or a member changed.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    ///     Gets the collection property being edited.
    /// </summary>
    public PropertyItem Item { get; }

    /// <summary>
    ///     Gets the element type of the collection.
    /// </summary>
    public Type ItemType { get; }

    /// <summary>
    ///     Gets or sets the formatter that produces the text of an element; <see langword="null"/> uses the defaults.
    /// </summary>
    public Func<object?, string>? ItemFormatter { get; set; }

    /// <summary>
    ///     Gets a value indicating whether elements can be added and removed.
    /// </summary>
    public bool CanResize { get; }

    /// <summary>
    ///     Gets a value indicating whether elements can be reordered.
    /// </summary>
    public bool CanReorder { get; }

    /// <summary>
    ///     Gets the working list (a snapshot is taken when the view is created; <see cref="Apply"/> writes it back).
    /// </summary>
    public IReadOnlyList<object?> Items => _list;

    /// <summary>
    ///     Gets the nested grid that edits the selected member.
    /// </summary>
    public PropertyGrid MemberGrid => _memberGrid;

    /// <summary>
    ///     Gets a value indicating whether the working list differs from the collection it started from.
    /// </summary>
    public bool HasChanges => _valueEdited || !_list.SequenceEqual(_initial);

    /// <summary>
    ///     Gets or sets the index of the selected member (-1 for none).
    /// </summary>
    public int SelectedIndex
    {
        get => _members.SelectedIndex;
        set => _members.SelectedIndex = value;
    }

    /// <summary>
    ///     Creates a new element (through the session's <see cref="PropertyEditSession.CollectionItemFactory"/> or the
    ///     element type's default constructor) and adds it after the selected member, or at the end.
    /// </summary>
    /// <returns>The outcome; a failure (for example an element type without a default constructor) is also shown in the view.</returns>
    public PropertyResult Add()
    {
        if (!CanResize)
        {
            return Show(PropertyResult.Failure($"'{Item.DisplayName}' is read-only; its elements cannot be added or removed."));
        }

        var created = _session.CreateElement(Item, ItemType, out var element);
        if (!created.Succeeded)
        {
            return Show(created);
        }

        var index = SelectedIndex >= 0 ? SelectedIndex + 1 : _list.Count;
        _list.Insert(index, element);
        Fill(index);
        Changed?.Invoke(this, EventArgs.Empty);
        return Show(PropertyResult.Success);
    }

    /// <summary>
    ///     Removes the selected member and selects its neighbor.
    /// </summary>
    /// <returns>The outcome.</returns>
    public PropertyResult Remove()
    {
        var index = SelectedIndex;
        if (!CanResize || index < 0)
        {
            return Show(PropertyResult.Failure(CanResize ? "No member is selected." : $"'{Item.DisplayName}' is read-only; its elements cannot be added or removed."));
        }

        _list.RemoveAt(index);
        Fill(Math.Min(index, _list.Count - 1));
        Changed?.Invoke(this, EventArgs.Empty);
        return Show(PropertyResult.Success);
    }

    /// <summary>
    ///     Moves the selected member up (<paramref name="delta"/> = -1) or down (<paramref name="delta"/> = 1).
    /// </summary>
    /// <param name="delta">The direction.</param>
    /// <returns>The outcome.</returns>
    public PropertyResult Move(int delta)
    {
        var index = SelectedIndex;
        var target = index + delta;
        if (!CanReorder || index < 0 || target < 0 || target >= _list.Count)
        {
            return Show(PropertyResult.Failure("The member cannot be moved."));
        }

        (_list[index], _list[target]) = (_list[target], _list[index]);
        Fill(target);
        Changed?.Invoke(this, EventArgs.Empty);
        return Show(PropertyResult.Success);
    }

    /// <summary>
    ///     Writes the working list to the owning session (one write in live mode, one pending edit in staged mode).
    ///     Does nothing when the list has not changed.
    /// </summary>
    /// <returns>The outcome; a failure is also shown in the view.</returns>
    public PropertyResult Apply()
    {
        if (!HasChanges)
        {
            return PropertyResult.Success;
        }

        var editor = _session.BeginCollectionEdit(Item);
        return Show(editor.SetItems(_list));
    }

    /// <summary>
    ///     Gets the text of an element: the <see cref="ItemFormatter"/>, else <c>ToString()</c>, else the default property,
    ///     the <c>DisplayName</c> annotation or the type name.
    /// </summary>
    /// <param name="value">The element.</param>
    /// <returns>The text.</returns>
    public string FormatItem(object? value)
    {
        if (ItemFormatter is { } formatter)
        {
            return formatter(value);
        }

        if (value is null)
        {
            return "(null)";
        }

        if (value is string { Length: 0 })
        {
            return "(empty)";
        }

        var text = value.ToString();
        var type = value.GetType();
        if (!string.IsNullOrEmpty(text) && text != type.ToString())
        {
            return text;
        }

        if (TypeDescriptor.GetDefaultProperty(value) is { } property && property.GetValue(value)?.ToString() is { Length: > 0 } fromProperty)
        {
            return fromProperty;
        }

        return TypeDescriptor.GetAttributes(value).OfType<DisplayNameAttribute>().FirstOrDefault() is { DisplayName.Length: > 0 } attribute
            ? attribute.DisplayName
            : type.Name;
    }

    /// <summary>
    ///     Builds the visual tree.
    /// </summary>
    private void BuildLayout()
    {
        ConfigureButton(_add, "Add", "CollectionEditor_Add", "Add a member");
        ConfigureButton(_remove, "Remove", "CollectionEditor_Remove", "Remove the selected member");
        ConfigureButton(_up, "\u25B2", "CollectionEditor_MoveUp", "Move the selected member up");
        ConfigureButton(_down, "\u25BC", "CollectionEditor_MoveDown", "Move the selected member down");
        _up.Width = 30;
        _down.Width = 30;
        _add.Click += (_, _) => Add();
        _remove.Click += (_, _) => Remove();
        _up.Click += (_, _) => Move(-1);
        _down.Click += (_, _) => Move(1);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(0, 6, 0, 0) };
        buttons.Children.Add(_add);
        buttons.Children.Add(_remove);
        buttons.Children.Add(_up);
        buttons.Children.Add(_down);

        _members.ItemTemplate = new FuncDataTemplate<MemberEntry>((entry, _) =>
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("28,*"), Height = GridTheme.RowHeight - 2 };
            row.Children.Add(new TextBlock { Text = entry?.Index.ToString(System.Globalization.CultureInfo.InvariantCulture), Opacity = 0.6, FontSize = GridTheme.FontSize, VerticalAlignment = VerticalAlignment.Center });
            var text = new TextBlock { Text = entry?.Text, FontSize = GridTheme.FontSize, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            Grid.SetColumn(text, 1);
            row.Children.Add(text);
            return row;
        });
        _members.SelectionChanged += (_, _) => OnMemberSelected();
        AutomationProperties.SetAutomationId(_members, "CollectionEditor_Members");
        AutomationProperties.SetName(_members, "Members");

        var header = new TextBlock { Text = "Members", FontWeight = FontWeight.SemiBold, FontSize = GridTheme.FontSize, Margin = new Thickness(0, 0, 0, 4) };
        var left = new DockPanel { Margin = new Thickness(0, 0, 8, 0) };
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(buttons, Dock.Bottom);
        left.Children.Add(header);
        left.Children.Add(buttons);
        left.Children.Add(_members);

        var rightHeader = new TextBlock { Text = "Properties", FontWeight = FontWeight.SemiBold, FontSize = GridTheme.FontSize, Margin = new Thickness(0, 0, 0, 4) };
        var right = new DockPanel();
        DockPanel.SetDock(rightHeader, Dock.Top);
        right.Children.Add(rightHeader);
        right.Children.Add(_memberGrid);

        _message.Foreground = GridTheme.ErrorBrush;
        _message.FontSize = GridTheme.FontSize;
        _message.TextWrapping = TextWrapping.Wrap;
        _message.Margin = new Thickness(0, 6, 0, 0);
        AutomationProperties.SetAutomationId(_message, "CollectionEditor_Message");

        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("240,*") };
        body.Children.Add(left);
        Grid.SetColumn(right, 1);
        body.Children.Add(right);

        var root = new DockPanel { Margin = new Thickness(10) };
        DockPanel.SetDock(_message, Dock.Bottom);
        root.Children.Add(_message);
        root.Children.Add(body);
        Content = root;
    }

    /// <summary>
    ///     Configures a button of the view.
    /// </summary>
    /// <param name="button">The button.</param>
    /// <param name="text">The caption.</param>
    /// <param name="id">The automation id.</param>
    /// <param name="tip">The tooltip.</param>
    private static void ConfigureButton(Button button, string text, string id, string tip)
    {
        button.Content = text;
        button.MinWidth = 0;
        button.Padding = new Thickness(10, 3);
        button.FontSize = GridTheme.FontSize;
        ToolTip.SetTip(button, tip);
        AutomationProperties.SetAutomationId(button, id);
        AutomationProperties.SetName(button, tip);
    }

    /// <summary>
    ///     Refills the member list from the working list.
    /// </summary>
    /// <param name="select">The index to select (-1 for none).</param>
    private void Fill(int select)
    {
        _filling = true;
        _members.ItemsSource = _list.Select((value, index) => new MemberEntry(index, FormatItem(value))).ToList();
        _members.SelectedIndex = select;
        _filling = false;
        OnMemberSelected();
    }

    /// <summary>
    ///     Shows the selected member in the nested grid and updates the buttons.
    /// </summary>
    private void OnMemberSelected()
    {
        if (_filling)
        {
            return;
        }

        var index = SelectedIndex;
        _remove.IsEnabled = CanResize && index >= 0;
        _add.IsEnabled = CanResize;
        _up.IsEnabled = CanReorder && index > 0;
        _down.IsEnabled = CanReorder && index >= 0 && index < _list.Count - 1;
        ShowMember(index);
    }

    /// <summary>
    ///     Points the nested grid at a member.
    /// </summary>
    /// <param name="index">The member index, or -1 for none.</param>
    private void ShowMember(int index)
    {
        if (index < 0 || index >= _list.Count || _list[index] is not { } value)
        {
            _memberGrid.SelectedObject = null;
            return;
        }

        if (ShowsObjectProperties(value))
        {
            _memberGrid.SelectedObject = value;
        }
        else
        {
            var type = ItemType == typeof(object) ? value.GetType() : ItemType;
            _memberGrid.Source = new CustomPropertySource([new CustomPropertyItem("Value", type, () => _list[index], v =>
            {
                _list[index] = v;
                _valueEdited = true;
            })]);
        }

        if (_memberGrid.Session is { } session && !ReferenceEquals(session, _hookedSession))
        {
            _hookedSession = session;
            session.Changed += (_, _) => OnMemberEdited(SelectedIndex);
        }
    }

    /// <summary>
    ///     Refreshes the text of a member after one of its properties changed.
    /// </summary>
    /// <param name="index">The member index.</param>
    private void OnMemberEdited(int index)
    {
        if (index == SelectedIndex && index < _list.Count && _members.ItemsSource is List<MemberEntry> entries && index < entries.Count)
        {
            var text = FormatItem(_list[index]);
            if (entries[index].Text != text)
            {
                _filling = true;
                _members.ItemsSource = _list.Select((v, i) => new MemberEntry(i, FormatItem(v))).ToList();
                _members.SelectedIndex = index;
                _filling = false;
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    ///     Decides whether a member shows its own properties (an object) or a single value row.
    /// </summary>
    /// <param name="value">The member.</param>
    /// <returns><see langword="true"/> for an object with properties.</returns>
    private static bool ShowsObjectProperties(object value)
    {
        var type = value.GetType();
        return !type.IsValueType
            && type != typeof(string)
            && !CollectionTypes.TryGetElementType(type, out _)
            && !TypeDescriptor.GetConverter(value).CanConvertFrom(typeof(string));
    }

    /// <summary>
    ///     Shows an outcome below the editor.
    /// </summary>
    /// <param name="result">The outcome.</param>
    /// <returns>The same outcome.</returns>
    private PropertyResult Show(PropertyResult result)
    {
        _message.Text = result.Error ?? string.Empty;
        return result;
    }

    /// <summary>
    ///     One entry of the member list.
    /// </summary>
    /// <param name="Index">The position in the collection.</param>
    /// <param name="Text">The text of the member.</param>
    private sealed record MemberEntry(int Index, string Text);
}

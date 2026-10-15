using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using DemaConsulting.Plexus.Scene;
using AvaloniaSize = Avalonia.Size;

namespace DemaConsulting.Plexus.Avalonia;

/// <summary>
///     A palette of node types that can be dragged onto a <see cref="DiagramEditor"/>, each drawn exactly as the node looks on the canvas.
/// </summary>
/// <remarks>
///     <para>
///         The control works with zero configuration: <c>&lt;plexus:NodePalette Editor="{Binding #Editor}"/&gt;</c>
///         (with <c>xmlns:plexus="https://github.com/demaconsulting/plexus"</c> and a
///         <see cref="DiagramEditor"/> named <c>Editor</c>) lists every node type of the editor's diagram, with a
///         search box, lets the user drag one onto the editor to create a node, and adds it at the editor's
///         center on double-click or Enter. It follows the editor when a new diagram is opened, and uses its theme.
///         Without an editor, <c>&lt;plexus:NodePalette Types="{Binding Registry}"/&gt;</c> lists every node type of a
///         <see cref="NodeTypeRegistry"/>, or of any sequence of <see cref="NodeType"/>; dragging onto an
///         editor still needs nothing else, because the editor accepts the drop.
///     </para>
///     <para>
///         Every item is the very scene the canvas would draw for a freshly added node of that type (box,
///         header, icon, ports, port labels, default-value summary and any custom
///         <see cref="INodeVisual"/>), built by the same <see cref="SceneBuilder"/> and painted by the same
///         painter, so the palette cannot drift from the canvas. Bind <see cref="Zoom"/> to the editor's
///         zoom and <see cref="DiagramTheme"/> to its theme to keep them in step.
///         A palette entry shows the type's default look: <c>Diagram.NodeStyler</c> and <c>Diagram.WireStyler</c> are not applied to it. The entry is built
///         from a private scratch node in a separate diagram that has no stylers, and a styler decides from the state of a real node (its property values,
///         its wires), which a palette entry, standing for a type rather than a node, does not have. A node created from the palette is styled as soon as it is on the canvas.
///         <c>NodeType.Visual</c>, icons, categories and the theme do apply.
///     </para>
///     <para>
///         Types are grouped by <see cref="NodeType.Category"/> (in registration order, uncategorized last)
///         unless <see cref="GroupByCategory"/> is off, and <see cref="Filter"/> narrows the list. Double-click
///         or Enter raises <see cref="NodeTypeActivated"/> for hosts that also add nodes by activation. A palette that is wide
///         enough lays the items of each category out in several columns (see <see cref="MaxColumns"/>), with the boxes lined up in a grid. The
///         arrow keys move the selection. When the sequence given as <see cref="Types"/> is not a
///         <see cref="NodeTypeRegistry"/>, its port types are unknown and ports are drawn neutral grey;
///         pass the registry for the exact port colors. A registry that changes after it was given
///         (types added or removed) is picked up by <see cref="Refresh"/>. It must be used on the UI thread. The palette derives from Avalonia's <c>Control</c>,
///         so the members every control has (<c>Bounds</c>, <c>IsVisible</c>, <c>Width</c>, <c>Focus()</c>, ...) are available but not repeated in the member tables of this page.
///     </para>
/// </remarks>
/// <example>
///     In XAML, bind the palette to the editor; it follows the editor's diagram, theme and zoom. When a <c>GridSplitter</c> resizes the
///     palette, the width belongs to the grid column, so do not set a <c>Width</c> on the palette itself:
///     <code lang="xml"><![CDATA[
///     <Grid ColumnDefinitions="220,5,*">
///         <plexus:NodePalette Grid.Column="0" Editor="{Binding #Editor}" />
///         <GridSplitter Grid.Column="1" ResizeDirection="Columns" />
///         <plexus:DiagramEditor Grid.Column="2" x:Name="Editor" Page="{Binding CurrentPage}" />
///     </Grid>
///     ]]></code>
///     <para>
///         Without a splitter a fixed width is fine: <c>&lt;plexus:NodePalette DockPanel.Dock="Left" Width="220" Editor="{Binding #Editor}"/&gt;</c>.
///         From code, a palette can also stand alone (<c>types</c> is the registry of the <c>DemaConsulting.Plexus</c> namespace example):
///     </para>
///     <code>
///     using DemaConsulting.Plexus;
///     using DemaConsulting.Plexus.Avalonia;
///
///     var diagram = new Diagram(types);
///     var page = diagram.Pages.Add("Main");
///
///     var palette = new NodePalette
///     {
///         Types = types,                                        // a NodeTypeRegistry (exact port colors) or any IEnumerable&lt;NodeType&gt;
///         GroupByCategory = true,
///         ShowFilter = true,
///         Filter = "sink",                                      // narrow the list
///     };
///
///     // Without an Editor binding you decide what activation (double-click or Enter) does
///     palette.NodeTypeActivated += (_, e) =&gt;
///     {
///         Console.WriteLine($"add {e.NodeType.Key}");
///         e.Handled = true;                                     // suppress the palette's own add-to-editor action
///     };
///
///     palette.Editor = new DiagramEditor { Page = page };       // or connect it to an editor later; dropping then just works
///     </code>
/// </example>
public sealed partial class NodePalette : Control
{
    /// <summary>
    ///     Defines the <see cref="Types"/> property.
    /// </summary>
    public static readonly StyledProperty<IEnumerable<NodeType>?> TypesProperty =
        AvaloniaProperty.Register<NodePalette, IEnumerable<NodeType>?>(nameof(Types));

    /// <summary>
    ///     Defines the <see cref="DiagramTheme"/> property.
    /// </summary>
    public static readonly StyledProperty<DiagramTheme?> DiagramThemeProperty =
        AvaloniaProperty.Register<NodePalette, DiagramTheme?>(nameof(DiagramTheme));

    /// <summary>
    ///     Defines the <see cref="Zoom"/> property.
    /// </summary>
    public static readonly StyledProperty<double> ZoomProperty =
        AvaloniaProperty.Register<NodePalette, double>(nameof(Zoom), 1.0, coerce: CoerceZoom);

    /// <summary>
    ///     Defines the <see cref="Filter"/> property.
    /// </summary>
    public static readonly StyledProperty<string?> FilterProperty =
        AvaloniaProperty.Register<NodePalette, string?>(nameof(Filter));

    /// <summary>
    ///     Defines the <see cref="GroupByCategory"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> GroupByCategoryProperty =
        AvaloniaProperty.Register<NodePalette, bool>(nameof(GroupByCategory), true);

    /// <summary>
    ///     Defines the <see cref="UncategorizedHeader"/> property.
    /// </summary>
    public static readonly StyledProperty<string> UncategorizedHeaderProperty =
        AvaloniaProperty.Register<NodePalette, string>(nameof(UncategorizedHeader), "Other");

    /// <summary>
    ///     Defines the <see cref="SelectedNodeType"/> property.
    /// </summary>
    public static readonly StyledProperty<NodeType?> SelectedNodeTypeProperty =
        AvaloniaProperty.Register<NodePalette, NodeType?>(nameof(SelectedNodeType), defaultBindingMode: global::Avalonia.Data.BindingMode.TwoWay);

    /// <summary>
    ///     Defines the <see cref="Editor"/> property.
    /// </summary>
    public static readonly StyledProperty<DiagramEditor?> EditorProperty =
        AvaloniaProperty.Register<NodePalette, DiagramEditor?>(nameof(Editor));

    /// <summary>
    ///     Defines the <see cref="MaxColumns"/> property.
    /// </summary>
    public static readonly StyledProperty<int> MaxColumnsProperty =
        AvaloniaProperty.Register<NodePalette, int>(nameof(MaxColumns), 0, coerce: CoerceMaxColumns);

    /// <summary>
    ///     Defines the <see cref="MinColumnSpacing"/> property.
    /// </summary>
    public static readonly StyledProperty<double> MinColumnSpacingProperty =
        AvaloniaProperty.Register<NodePalette, double>(nameof(MinColumnSpacing), 4.0, coerce: CoerceMinColumnSpacing);

    /// <summary>
    ///     Defines the <see cref="ShowFilter"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> ShowFilterProperty =
        AvaloniaProperty.Register<NodePalette, bool>(nameof(ShowFilter), true);

    /// <summary>
    ///     The AutomationId of the built-in filter box.
    /// </summary>
    public const string FilterAutomationId = "NodePalette_Filter";

    /// <summary>
    ///     The space, in device-independent pixels, between rows of entries, and the margin around them.
    /// </summary>
    private const double Spacing = 4;

    /// <summary>
    ///     The painter shared by all items, which owns the brush, pen, text and icon caches.
    /// </summary>
    private readonly ScenePainter _painter = new();

    /// <summary>
    ///     Measures text with Avalonia so the item scenes match the canvas.
    /// </summary>
    private readonly AvaloniaTextMeasurer _measurer = new();

    /// <summary>
    ///     The prototypes, built once per type and kept until the registry or theme changes.
    /// </summary>
    private readonly NodePrototypeCache _prototypes = new();

    /// <summary>
    ///     The items currently shown, in display order.
    /// </summary>
    private readonly List<NodePaletteItem> _items = [];

    /// <summary>
    ///     The scroll viewer that holds the entries.
    /// </summary>
    private readonly ScrollViewer _scroller;

    /// <summary>
    ///     The panel the headers and items are laid out in: headers across the full width, items in a grid of equal cells.
    /// </summary>
    private readonly NodePalettePanel _stack;

    /// <summary>
    ///     The search box above the entries, shown while <see cref="ShowFilter"/> is on.
    /// </summary>
    private readonly TextBox _filterBox;

    /// <summary>
    ///     The palette's only visual child: the search box over the scroll viewer.
    /// </summary>
    private readonly DockPanel _layout;

    /// <summary>
    ///     The registry the prototypes are built from, or null when no types are shown.
    /// </summary>
    private NodeTypeRegistry? _registry;

    /// <summary>
    ///     Whether the entries must be rebuilt before the next layout or query.
    /// </summary>
    private bool _itemsDirty = true;

    /// <summary>
    ///     Initializes a new instance of the <see cref="NodePalette"/> class.
    /// </summary>
    public NodePalette()
    {
        // The palette is one keyboard stop; arrow keys move the selection inside it
        Focusable = true;
        ClipToBounds = true;
        _stack = new NodePalettePanel(Spacing) { Margin = new Thickness(Spacing), MinColumnSpacing = MinColumnSpacing };
        _scroller = new ScrollViewer
        {
            Content = _stack,
            HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
        };
        _filterBox = new TextBox { PlaceholderText = "Search nodes", Margin = new Thickness(Spacing, Spacing, Spacing, 0) };
        AutomationProperties.SetAutomationId(_filterBox, FilterAutomationId);
        AutomationProperties.SetName(_filterBox, "Search nodes");
        _filterBox.PropertyChanged += OnFilterBoxPropertyChanged;

        // The filter box sits over the scrolling entries; the dock panel is the palette's only visual child, added by hand because the palette is a plain control
        _layout = new DockPanel();
        DockPanel.SetDock(_filterBox, Dock.Top);
        _layout.Children.Add(_filterBox);
        _layout.Children.Add(_scroller);
        LogicalChildren.Add(_layout);
        VisualChildren.Add(_layout);

        // A stable default lets UI automation find the palette; hosts may override it
        AutomationProperties.SetAutomationId(this, "NodePalette");
        AutomationProperties.SetName(this, "Node palette");
    }

    /// <summary>
    ///     Occurs when the user activates a node type: double-click, or Enter on the selected item.
    /// </summary>
    /// <remarks>
    ///     Set <see cref="NodeTypeActivatedEventArgs.Handled"/> to suppress the default action of a palette
    ///     that follows an <see cref="Editor"/> (adding the node at the editor's center).
    /// </remarks>
    public event EventHandler<NodeTypeActivatedEventArgs>? NodeTypeActivated;

    /// <summary>
    ///     Gets or sets the editor the palette works with; the palette then follows its document and activation adds nodes to it.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         With an editor set, a host needs no code at all: <c>&lt;plexus:NodePalette Editor="{Binding #Editor}"/&gt;</c>.
    ///         The palette lists the node types of the registry of the editor's current page's diagram, and
    ///         follows when the editor shows another page or diagram (New, Open). It is drawn with the
    ///         editor's effective theme, including later changes of the diagram's theme. Activating a type (double-click,
    ///         Enter, <see cref="Activate"/>) adds the node at the editor's visible center, as one undo entry,
    ///         selected, unless a <see cref="NodeTypeActivated"/> handler marks the event handled.
    ///     </para>
    ///     <para>
    ///         An explicit <see cref="Types"/> or <see cref="DiagramTheme"/> always wins over the
    ///         editor's. The palette listens to the editor only while it is in the visual tree, so a discarded palette never
    ///         stays alive through the editor.
    ///     </para>
    /// </remarks>
    public DiagramEditor? Editor
    {
        get => GetValue(EditorProperty);
        set => SetValue(EditorProperty, value);
    }

    /// <summary>
    ///     Gets or sets a value indicating whether a search box is shown above the entries (default true).
    /// </summary>
    /// <remarks>
    ///     The box edits <see cref="Filter"/> and is the other way round too: setting <see cref="Filter"/>
    ///     fills the box. It has the AutomationId <see cref="FilterAutomationId"/> (<c>NodePalette_Filter</c>).
    ///     Turn it off to supply a search UI of your own and bind <see cref="Filter"/> to it.
    /// </remarks>
    public bool ShowFilter
    {
        get => GetValue(ShowFilterProperty);
        set => SetValue(ShowFilterProperty, value);
    }
    /// <summary>
    ///     Gets or sets the node types to show: a <see cref="NodeTypeRegistry"/>, or any sequence of types.
    /// </summary>
    /// <remarks>
    ///     A registry also supplies the port colors. Null shows an empty palette.
    /// </remarks>
    public IEnumerable<NodeType>? Types
    {
        get => GetValue(TypesProperty);
        set => SetValue(TypesProperty, value);
    }

    /// <summary>
    ///     Gets or sets the theme the items are drawn with; null (the default) uses the followed <see cref="Editor"/>'s theme, or <see cref="Scene.DiagramTheme.Classic"/> when there is no editor.
    /// </summary>
    /// <remarks>
    ///     The theme changes the look of the items only: port positions and port triangles follow the diagram theme the
    ///     prototypes are sized with, as on the canvas.
    /// </remarks>
    public DiagramTheme? DiagramTheme
    {
        get => GetValue(DiagramThemeProperty);
        set => SetValue(DiagramThemeProperty, value);
    }

    /// <summary>
    ///     Gets or sets the zoom factor the items are drawn at (1 = actual size), clamped like <see cref="DiagramEditor.Zoom"/>.
    /// </summary>
    public double Zoom
    {
        get => GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    /// <summary>
    ///     Gets or sets the text that narrows the list; null or empty shows every type.
    /// </summary>
    /// <remarks>
    ///     A type is shown when its display name, title, key or category contains the text, ignoring case.
    /// </remarks>
    public string? Filter
    {
        get => GetValue(FilterProperty);
        set => SetValue(FilterProperty, value);
    }

    /// <summary>
    ///     Gets or sets a value indicating whether types are listed under <see cref="NodeType.Category"/> headers (default true).
    /// </summary>
    /// <remarks>
    ///     Headers appear only when at least one visible type has a category, so a registry without
    ///     categories is a plain list. Categories are listed in the order they first appear in the
    ///     registry; uncategorized types come last under <see cref="UncategorizedHeader"/>.
    /// </remarks>
    public bool GroupByCategory
    {
        get => GetValue(GroupByCategoryProperty);
        set => SetValue(GroupByCategoryProperty, value);
    }

    /// <summary>
    ///     Gets or sets the header shown over types that have no category when other types do (default "Other").
    /// </summary>
    public string UncategorizedHeader
    {
        get => GetValue(UncategorizedHeaderProperty);
        set => SetValue(UncategorizedHeaderProperty, value);
    }

    /// <summary>
    ///     Gets or sets the most columns the items of a category may be laid out in; zero (the default) means as many as fit the palette's width.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The palette lays the items out in a grid of equal cells: each cell is as wide as the widest
    ///         item (see <see cref="MinColumnSpacing"/>), and as many cells as fit side by side form a row, so
    ///         widening the palette gives two, three or more columns. Set a number to cap that, for example
    ///         1 for a plain list. A negative value counts as zero.
    ///     </para>
    ///     <para>
    ///         The node boxes line up down each column and across each row; category headers always use the
    ///         full width. The left and right arrow keys move within a row and the up and down arrow keys by a row.
    ///     </para>
    /// </remarks>
    public int MaxColumns
    {
        get => GetValue(MaxColumnsProperty);
        set => SetValue(MaxColumnsProperty, value);
    }

    /// <summary>
    ///     Gets or sets the smallest horizontal space, in device-independent pixels, between two cells of a row (default 4); a negative value counts as zero.
    /// </summary>
    /// <remarks>
    ///     It decides how many columns fit: a further column is added once the width holds it with this much space
    ///     before it. Any leftover width is shared out between the cells.
    /// </remarks>
    public double MinColumnSpacing
    {
        get => GetValue(MinColumnSpacingProperty);
        set => SetValue(MinColumnSpacingProperty, value);
    }

    /// <summary>
    ///     Gets or sets the type of the selected item, or null when none is selected.
    /// </summary>
    /// <remarks>
    ///     Clicking an item and the arrow keys change it. A type that is not shown (for example because
    ///     of <see cref="Filter"/>) is kept, but no item is highlighted for it.
    /// </remarks>
    public NodeType? SelectedNodeType
    {
        get => GetValue(SelectedNodeTypeProperty);
        set => SetValue(SelectedNodeTypeProperty, value);
    }

    /// <summary>
    ///     Gets the node types currently shown, in display order (after filtering and grouping).
    /// </summary>
    public IReadOnlyList<NodeType> VisibleTypes
    {
        get
        {
            EnsureItems();
            return _items.ConvertAll(item => item.NodeType);
        }
    }

    /// <summary>
    ///     Gets the items currently shown, in display order.
    /// </summary>
    internal IReadOnlyList<NodePaletteItem> Items
    {
        get
        {
            EnsureItems();
            return _items;
        }
    }

    /// <summary>
    ///     Gets the header texts currently shown, in display order.
    /// </summary>
    internal IReadOnlyList<string> Headers
    {
        get
        {
            EnsureItems();
            return [.. _stack.Children.OfType<Border>().Select(border => ((TextBlock)border.Child!).Text ?? string.Empty)];
        }
    }

    /// <summary>
    ///     Gets the number of columns the items are laid out in as of the last layout (one when nothing is shown).
    /// </summary>
    internal int ColumnCount => _stack.ColumnCount;

    /// <summary>
    ///     Gets how many prototypes have been built so far; lets tests prove that item scenes are cached.
    /// </summary>
    internal int PrototypeBuildCount => _prototypes.BuildCount;

    /// <summary>
    ///     Gets or sets the function that starts a drag from an item; the editor's own starter unless a test replaces it.
    /// </summary>
    internal Func<PointerPressedEventArgs, string, Task> DragStarter { get; set; } =
        async (press, key) => await DiagramEditor.StartNodeTypeDragAsync(press, key).ConfigureAwait(true);

    /// <summary>
    ///     Gets the theme in effect: the explicit <see cref="DiagramTheme"/>, else the followed editor's, else the classic theme.
    /// </summary>
    internal DiagramTheme EffectiveTheme => DiagramTheme ?? Editor?.EffectiveTheme ?? Scene.DiagramTheme.Classic;

    /// <summary>
    ///     Gets the node types in effect: the explicit <see cref="Types"/>, else the registry of the followed editor's diagram, else none.
    /// </summary>
    internal IEnumerable<NodeType>? EffectiveTypes => Types ?? Editor?.Page?.Diagram.Types;

    /// <summary>
    ///     Builds the AutomationId of a node type's palette item: the one rule hosts, tests and UI drivers share.
    /// </summary>
    /// <remarks>
    ///     The id is <c>NodePalette_</c> followed by the type's key with every character other than an
    ///     ASCII letter, digit or underscore replaced by an underscore, so the key
    ///     <c>demo.audio.Mixer</c> gives <c>NodePalette_demo_audio_Mixer</c>. A key that already contains
    ///     such punctuation can collide with another key that differs only in it; keys of one registry
    ///     seldom do. (The built-in filter box has the reserved id <see cref="FilterAutomationId"/>.)
    /// </remarks>
    /// <param name="type">The node type.</param>
    /// <returns>The AutomationId of the type's item.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="type"/> is null.</exception>
    public static string GetAutomationId(NodeType type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return GetAutomationId(type.Key);
    }

    /// <summary>
    ///     Builds the AutomationId of the palette item of the node type with a key; see <see cref="GetAutomationId(NodeType)"/> for the rule.
    /// </summary>
    /// <param name="typeKey">The node type key.</param>
    /// <returns>The AutomationId of the type's item.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="typeKey"/> is null.</exception>
    public static string GetAutomationId(string typeKey)
    {
        ArgumentNullException.ThrowIfNull(typeKey);
        return "NodePalette_" + string.Concat(typeKey.Select(c => char.IsAsciiLetterOrDigit(c) || c == '_' ? c : '_'));
    }

    /// <summary>
    ///     Rebuilds the list from <see cref="Types"/>; call it after types were added to or removed from a registry that is already shown.
    /// </summary>
    public void Refresh()
    {
        _prototypes.Clear();
        MarkItemsDirty();
    }

    /// <summary>
    ///     Draws the palette entries (icon, label and category headings) for the current scroll position.
    /// </summary>
    /// <param name="context">The drawing context supplied by Avalonia.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // The canvas color is the backdrop, so the nodes sit on what they will sit on in the editor
        context.FillRectangle(_painter.GetBrush(EffectiveTheme.CanvasBackground), new Rect(Bounds.Size));
    }

    /// <summary>
    ///     Starts following the <see cref="Editor"/> (if any) once the palette is shown, and rebuilds the entries from what the editor holds now. Protected override of the Avalonia hook.
    /// </summary>
    /// <param name="e">The event data supplied by Avalonia.</param>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _isAttached = true;

        // Whatever the editor did while the palette was away is picked up now
        UpdateEditorSubscription();
        if (Editor is not null)
        {
            MarkItemsDirty();
            InvalidateVisual();
        }
    }

    /// <summary>
    ///     Stops listening to the <see cref="Editor"/> when the palette leaves the visual tree, so a discarded palette is not kept alive by it. Protected override of the Avalonia hook.
    /// </summary>
    /// <param name="e">The event data supplied by Avalonia.</param>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _isAttached = false;
        UpdateEditorSubscription();
    }

    /// <summary>
    ///     Reacts to a changed palette property (<see cref="Types"/>, <see cref="Editor"/>, <see cref="Filter"/>, <see cref="DiagramTheme"/>, <see cref="Zoom"/>,
    ///     <see cref="GroupByCategory"/>, <see cref="MaxColumns"/> and so on) by rebuilding or re-laying out the entries at the next layout. Protected override of the Avalonia hook.
    /// </summary>
    /// <param name="change">The change description supplied by Avalonia.</param>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        ArgumentNullException.ThrowIfNull(change);

        // Anything that changes what is listed or how it is drawn rebuilds the entries, once, at the next layout
        if (change.Property == TypesProperty)
        {
            _prototypes.Clear();
            MarkItemsDirty();
        }
        else if (change.Property == EditorProperty)
        {
            UpdateEditorSubscription();
            MarkItemsDirty();
            InvalidateVisual();
        }
        else if (change.Property == FilterProperty)
        {
            // Keep the search box in step with a filter that was set from code or a binding
            if (!string.Equals(_filterBox.Text ?? string.Empty, Filter ?? string.Empty, StringComparison.Ordinal))
            {
                _filterBox.Text = Filter;
            }

            MarkItemsDirty();
            InvalidateVisual();
        }
        else if (change.Property == ShowFilterProperty)
        {
            _filterBox.IsVisible = ShowFilter;
        }
        else if (change.Property == DiagramThemeProperty || change.Property == ZoomProperty
            || change.Property == GroupByCategoryProperty || change.Property == UncategorizedHeaderProperty)
        {
            MarkItemsDirty();
            InvalidateVisual();
        }
        else if (change.Property == MaxColumnsProperty)
        {
            _stack.MaxColumns = MaxColumns;
        }
        else if (change.Property == MinColumnSpacingProperty)
        {
            _stack.MinColumnSpacing = MinColumnSpacing;
        }
        else if (change.Property == SelectedNodeTypeProperty)
        {
            UpdateSelectionVisuals();
        }
    }

    /// <summary>
    ///     Measures the search box and the entries; like a list box it takes the room offered, or the size of its content when the parent offers none. Protected override of the Avalonia hook.
    /// </summary>
    /// <param name="availableSize">The space the parent offers.</param>
    /// <returns>The size the palette wants.</returns>
    protected override AvaloniaSize MeasureOverride(AvaloniaSize availableSize)
    {
        EnsureItems();

        // The scroll viewer offers its content unlimited width, so the panel is told the viewport's width
        // (the palette's width less the panel's margins) to decide how many columns fit
        _stack.AvailableWidth = double.IsInfinity(availableSize.Width) ? double.NaN : Math.Max(0, availableSize.Width - (2 * Spacing));
        _layout.Measure(availableSize);

        // Like a list box: take the room offered, or the content's size when the parent offers none
        return new AvaloniaSize(
            double.IsInfinity(availableSize.Width) ? _layout.DesiredSize.Width : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? _layout.DesiredSize.Height : availableSize.Height);
    }

    /// <summary>
    ///     Arranges the search box and the entries to fill the palette. Protected override of the Avalonia hook.
    /// </summary>
    /// <param name="finalSize">The space the parent assigned.</param>
    /// <returns>The size used, which is <paramref name="finalSize"/>.</returns>
    protected override AvaloniaSize ArrangeOverride(AvaloniaSize finalSize)
    {
        _layout.Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <summary>
    ///     Creates the UI Automation peer that exposes the palette as a list. Protected override of the Avalonia hook.
    /// </summary>
    /// <returns>The automation peer.</returns>
    protected override AutomationPeer OnCreateAutomationPeer() => new NodePaletteAutomationPeer(this);

    /// <summary>
    ///     Keeps <see cref="Zoom"/> inside the editor's zoom range, so a binding to the editor's zoom is always valid.
    /// </summary>
    /// <param name="sender">The palette.</param>
    /// <param name="value">The requested zoom.</param>
    /// <returns>The zoom to use.</returns>
    private static double CoerceZoom(AvaloniaObject sender, double value) =>
        double.IsNaN(value) ? 1 : Math.Clamp(value, DiagramEditor.MinZoom, DiagramEditor.MaxZoom);

    /// <summary>
    ///     Keeps <see cref="MaxColumns"/> from going negative.
    /// </summary>
    /// <param name="sender">The palette.</param>
    /// <param name="value">The requested maximum.</param>
    /// <returns>The maximum to use; zero means no limit.</returns>
    private static int CoerceMaxColumns(AvaloniaObject sender, int value) => Math.Max(0, value);

    /// <summary>
    ///     Keeps <see cref="MinColumnSpacing"/> a finite, non-negative number.
    /// </summary>
    /// <param name="sender">The palette.</param>
    /// <param name="value">The requested spacing.</param>
    /// <returns>The spacing to use.</returns>
    private static double CoerceMinColumnSpacing(AvaloniaObject sender, double value) =>
        double.IsNaN(value) || double.IsInfinity(value) ? 4 : Math.Max(0, value);

    /// <summary>
    ///     Gets the node types in effect as a registry that holds all of them.
    /// </summary>
    /// <returns>The given registry, or a private one built from the sequence; null when there are no types.</returns>
    private NodeTypeRegistry? ResolveRegistry()
    {
        var types = EffectiveTypes;
        switch (types)
        {
            case null:
                return null;
            case NodeTypeRegistry registry:
                return registry;
        }

        // A plain sequence has no port types: give each used key a neutral color so scene building can resolve them
        var neutral = Geometry.Color.Parse("#9A9A9A");
        var created = new NodeTypeRegistry();
        foreach (var type in types)
        {
            foreach (var port in type.Inputs.Concat(type.Outputs).Where(port => !created.PortTypes.Contains(port.TypeKey)))
            {
                created.PortTypes.Add(new PortType(port.TypeKey, neutral));
            }

            if (!created.Contains(type.Key))
            {
                created.Add(type);
            }
        }

        return created;
    }
}

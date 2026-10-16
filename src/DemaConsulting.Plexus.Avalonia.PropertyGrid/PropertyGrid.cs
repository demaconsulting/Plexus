using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid;

/// <summary>
///     A generic, annotation-driven property grid: shows and edits the properties of any object (or of a custom
///     <see cref="IPropertySource"/>) in the familiar two-column layout of the WinForms <c>PropertyGrid</c>.
/// </summary>
/// <remarks>
///     <para>
///         <b>Content.</b> Set <see cref="SelectedObject"/> to show an object through
///         <see cref="PropertySourceFactory"/> (annotated objects use <see cref="ReflectionPropertySource"/>), or
///         <see cref="Source"/> to supply the items yourself. Setting one clears the other. The grid honors the
///         <c>System.ComponentModel</c> annotations the property model reads: category, description, display name,
///         read-only, browsable, default value (changed values are shown in bold), expandable objects, enum and
///         type-converter standard values, password text and validation attributes.
///     </para>
///     <para>
///         <b>Editing.</b> Every edit goes through <see cref="Session"/> (a <see cref="PropertyEditSession"/>).
///         <see cref="EditMode"/> chooses <see cref="PropertyEditMode.Live"/> (the default: each edit is written at once,
///         as in a docked properties panel) or <see cref="PropertyEditMode.Staged"/> (edits are buffered until the host
///         calls <c>Session.Commit()</c> or <c>Session.Cancel()</c>, as in a dialog). <see cref="CommitScope"/> lets the
///         host wrap writes in its undo transaction. Invalid input is shown inline (red border, the reason as a tooltip)
///         and stays until it is fixed or cancelled with Escape.
///     </para>
///     <para>
///         <b>Layout.</b> An optional toolbar (categorized / alphabetical and a search box), the property rows, and an
///         optional description pane. The column splitter and the description pane splitter can be dragged. Category rows
///         collapse; the grid remembers which categories are collapsed and which nested objects are expanded.
///     </para>
///     <para>
///         <b>Keyboard.</b> Up / Down move the selection, Left / Right collapse or expand (or move to the parent / first
///         child), Enter or F2 edits the selected value, Escape abandons an edit in progress, Tab moves between the name
///         and the value of a row.
///     </para>
///     <para>
///         <b>Editors.</b> A custom editor is a factory, <c>Func&lt;PropertyEditorContext, Control&gt;</c>: it receives the row's
///         <see cref="PropertyEditorContext"/> (the item, its value, and the writes that go through the session) and returns the control
///         shown in the value column. <see cref="EditorRegistry"/> maps editor keys (a <see cref="PropertyEditorAttribute"/> or <c>[UIHint]</c> on the
///         property) and property types to such factories; the defaults edit text and any type with a text
///         converter, <see cref="bool"/>, enumerations (drop-down, or multi-select for <c>[Flags]</c>), converter standard
///         values, colors, nested objects and collections. There are two places to register your own, and the difference matters:
///         <b>once at application start-up on the shared <see cref="DefaultEditors"/> registry</b> (recommended: every grid, and every dialog
///         the package creates such as <see cref="PropertyGridDialog"/> and <c>NodePropertiesDialog</c>, uses it), or <b>on one grid's own
///         registry</b> (<c>grid.EditorRegistry = PropertyGrid.CreateEditorRegistry();</c> and then register on <c>grid.EditorRegistry</c>), which affects only that grid.
///         Registering on <c>grid.EditorRegistry</c> without assigning a new registry first changes the shared one for the whole application.
///         See <see cref="PropertyEditorRegistry{TEditor}"/> for the lookup rules and <see cref="PropertyEditorContext"/> for a complete editor.
///     </para>
///     <para>
///         <b>Threading and theming.</b> Use the grid from the UI thread. It uses the Fluent theme resources, so the application must include
///         <c>&lt;FluentTheme /&gt;</c> in App.axaml; it works in light and dark variants. Rows expose the automation ids <c>PropertyGrid_Row_{path}</c>
///         and <c>PropertyGrid_Editor_{path}</c>, where <c>{path}</c> is <see cref="PropertyItem.Path"/>. The grid derives from Avalonia's <c>UserControl</c>, so the usual control members (<c>Bounds</c>, <c>IsVisible</c>, <c>Width</c>, <c>Focus()</c>, ...) apply; see Inherited Members. <c>Content</c> is managed by the grid and must not be set.
///     </para>
/// </remarks>
/// <example>
///     In XAML the grid is an ordinary control. The grid is not covered by the <c>plexus</c> XML namespace of the editor package, so map its CLR namespace
///     (MixerWindow.axaml; the application's App.axaml needs <c>&lt;FluentTheme /&gt;</c>):
///     <code lang="xml"><![CDATA[
///     <Window xmlns="https://github.com/avaloniaui"
///             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
///             xmlns:pg="clr-namespace:DemaConsulting.Plexus.Avalonia.PropertyGrid;assembly=DemaConsulting.Plexus.Avalonia.PropertyGrid"
///             x:Class="MyApp.MixerWindow"
///             Title="Mixer" Width="420" Height="560">
///         <pg:PropertyGrid x:Name="MixerGrid" EditMode="Live" SortMode="Categorized" ShowDescription="True" />
///     </Window>
///     ]]></code>
///     The object to show is any annotated .NET object, set from code (MixerWindow.axaml.cs); the grid honors the <c>System.ComponentModel</c> annotations
///     and writes edits straight back (live mode):
///     <code><![CDATA[
///     using System.ComponentModel;
///     using Avalonia.Controls;
///     using DemaConsulting.Plexus.Avalonia.PropertyGrid;
///
///     namespace MyApp;
///
///     public class Mixer
///     {
///         [Category("Levels"), Description("Output level in dB."), DefaultValue(0.0)]
///         public double Gain { get; set; }
///
///         [Category("Levels")]
///         public bool Muted { get; set; }
///
///         [Browsable(false)]
///         public string Id { get; set; } = "";
///     }
///
///     public partial class MixerWindow : Window
///     {
///         public MixerWindow()
///         {
///             InitializeComponent();                       // generated from the XAML: MixerGrid exists now
///             MixerGrid.SelectedObject = new Mixer();      // category bands, bold changed values, description pane, search
///         }
///     }
///     ]]></code>
///     The same grid created from code, without XAML:
///     <code><![CDATA[
///     using System.ComponentModel;
///     using Avalonia.Controls;
///     using DemaConsulting.Plexus.Avalonia.PropertyGrid;
///
///     var grid = new PropertyGrid
///     {
///         SelectedObject = new Mixer(),
///         EditMode = PropertyEditMode.Live,
///         SortMode = PropertySortMode.Categorized,
///         ShowDescription = true,
///     };
///     var window = new Window { Content = grid, Width = 420, Height = 560 };
///
///     public class Mixer
///     {
///         [Category("Levels"), Description("Output level in dB."), DefaultValue(0.0)]
///         public double Gain { get; set; }
///     }
///     ]]></code>
/// </example>
public partial class PropertyGrid : UserControl
{
    /// <summary>
    ///     Identifies the <see cref="SelectedObject"/> property.
    /// </summary>
    public static readonly StyledProperty<object?> SelectedObjectProperty =
        AvaloniaProperty.Register<PropertyGrid, object?>(nameof(SelectedObject));

    /// <summary>
    ///     Identifies the <see cref="Source"/> property.
    /// </summary>
    public static readonly StyledProperty<IPropertySource?> SourceProperty =
        AvaloniaProperty.Register<PropertyGrid, IPropertySource?>(nameof(Source));

    /// <summary>
    ///     Identifies the <see cref="EditMode"/> property.
    /// </summary>
    public static readonly StyledProperty<PropertyEditMode> EditModeProperty =
        AvaloniaProperty.Register<PropertyGrid, PropertyEditMode>(nameof(EditMode), PropertyEditMode.Live);

    /// <summary>
    ///     Identifies the <see cref="SortMode"/> property.
    /// </summary>
    public static readonly StyledProperty<PropertySortMode> SortModeProperty =
        AvaloniaProperty.Register<PropertyGrid, PropertySortMode>(nameof(SortMode), PropertySortMode.Categorized);

    /// <summary>
    ///     Identifies the <see cref="ShowToolbar"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> ShowToolbarProperty =
        AvaloniaProperty.Register<PropertyGrid, bool>(nameof(ShowToolbar), true);

    /// <summary>
    ///     Identifies the <see cref="ShowSearch"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> ShowSearchProperty =
        AvaloniaProperty.Register<PropertyGrid, bool>(nameof(ShowSearch), true);

    /// <summary>
    ///     Identifies the <see cref="ShowDescription"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> ShowDescriptionProperty =
        AvaloniaProperty.Register<PropertyGrid, bool>(nameof(ShowDescription), true);

    /// <summary>
    ///     Identifies the <see cref="Filter"/> property.
    /// </summary>
    public static readonly StyledProperty<string?> FilterProperty =
        AvaloniaProperty.Register<PropertyGrid, string?>(nameof(Filter));

    /// <summary>
    ///     Identifies the <see cref="NameColumnWidth"/> property.
    /// </summary>
    public static readonly StyledProperty<double> NameColumnWidthProperty =
        AvaloniaProperty.Register<PropertyGrid, double>(nameof(NameColumnWidth), 150);

    /// <summary>
    ///     Identifies the <see cref="DescriptionHeight"/> property.
    /// </summary>
    public static readonly StyledProperty<double> DescriptionHeightProperty =
        AvaloniaProperty.Register<PropertyGrid, double>(nameof(DescriptionHeight), 64);

    /// <summary>
    ///     Identifies the <see cref="Session"/> property.
    /// </summary>
    public static readonly DirectProperty<PropertyGrid, PropertyEditSession?> SessionProperty =
        AvaloniaProperty.RegisterDirect<PropertyGrid, PropertyEditSession?>(nameof(Session), o => o.Session);

    /// <summary>
    ///     Identifies the <see cref="SelectedItem"/> property.
    /// </summary>
    public static readonly DirectProperty<PropertyGrid, PropertyItem?> SelectedItemProperty =
        AvaloniaProperty.RegisterDirect<PropertyGrid, PropertyItem?>(nameof(SelectedItem), o => o.SelectedItem, (o, v) => o.SelectItem(v));

    /// <summary>
    ///     The editors every grid uses unless it is given its own registry.
    /// </summary>
    private static readonly PropertyEditorRegistry<PropertyEditorFactory> SharedEditors = BuiltInEditors.CreateRegistry();

    /// <summary>
    ///     The edit session of the current source.
    /// </summary>
    private PropertyEditSession? _session;

    /// <summary>
    ///     The item of the selected row.
    /// </summary>
    private PropertyItem? _selection;

    /// <summary>
    ///     The editors this grid uses.
    /// </summary>
    private PropertyEditorRegistry<PropertyEditorFactory> _editors = SharedEditors;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PropertyGrid"/> class.
    /// </summary>
    public PropertyGrid()
    {
        Focusable = true;
        FocusAdorner = null;
        BuildLayout();
        AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel);
        UpdateChrome();
        UpdateToolbarState();
    }

    /// <summary>
    ///     Occurs when the grid is about to open its collection editor; a handler can show its own editor and set
    ///     <see cref="CollectionEditorRequestedEventArgs.Handled"/>.
    /// </summary>
    public event EventHandler<CollectionEditorRequestedEventArgs>? CollectionEditorRequested;

    /// <summary>
    ///     Gets or sets the object whose properties are shown. The properties come from
    ///     <see cref="PropertySourceFactory.Create"/>. Setting it clears <see cref="Source"/>.
    /// </summary>
    /// <remarks>
    ///     When the factory creates a new source for the object (a <see cref="IDisposable"/> such as the Plexus node source), the grid owns it:
    ///     it disposes the source when <see cref="SelectedObject"/> or <see cref="Source"/> changes, and while the grid is not in the visual tree
    ///     (it creates the source again when the grid returns, keeping the expanded rows). Nothing needs to be disposed by the host.
    ///     An object that is itself an <see cref="IPropertySource"/> is shown as it is and never disposed by the grid.
    /// </remarks>
    public object? SelectedObject
    {
        get => GetValue(SelectedObjectProperty);
        set => SetValue(SelectedObjectProperty, value);
    }

    /// <summary>
    ///     Gets or sets a custom property source (see <see cref="CustomPropertySource"/>). The host owns the source and
    ///     disposes it: the grid never disposes a source assigned here, not when it is replaced and not when the grid is
    ///     removed. Setting it clears <see cref="SelectedObject"/>.
    /// </summary>
    public IPropertySource? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>
    ///     Gets or sets when edits are written: <see cref="PropertyEditMode.Live"/> (default) or
    ///     <see cref="PropertyEditMode.Staged"/>. Changing it starts a new <see cref="Session"/> (pending edits are lost).
    /// </summary>
    public PropertyEditMode EditMode
    {
        get => GetValue(EditModeProperty);
        set => SetValue(EditModeProperty, value);
    }

    /// <summary>
    ///     Gets or sets how the rows are ordered: <see cref="PropertySortMode.Categorized"/> (default) or
    ///     <see cref="PropertySortMode.Alphabetical"/>.
    /// </summary>
    public PropertySortMode SortMode
    {
        get => GetValue(SortModeProperty);
        set => SetValue(SortModeProperty, value);
    }

    /// <summary>
    ///     Gets or sets a value indicating whether the sort toolbar is shown (default <see langword="true"/>).
    /// </summary>
    public bool ShowToolbar
    {
        get => GetValue(ShowToolbarProperty);
        set => SetValue(ShowToolbarProperty, value);
    }

    /// <summary>
    ///     Gets or sets a value indicating whether the search box is shown (default <see langword="true"/>).
    /// </summary>
    public bool ShowSearch
    {
        get => GetValue(ShowSearchProperty);
        set => SetValue(ShowSearchProperty, value);
    }

    /// <summary>
    ///     Gets or sets a value indicating whether the description pane at the bottom is shown (default
    ///     <see langword="true"/>).
    /// </summary>
    public bool ShowDescription
    {
        get => GetValue(ShowDescriptionProperty);
        set => SetValue(ShowDescriptionProperty, value);
    }

    /// <summary>
    ///     Gets or sets the text that rows must contain (in the display name, name or category) to be shown. The search
    ///     box edits this property; it can also be set from code.
    /// </summary>
    public string? Filter
    {
        get => GetValue(FilterProperty);
        set => SetValue(FilterProperty, value);
    }

    /// <summary>
    ///     Gets or sets the width of the name column in pixels (default 150); the user changes it by dragging the
    ///     column splitter.
    /// </summary>
    public double NameColumnWidth
    {
        get => GetValue(NameColumnWidthProperty);
        set => SetValue(NameColumnWidthProperty, value);
    }

    /// <summary>
    ///     Gets or sets the initial height of the description pane in pixels (default 64); the user changes it by
    ///     dragging the splitter above the pane.
    /// </summary>
    public double DescriptionHeight
    {
        get => GetValue(DescriptionHeightProperty);
        set => SetValue(DescriptionHeightProperty, value);
    }

    /// <summary>
    ///     Gets the edit session of the shown source: use it to commit or cancel a staged edit
    ///     (<c>Session.Commit()</c>, <c>Session.Cancel()</c>, <c>Session.HasChanges</c>), or <see langword="null"/>
    ///     when nothing is shown. A new session is created when the content or <see cref="EditMode"/> changes.
    /// </summary>
    public PropertyEditSession? Session
    {
        get => _session;
        private set => SetAndRaise(SessionProperty, ref _session, value);
    }

    /// <summary>
    ///     Gets or sets the item of the selected row (<see langword="null"/> when a category or nothing is selected).
    /// </summary>
    public PropertyItem? SelectedItem
    {
        get => _selection;
        set => SelectItem(value);
    }

    /// <summary>
    ///     Gets or sets the host hook that runs the writes of the grid's session, for example inside an undo
    ///     transaction. It must invoke the action it receives exactly once (see <see cref="PropertyEditSession"/>).
    /// </summary>
    public Action<Action>? CommitScope { get; set; }

    /// <summary>
    ///     Gets or sets the hook that creates new collection elements (see
    ///     <see cref="PropertyEditSession.CollectionItemFactory"/>). <see langword="null"/> uses the element type's
    ///     default constructor.
    /// </summary>
    public Func<PropertyItem, object?>? CollectionItemFactory { get; set; }

    /// <summary>
    ///     Gets or sets the formatter that produces the text of a collection element in the collection editor.
    ///     <see langword="null"/> uses <c>ToString()</c> (falling back to the element's default property or type name).
    /// </summary>
    public Func<object?, string>? CollectionItemFormatter { get; set; }

    /// <summary>
    ///     Gets or sets the registry that chooses editors for this grid; the default is the shared registry (see <see cref="DefaultEditors"/>),
    ///     so registering on it without assigning a registry first changes every grid in the application.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Use it to customize <b>one grid</b>: assign <see cref="CreateEditorRegistry"/> (a private copy of the built-in editors) and register your editors on
    ///         that copy. To customize all grids, and the dialogs the package creates itself (<see cref="PropertyGridDialog"/>, <c>NodePropertiesDialog</c>),
    ///         register on <see cref="DefaultEditors"/> once at start-up instead. The registry holds factories of the type
    ///         <c>Func&lt;PropertyEditorContext, Control&gt;</c>.
    ///     </para>
    ///     <para>
    ///         A factory creates the control of the value column of one row. Read the value from
    ///         <see cref="PropertyEditorContext.Value"/>, write it with <see cref="PropertyEditorContext.SetValue"/> or
    ///         <see cref="PropertyEditorContext.SetText"/> (never straight to the item, so staged mode, validation, errors and
    ///         the host's undo scope all apply), and re-read the value when <see cref="PropertyEditorContext.Refreshed"/>
    ///         occurs (without writing it back, see the example). Resolution order: the key of a <see cref="PropertyEditorAttribute"/> (or a <c>[UIHint]</c> annotation),
    ///         then the property type, then the registry's fallback.
    ///     </para>
    /// </remarks>
    /// <example>
    ///     A slider for a double property of one grid only; other grids keep their editors. The key is chosen with <c>[PropertyEditor("slider")]</c>
    ///     (or <c>[UIHint("slider")]</c>) on the property.
    ///     <code><![CDATA[
    ///     using System.ComponentModel;
    ///     using Avalonia.Controls;
    ///     using DemaConsulting.Plexus.Avalonia.PropertyGrid;
    ///
    ///     var grid = new PropertyGrid { SelectedObject = new Mixer() };
    ///     grid.EditorRegistry = PropertyGrid.CreateEditorRegistry();          // a private copy: assigning it is what keeps the change local
    ///     grid.EditorRegistry.Register("slider", context =>
    ///     {
    ///         double Current() => Convert.ToDouble(context.Value ?? 0.0);
    ///         var slider = new Slider { Minimum = 0, Maximum = 100, Value = Current(), IsEnabled = !context.IsReadOnly };
    ///         slider.ValueChanged += (_, e) =>
    ///         {
    ///             // Showing a value (below) raises ValueChanged too, so write only what differs from the row's value: no feedback loop
    ///             if (e.NewValue != Current()) { context.SetValue(e.NewValue); }   // through the session: staged mode, validation and undo apply
    ///         };
    ///         context.Refreshed += (_, _) => slider.Value = Current();             // the value changed elsewhere (undo, another editor)
    ///         return slider;
    ///     });
    ///
    ///     public class Mixer
    ///     {
    ///         [PropertyEditor("slider"), DefaultValue(0.0)]
    ///         public double Gain { get; set; }
    ///     }
    ///     ]]></code>
    /// </example>
    public PropertyEditorRegistry<PropertyEditorFactory> EditorRegistry
    {
        get => _editors;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _editors = value;
            RebuildFromScratch();
        }
    }

    /// <summary>
    ///     Gets the shared registry of the built-in editors that every grid uses unless it was given its own registry (see <see cref="EditorRegistry"/>).
    /// </summary>
    /// <remarks>
    ///     This is the recommended place for your own editors: register them <b>once at application start-up</b> and every grid, and every dialog the package
    ///     creates (<see cref="PropertyGridDialog"/>, <c>NodePropertiesDialog</c>), uses them. A grid that was given another registry through
    ///     <see cref="EditorRegistry"/> ignores this one. The registry holds factories of the type <c>Func&lt;PropertyEditorContext, Control&gt;</c>.
    /// </remarks>
    /// <example>
    ///     Register a custom editor for the whole application, for example in <c>App.OnFrameworkInitializationCompleted</c>. The sample class stands in for any
    ///     annotated class: the key <c>"slider"</c> is chosen on the property with <c>[PropertyEditor("slider")]</c>.
    ///     <code><![CDATA[
    ///     using Avalonia.Controls;
    ///     using DemaConsulting.Plexus.Avalonia.PropertyGrid;
    ///
    ///     PropertyGrid.DefaultEditors.Register("slider", SliderEditor.Create);   // every grid and dialog now offers it
    ///
    ///     public static class SliderEditor
    ///     {
    ///         public static Control Create(PropertyEditorContext context)
    ///         {
    ///             double Current() => Convert.ToDouble(context.Value ?? 0.0);
    ///             var slider = new Slider { Minimum = 0, Maximum = 100, Value = Current(), IsEnabled = !context.IsReadOnly };
    ///             slider.ValueChanged += (_, e) =>
    ///             {
    ///                 // Showing a value (below) raises ValueChanged too, so write only what differs from the row's value: no feedback loop
    ///                 if (e.NewValue != Current()) { context.SetValue(e.NewValue); }
    ///             };
    ///             context.Refreshed += (_, _) => slider.Value = Current();   // the value changed elsewhere (undo, another editor)
    ///             return slider;
    ///         }
    ///     }
    ///     ]]></code>
    /// </example>
    public static PropertyEditorRegistry<PropertyEditorFactory> DefaultEditors => SharedEditors;

    /// <summary>
    ///     Creates a new registry that holds the built-in editors (text, bool, enum and flags, standard values, color,
    ///     collection fallback), ready to be extended and assigned to <see cref="EditorRegistry"/>.
    /// </summary>
    /// <remarks>
    ///     The copy is independent of <see cref="DefaultEditors"/>: editors registered on it, and later changes to the shared registry, do not
    ///     affect each other. See <see cref="EditorRegistry"/> for an example.
    /// </remarks>
    /// <returns>The new registry.</returns>
    public static PropertyEditorRegistry<PropertyEditorFactory> CreateEditorRegistry() => BuiltInEditors.CreateRegistry();

    /// <summary>
    ///     Opens the collection editor for a collection property of the shown source: a
    ///     <see cref="CollectionEditorDialog"/> over the grid's session, unless a
    ///     <see cref="CollectionEditorRequested"/> handler takes over.
    /// </summary>
    /// <param name="item">The collection property (<see cref="PropertyItem.IsCollection"/>).</param>
    /// <returns>
    ///     A task that completes with <see langword="true"/> when the user applied the edit (OK) and
    ///     <see langword="false"/> when it was cancelled or handled by a host handler.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The grid has no source.</exception>
    public async Task<bool> EditCollectionAsync(PropertyItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var session = Session ?? throw new InvalidOperationException("The grid has no source.");
        var request = new CollectionEditorRequestedEventArgs(session, item);
        CollectionEditorRequested?.Invoke(this, request);
        if (request.Handled)
        {
            return false;
        }

        var dialog = new CollectionEditorDialog(session, item, CollectionItemFormatter);
        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            return await dialog.ShowDialog<bool>(owner);
        }

        dialog.Show();
        return false;
    }

    /// <summary>
    ///     Opens the collection editor without awaiting it (used by the "…" button).
    /// </summary>
    /// <param name="item">The collection property.</param>
    internal void EditCollection(PropertyItem item) => _ = EditCollectionAsync(item);

    /// <summary>
    ///     Gets a value indicating whether a category is expanded.
    /// </summary>
    /// <param name="category">The category name.</param>
    /// <returns><see langword="true"/> unless the category has been collapsed.</returns>
    public bool IsCategoryExpanded(string category) => !_collapsedCategories.Contains(category);

    /// <summary>
    ///     Expands or collapses a category; the state is remembered across content changes.
    /// </summary>
    /// <param name="category">The category name.</param>
    /// <param name="expanded">Whether the category should be expanded.</param>
    public void SetCategoryExpanded(string category, bool expanded)
    {
        ArgumentNullException.ThrowIfNull(category);
        if (expanded ? _collapsedCategories.Remove(category) : _collapsedCategories.Add(category))
        {
            RefreshRows();
        }
    }

    /// <summary>
    ///     Gets a value indicating whether a nested object is expanded.
    /// </summary>
    /// <param name="path">The item path (see <see cref="PropertyItem.Path"/>).</param>
    /// <returns><see langword="true"/> when the object shows its properties.</returns>
    public bool IsExpanded(string path) => _expandedPaths.Contains(path);

    /// <summary>
    ///     Expands or collapses a nested object.
    /// </summary>
    /// <param name="path">The item path.</param>
    /// <param name="expanded">Whether the object should show its properties.</param>
    public void SetExpanded(string path, bool expanded)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (expanded ? _expandedPaths.Add(path) : _expandedPaths.Remove(path))
        {
            RefreshRows();
        }
    }

    /// <summary>
    ///     Selects the row of an item.
    /// </summary>
    /// <param name="item">The item, or <see langword="null"/> to clear the selection.</param>
    public void SelectItem(PropertyItem? item) => SelectKey(item is null ? null : PropertyKey(item.Path), false);

    /// <summary>
    ///     Re-reads the source's items and values and redraws the rows (call after changing the object outside the
    ///     grid when it does not raise change notifications).
    /// </summary>
    public void Refresh() => RefreshRows();

    /// <summary>
    ///     Gets the key of a category row.
    /// </summary>
    /// <param name="category">The category name.</param>
    /// <returns>The key.</returns>
    internal static string CategoryKey(string category) => "c:" + category;

    /// <summary>
    ///     Gets the key of a property row.
    /// </summary>
    /// <param name="path">The item path.</param>
    /// <returns>The key.</returns>
    internal static string PropertyKey(string path) => "p:" + path;

    /// <summary>
    ///     Gets the state of the row's changed (bold) marker, taking pending staged edits into account.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns><see langword="true"/> when the shown value differs from the default.</returns>
    internal bool IsChanged(PropertyItem item)
    {
        if (Session is { } session && item.HasDefault && item.IsDefaultValueKnown && session.IsPending(item) && !item.IsCollection)
        {
            return !Equals(session.GetValue(item), item.DefaultValue);
        }

        return item.IsChanged;
    }

    /// <summary>
    ///     Creates the editor control of a row: an editor registered for its key, the collection editor, a read-only
    ///     text, or the editor the registry chooses for its type.
    /// </summary>
    /// <param name="context">The row context.</param>
    /// <returns>The editor control.</returns>
    internal Control CreateEditor(PropertyEditorContext context)
    {
        var item = context.Item;
        if (item.EditorKey is { } key && _editors.Resolve(key) is { } keyed)
        {
            return keyed(context);
        }

        if (item.IsCollection)
        {
            return BuiltInEditors.CollectionEditorButton(context);
        }

        if (item.IsReadOnly || (item.IsExpandable && !item.SupportsTextEditing && !item.HasStandardValues))
        {
            return BuiltInEditors.ReadOnlyEditor(context);
        }

        var factory = _editors.Resolve(item.PropertyType) ?? _editors.Fallback ?? BuiltInEditors.TextOrChoiceEditor;
        return factory(context);
    }

    /// <summary>
    ///     Moves keyboard focus to the first focusable element of an editor.
    /// </summary>
    /// <param name="root">The editor control.</param>
    /// <returns><see langword="true"/> when an element took the focus.</returns>
    internal static bool FocusFirst(Control root)
    {
        if (root.Focusable && root.IsEffectivelyEnabled)
        {
            return root.Focus();
        }

        foreach (var descendant in global::Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(root))
        {
            if (descendant is InputElement { Focusable: true, IsEffectivelyEnabled: true, IsEffectivelyVisible: true } element)
            {
                return element.Focus();
            }
        }

        return false;
    }

    /// <summary>
    ///     Reacts to a change of a grid property: a new <see cref="SelectedObject"/> or <see cref="Source"/> rebuilds the rows, <see cref="EditMode"/> starts a new session, and the sort, filter and chrome settings redraw.
    /// </summary>
    /// <remarks>
    ///     Protected override of the Avalonia hook; call the base implementation when you derive.
    /// </remarks>
    /// <param name="change">The change that occurred.</param>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedObjectProperty)
        {
            OnContentChanged(fromSource: false);
        }
        else if (change.Property == SourceProperty)
        {
            OnContentChanged(fromSource: true);
        }
        else if (change.Property == EditModeProperty)
        {
            RecreateSession();
        }
        else if (change.Property == SortModeProperty)
        {
            UpdateToolbarState();
            RefreshRows();
        }
        else if (change.Property == FilterProperty)
        {
            OnFilterChanged();
        }
        else if (change.Property == NameColumnWidthProperty)
        {
            ApplyNameWidth();
        }
        else if (change.Property == DescriptionHeightProperty)
        {
            UpdateChrome();
        }
        else if (change.Property == ShowToolbarProperty || change.Property == ShowSearchProperty || change.Property == ShowDescriptionProperty)
        {
            UpdateChrome();
        }
    }

    /// <summary>
    ///     Creates the source of an object the grid owns again when the grid returns to the visual tree, keeping the expanded rows.
    /// </summary>
    /// <remarks>
    ///     Protected override of the Avalonia hook; call the base implementation when you derive.
    /// </remarks>
    /// <param name="e">The attachment details.</param>
    protected override void OnAttachedToVisualTree(global::Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ReacquireOwnedSource();
    }

    /// <summary>
    ///     Releases (and disposes) the source the grid created for <see cref="SelectedObject"/>, so a grid that is not shown does not stay subscribed to the object.
    /// </summary>
    /// <remarks>
    ///     Protected override of the Avalonia hook; call the base implementation when you derive.
    /// </remarks>
    /// <param name="e">The detachment details.</param>
    protected override void OnDetachedFromVisualTree(global::Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ReleaseOwnedSource();
    }
}

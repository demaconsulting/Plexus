using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using DemaConsulting.Plexus.Avalonia.PropertyGrid;

namespace DemaConsulting.Plexus.Demo.Gui;

/// <content>
///     The optional properties panel docked on the right of the window: a live property grid that shows the selected
///     node, annotation or group, so every edit is applied (and undoable) as it is made.
/// </content>
/// <remarks>
///     The panel is the second way the demo shows the library's property grid: the double-click dialog is staged (OK or
///     Cancel), the panel is live (each edit is one undo step). The whole integration is one assignment,
///     <c>PropertiesGrid.SelectedObject = element</c>: the library picks the Plexus adapter for the element.
/// </remarks>
internal sealed partial class MainWindow
{
    /// <summary>
    ///     The initial width of the properties panel, in device-independent pixels.
    /// </summary>
    internal const double PropertiesPanelWidth = 320;

    /// <summary>
    ///     The narrowest the properties panel can be dragged, in device-independent pixels.
    /// </summary>
    internal const double MinPropertiesPanelWidth = 240;

    /// <summary>
    ///     The pane that holds the heading and the grid.
    /// </summary>
    private Border _propertiesPane = null!;

    /// <summary>
    ///     The heading of the panel, which names the kind of the shown element.
    /// </summary>
    private readonly TextBlock _propertiesHeading = new() { FontWeight = FontWeight.SemiBold, Margin = new Thickness(8, 6) };

    /// <summary>
    ///     The width the panel had when it was last visible.
    /// </summary>
    private double _propertiesWidth = PropertiesPanelWidth;

    /// <summary>
    ///     Gets the grid of the properties panel (live editing).
    /// </summary>
    public PropertyGrid PropertiesGrid { get; } = new() { EditMode = PropertyEditMode.Live };

    /// <summary>
    ///     Gets the column of the window's body that holds the properties panel; its width is what dragging <see cref="PropertiesPanelSplitter"/> changes.
    /// </summary>
    public ColumnDefinition PropertiesPanelColumn { get; } = new(0, GridUnitType.Pixel);

    /// <summary>
    ///     Gets the splitter between the editor and the properties panel.
    /// </summary>
    public GridSplitter PropertiesPanelSplitter { get; private set; } = null!;

    /// <summary>
    ///     Gets or sets a value indicating whether the properties panel is shown (View &gt; Properties Panel).
    /// </summary>
    public bool IsPropertiesPanelVisible
    {
        get => _propertiesPane.IsVisible;
        set
        {
            if (value == _propertiesPane.IsVisible)
            {
                return;
            }

            if (!value && PropertiesPanelColumn.ActualWidth > 0)
            {
                _propertiesWidth = PropertiesPanelColumn.ActualWidth;
            }

            _propertiesPane.IsVisible = value;
            PropertiesPanelSplitter.IsVisible = value;
            PropertiesPanelColumn.MinWidth = value ? MinPropertiesPanelWidth : 0;
            PropertiesPanelColumn.Width = new GridLength(value ? _propertiesWidth : 0, GridUnitType.Pixel);
            _showPropertiesPanel.IsChecked = value;
            UpdatePropertiesPanel();
        }
    }

    /// <summary>
    ///     Adds the splitter and the (initially hidden) properties panel to the window body.
    /// </summary>
    /// <param name="body">The body grid, which already holds the palette, its splitter and the editor pane.</param>
    private void AddPropertiesPanel(Grid body)
    {
        body.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        body.ColumnDefinitions.Add(PropertiesPanelColumn);

        PropertiesPanelSplitter = new GridSplitter
        {
            ResizeDirection = GridResizeDirection.Columns,
            Width = 5,
            Background = new SolidColorBrush(Colors.Gray, 0.35),
            ResizeBehavior = GridResizeBehavior.PreviousAndNext,
            IsVisible = false,
        }.WithId(AutomationIds.PropertiesPanelSplitter);
        Grid.SetColumn(PropertiesPanelSplitter, 3);
        body.Children.Add(PropertiesPanelSplitter);

        _propertiesHeading.Text = HeadingFor(null);
        PropertiesGrid.Margin = new Thickness(4, 0, 4, 4);
        _propertiesPane = new Border
        {
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1, 0, 0, 0),
            IsVisible = false,
            Child = new DockPanel
            {
                Children =
                {
                    _propertiesHeading.DockTop(),
                    PropertiesGrid,
                },
            },
        }.WithId(AutomationIds.PropertiesPanel);
        Grid.SetColumn(_propertiesPane, 4);
        body.Children.Add(_propertiesPane);
    }

    /// <summary>
    ///     Shows the selected node, annotation or group in the panel (nothing for no selection or several).
    /// </summary>
    private void UpdatePropertiesPanel()
    {
        // A hidden panel shows nothing, so it never listens to an element
        object? target = IsPropertiesPanelVisible && Editor.Selection is [{ Page.IsAttached: true } element and (Node or Annotation or Group)]
            ? element
            : null;
        if (!ReferenceEquals(PropertiesGrid.SelectedObject, target))
        {
            PropertiesGrid.SelectedObject = target;
        }

        _propertiesHeading.Text = HeadingFor(target);
    }

    /// <summary>
    ///     Gets the heading of the panel for a shown element.
    /// </summary>
    /// <param name="target">The element, or <see langword="null"/> when nothing is shown.</param>
    /// <returns>The heading.</returns>
    private static string HeadingFor(object? target) => target switch
    {
        Node node => $"Properties - {node.Type.DisplayName}",
        Annotation => "Properties - Annotation",
        Group => "Properties - Group",
        _ => "Properties",
    };
}

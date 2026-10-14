using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaGeometry = Avalonia.Media.Geometry;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid;

/// <content>
///     The visual structure of the grid: toolbar, rows area with the column splitter, and the description pane.
/// </content>
public partial class PropertyGrid
{
    /// <summary>
    ///     The path data of the categorized icon.
    /// </summary>
    private const string CategorizedIcon = "M1,1.5 H15 V4 H1 Z M5,6 H15 V7.5 H5 Z M5,9 H15 V10.5 H5 Z M1,12 H15 V14.5 H1 Z";

    /// <summary>
    ///     The path data of the alphabetical icon ("A", "Z" and a down arrow).
    /// </summary>
    private const string AlphabeticalIcon =
        "M1,8 L3.6,1.5 H4.9 L7.5,8 H6.1 L5.6,6.6 H2.9 L2.4,8 Z M3.3,5.5 H5.2 L4.25,2.9 Z " +
        "M1.5,9.5 H7.5 V10.8 L3.4,14.7 H7.5 V16 H1.5 V14.7 L5.6,10.8 H1.5 Z " +
        "M11.6,1.5 H13 V10.8 L15,8.8 L16,9.8 L12.3,13.7 L8.6,9.8 L9.6,8.8 L11.6,10.8 Z";

    /// <summary>
    ///     The collapsed categories (the state is remembered across content changes).
    /// </summary>
    private readonly HashSet<string> _collapsedCategories = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     The expanded nested objects, by item path.
    /// </summary>
    private readonly HashSet<string> _expandedPaths = new(StringComparer.Ordinal);

    /// <summary>
    ///     The root layout.
    /// </summary>
    private readonly Grid _root = new();

    /// <summary>
    ///     The toolbar container.
    /// </summary>
    private readonly Border _toolbar = new();

    /// <summary>
    ///     The categorized toggle.
    /// </summary>
    private readonly ToggleButton _categorizedButton = new();

    /// <summary>
    ///     The alphabetical toggle.
    /// </summary>
    private readonly ToggleButton _alphabeticalButton = new();

    /// <summary>
    ///     The search box.
    /// </summary>
    private readonly TextBox _searchBox = new();

    /// <summary>
    ///     The panel that holds the rows.
    /// </summary>
    private readonly StackPanel _rowsPanel = new() { Orientation = Orientation.Vertical };

    /// <summary>
    ///     The scroll viewer of the rows.
    /// </summary>
    private readonly ScrollViewer _scroll = new();

    /// <summary>
    ///     The thumb that drags the column splitter.
    /// </summary>
    private readonly Thumb _columnSplitter = new();

    /// <summary>
    ///     The message shown when there are no rows.
    /// </summary>
    private readonly TextBlock _emptyText = new();

    /// <summary>
    ///     The splitter above the description pane.
    /// </summary>
    private readonly GridSplitter _descriptionSplitter = new();

    /// <summary>
    ///     The description pane.
    /// </summary>
    private readonly Border _descriptionPane = new();

    /// <summary>
    ///     The name shown in the description pane.
    /// </summary>
    private readonly TextBlock _descriptionName = new();

    /// <summary>
    ///     The description text shown in the description pane.
    /// </summary>
    private readonly TextBlock _descriptionText = new();

    /// <summary>
    ///     Whether the search box and the <see cref="Filter"/> property are being synchronized.
    /// </summary>
    private bool _syncingFilter;

    /// <summary>
    ///     Builds the visual tree.
    /// </summary>
    private void BuildLayout()
    {
        _root.RowDefinitions = new RowDefinitions { new(GridLength.Auto), new(1, GridUnitType.Star) { }, new(GridLength.Auto), new(DescriptionHeight, GridUnitType.Pixel) };
        _root.RowDefinitions[1].MinHeight = 40;
        _root.RowDefinitions[3].MinHeight = 28;
        GridTheme.BindBrush(_root, Panel.BackgroundProperty, GridTheme.BackgroundKey);

        BuildToolbar();
        Grid.SetRow(_toolbar, 0);
        _root.Children.Add(_toolbar);

        BuildRowsArea();
        BuildDescriptionPane();
        Content = _root;
        AutomationProperties.SetAutomationId(this, "PropertyGrid");
    }

    /// <summary>
    ///     Builds the toolbar: the two sort toggles and the search box.
    /// </summary>
    private void BuildToolbar()
    {
        ConfigureToggle(_categorizedButton, CategorizedIcon, "Categorized", "PropertyGrid_SortCategorized");
        ConfigureToggle(_alphabeticalButton, AlphabeticalIcon, "Alphabetical", "PropertyGrid_SortAlphabetical");
        _categorizedButton.Click += (_, _) => SortMode = PropertySortMode.Categorized;
        _alphabeticalButton.Click += (_, _) => SortMode = PropertySortMode.Alphabetical;

        _searchBox.PlaceholderText = "Search";
        _searchBox.MinHeight = 0;
        _searchBox.Height = 22;
        _searchBox.Padding = new Thickness(6, 0);
        _searchBox.FontSize = GridTheme.FontSize;
        _searchBox.VerticalContentAlignment = VerticalAlignment.Center;
        _searchBox.Margin = new Thickness(6, 0, 0, 0);
        AutomationProperties.SetAutomationId(_searchBox, "PropertyGrid_Search");
        AutomationProperties.SetName(_searchBox, "Search properties");
        _searchBox.TextChanged += (_, _) =>
        {
            if (!_syncingFilter)
            {
                _syncingFilter = true;
                Filter = _searchBox.Text;
                _syncingFilter = false;
            }
        };

        var bar = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*") };
        Grid.SetColumn(_alphabeticalButton, 1);
        Grid.SetColumn(_searchBox, 2);
        bar.Children.Add(_categorizedButton);
        bar.Children.Add(_alphabeticalButton);
        bar.Children.Add(_searchBox);
        _toolbar.Child = bar;
        _toolbar.Padding = new Thickness(4, 3);
        _toolbar.BorderThickness = new Thickness(0, 0, 0, 1);
        GridTheme.BindBrush(_toolbar, Border.BorderBrushProperty, GridTheme.LineKey);
        GridTheme.BindBrush(_toolbar, Border.BackgroundProperty, GridTheme.BandKey);
    }

    /// <summary>
    ///     Configures a sort toggle button with a vector icon.
    /// </summary>
    /// <param name="button">The button.</param>
    /// <param name="iconData">The icon path data.</param>
    /// <param name="tip">The tooltip.</param>
    /// <param name="id">The automation id.</param>
    private static void ConfigureToggle(ToggleButton button, string iconData, string tip, string id)
    {
        button.Width = 26;
        button.Height = 22;
        button.MinWidth = 0;
        button.MinHeight = 0;
        button.Padding = new Thickness(0);
        button.Margin = new Thickness(0, 0, 2, 0);
        button.HorizontalContentAlignment = HorizontalAlignment.Center;
        button.VerticalContentAlignment = VerticalAlignment.Center;
        button.Content = new PathIcon { Data = AvaloniaGeometry.Parse(iconData), Width = 14, Height = 14 };
        ToolTip.SetTip(button, tip);
        AutomationProperties.SetAutomationId(button, id);
        AutomationProperties.SetName(button, tip);
    }

    /// <summary>
    ///     Builds the rows area: the scrolled rows with the column splitter above them.
    /// </summary>
    private void BuildRowsArea()
    {
        _scroll.Content = _rowsPanel;
        _scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        _scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

        _columnSplitter.Width = 7;
        _columnSplitter.HorizontalAlignment = HorizontalAlignment.Left;
        _columnSplitter.Background = Brushes.Transparent;
        _columnSplitter.Cursor = new Cursor(StandardCursorType.SizeWestEast);
        _columnSplitter.DragDelta += (_, e) => NameColumnWidth = Math.Clamp(NameColumnWidth + e.Vector.X, 50, Math.Max(50, Bounds.Width - 50));
        AutomationProperties.SetAutomationId(_columnSplitter, "PropertyGrid_ColumnSplitter");
        AutomationProperties.SetName(_columnSplitter, "Column splitter");

        _emptyText.HorizontalAlignment = HorizontalAlignment.Center;
        _emptyText.VerticalAlignment = VerticalAlignment.Top;
        _emptyText.Margin = new Thickness(8, 16);
        _emptyText.FontSize = GridTheme.FontSize;
        _emptyText.Opacity = 0.6;
        _emptyText.IsVisible = false;

        var area = new Grid();
        area.Children.Add(_scroll);
        area.Children.Add(_emptyText);
        area.Children.Add(_columnSplitter);
        Grid.SetRow(area, 1);
        _root.Children.Add(area);
        ApplyNameWidth();
    }

    /// <summary>
    ///     Builds the description pane and the splitter above it.
    /// </summary>
    private void BuildDescriptionPane()
    {
        _descriptionSplitter.Height = 4;
        _descriptionSplitter.HorizontalAlignment = HorizontalAlignment.Stretch;
        _descriptionSplitter.ResizeDirection = GridResizeDirection.Rows;
        GridTheme.BindBrush(_descriptionSplitter, BackgroundProperty, GridTheme.LineKey);
        AutomationProperties.SetAutomationId(_descriptionSplitter, "PropertyGrid_DescriptionSplitter");
        Grid.SetRow(_descriptionSplitter, 2);
        _root.Children.Add(_descriptionSplitter);

        _descriptionName.FontWeight = FontWeight.SemiBold;
        _descriptionName.FontSize = GridTheme.FontSize;
        _descriptionText.FontSize = GridTheme.FontSize;
        _descriptionText.TextWrapping = TextWrapping.Wrap;
        _descriptionText.Opacity = 0.85;
        AutomationProperties.SetAutomationId(_descriptionName, "PropertyGrid_DescriptionName");
        AutomationProperties.SetAutomationId(_descriptionText, "PropertyGrid_DescriptionText");
        var content = new StackPanel { Spacing = 2 };
        content.Children.Add(_descriptionName);
        content.Children.Add(_descriptionText);
        _descriptionPane.Child = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        _descriptionPane.Padding = new Thickness(8, 4);
        GridTheme.BindBrush(_descriptionPane, Border.BackgroundProperty, GridTheme.BandKey);
        AutomationProperties.SetAutomationId(_descriptionPane, "PropertyGrid_Description");
        Grid.SetRow(_descriptionPane, 3);
        _root.Children.Add(_descriptionPane);
    }

    /// <summary>
    ///     Shows or hides the toolbar, the search box and the description pane.
    /// </summary>
    private void UpdateChrome()
    {
        _categorizedButton.IsVisible = ShowToolbar;
        _alphabeticalButton.IsVisible = ShowToolbar;
        _searchBox.IsVisible = ShowSearch;
        _toolbar.IsVisible = ShowToolbar || ShowSearch;
        _searchBox.Margin = new Thickness(ShowToolbar ? 6 : 0, 0, 0, 0);
        _descriptionPane.IsVisible = ShowDescription;
        _descriptionSplitter.IsVisible = ShowDescription;
        _root.RowDefinitions[3].Height = ShowDescription ? new GridLength(Math.Max(28, DescriptionHeight)) : new GridLength(0);
        _root.RowDefinitions[2].Height = ShowDescription ? GridLength.Auto : new GridLength(0);
    }

    /// <summary>
    ///     Checks the toggle that matches the sort mode.
    /// </summary>
    private void UpdateToolbarState()
    {
        _categorizedButton.IsChecked = SortMode == PropertySortMode.Categorized;
        _alphabeticalButton.IsChecked = SortMode == PropertySortMode.Alphabetical;
    }

    /// <summary>
    ///     Applies the name column width to the rows and the splitter thumb.
    /// </summary>
    private void ApplyNameWidth()
    {
        var width = Math.Max(30, NameColumnWidth);
        _columnSplitter.Margin = new Thickness(width - 3, 0, 0, 0);
        foreach (var row in _rowsPanel.Children.OfType<PropertyRowView>())
        {
            row.SetNameWidth(width);
        }
    }

    /// <summary>
    ///     Shows the name and description of the selected row in the description pane.
    /// </summary>
    private void UpdateDescription()
    {
        if (_selection is { } item)
        {
            _descriptionName.Text = item.Label;
            _descriptionText.Text = item.Description;
        }
        else if (_selectedKey is { } key && key.StartsWith("c:", StringComparison.Ordinal))
        {
            _descriptionName.Text = key[2..];
            _descriptionText.Text = string.Empty;
        }
        else
        {
            _descriptionName.Text = string.Empty;
            _descriptionText.Text = string.Empty;
        }
    }

    /// <summary>
    ///     Reacts to a change of <see cref="Filter"/>: updates the search box and the rows.
    /// </summary>
    private void OnFilterChanged()
    {
        if (!_syncingFilter)
        {
            _syncingFilter = true;
            _searchBox.Text = Filter;
            _syncingFilter = false;
        }

        RefreshRows();
    }
}

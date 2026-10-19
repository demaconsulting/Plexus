using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests;

/// <summary>
///     Tests of what the property grid shows: rows, categories, sorting, search, collapsing, description, layout.
/// </summary>
public class PropertyGridDisplayTests
{
    /// <summary>
    ///     Creates a grid over a widget in a window.
    /// </summary>
    /// <param name="widget">The widget.</param>
    /// <returns>The grid and its window.</returns>
    private static (PropertyGrid Grid, Window Window) Create(Widget widget)
    {
        var grid = new PropertyGrid { EditorRegistry = SliderEditor.CreateRegistry(), SelectedObject = widget };
        return (grid, GridHarness.Host(grid));
    }

    /// <summary>
    ///     An annotated object is shown as category bands followed by its browsable properties, sorted by display name.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyGrid_Rows_ShowCategoriesAndBrowsableProperties() => UiTest.Run(() =>
    {
        var (grid, window) = Create(new Widget());

        Assert.Equal(
            [
                "PropertyGrid_Category_Appearance", "PropertyGrid_Row_Caption", "PropertyGrid_Row_Fill", "PropertyGrid_Row_Opacity", "PropertyGrid_Row_Visible",
                "PropertyGrid_Category_Behavior", "PropertyGrid_Row_Features", "PropertyGrid_Row_Level", "PropertyGrid_Row_Mode", "PropertyGrid_Row_Secret", "PropertyGrid_Row_Timeout",
                "PropertyGrid_Category_Data", "PropertyGrid_Row_Id", "PropertyGrid_Row_Notes", "PropertyGrid_Row_Tags",
                "PropertyGrid_Category_Layout", "PropertyGrid_Row_Margins", "PropertyGrid_Row_Size",
            ],
            GridHarness.RowIds(grid));
        Assert.DoesNotContain("PropertyGrid_Row_Hidden", GridHarness.RowIds(grid));
        window.Close();
    });

    /// <summary>
    ///     Rows are a tight 22 pixels high.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyGrid_Rows_AreCompact() => UiTest.Run(() =>
    {
        var (grid, window) = Create(new Widget());

        Assert.All(grid.GetVisualDescendants().OfType<GridRowView>(), row => Assert.Equal(22, row.Bounds.Height));
        window.Close();
    });

    /// <summary>
    ///     The toolbar toggles switch between categorized and alphabetical order.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyGrid_SortToggle_SwitchesBetweenCategorizedAndAlphabetical() => UiTest.Run(() =>
    {
        var (grid, window) = Create(new Widget());
        var alphabetical = GridHarness.Get<ToggleButton>(grid, "PropertyGrid_SortAlphabetical");
        var categorized = GridHarness.Get<ToggleButton>(grid, "PropertyGrid_SortCategorized");
        Assert.True(categorized.IsChecked);

        GridHarness.Click(alphabetical);
        GridHarness.Pump(window);

        Assert.Equal(PropertySortMode.Alphabetical, grid.SortMode);
        Assert.True(alphabetical.IsChecked);
        Assert.False(categorized.IsChecked);
        Assert.DoesNotContain(GridHarness.RowIds(grid), id => id.StartsWith("PropertyGrid_Category_", StringComparison.Ordinal));
        Assert.Equal(
            ["Caption", "Features", "Fill", "Id", "Level", "Margins", "Mode", "Notes", "Opacity", "Secret", "Size", "Tags", "Timeout", "Visible"],
            GridHarness.PropertyPaths(grid));

        GridHarness.Click(categorized);
        GridHarness.Pump(window);

        Assert.Equal(PropertySortMode.Categorized, grid.SortMode);
        Assert.Contains("PropertyGrid_Category_Layout", GridHarness.RowIds(grid));
        window.Close();
    });

    /// <summary>
    ///     Typing in the search box filters the rows to the matching names and their categories; clearing restores them.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyGrid_Search_FiltersRowsByName() => UiTest.Run(() =>
    {
        var (grid, window) = Create(new Widget());
        var search = GridHarness.Get<TextBox>(grid, "PropertyGrid_Search");

        search.Text = "tim";
        GridHarness.Pump(window);

        Assert.Equal("tim", grid.Filter);
        Assert.Equal(["PropertyGrid_Category_Behavior", "PropertyGrid_Row_Timeout"], GridHarness.RowIds(grid));

        grid.Filter = null;
        GridHarness.Pump(window);

        Assert.Equal(string.Empty, search.Text ?? string.Empty);
        Assert.Equal(18, GridHarness.RowIds(grid).Count);
        window.Close();
    });

    /// <summary>
    ///     The search also finds nested properties and expands the objects that contain them; a filter without a match
    ///     explains itself.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyGrid_Search_FindsNestedPropertiesAndReportsNoMatch() => UiTest.Run(() =>
    {
        var (grid, window) = Create(new Widget());

        grid.Filter = "Left";
        GridHarness.Pump(window);

        Assert.Equal(["PropertyGrid_Category_Layout", "PropertyGrid_Row_Margins", "PropertyGrid_Row_Margins.Left"], GridHarness.RowIds(grid));

        grid.Filter = "zzz";
        GridHarness.Pump(window);

        Assert.Empty(GridHarness.RowIds(grid));
        Assert.Equal("No matching properties.", window.GetVisualDescendants().OfType<TextBlock>().First(t => t.IsVisible && t.Text == "No matching properties.").Text);
        window.Close();
    });

    /// <summary>
    ///     A category collapses and expands, and the state is remembered when another object is shown.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyGrid_Category_CollapsesAndRemembersState() => UiTest.Run(() =>
    {
        var (grid, window) = Create(new Widget());

        grid.SetCategoryExpanded("Data", false);
        GridHarness.Pump(window);

        Assert.False(grid.IsCategoryExpanded("Data"));
        Assert.DoesNotContain("PropertyGrid_Row_Tags", GridHarness.RowIds(grid));
        Assert.Contains("PropertyGrid_Category_Data", GridHarness.RowIds(grid));

        grid.SelectedObject = new Widget();
        GridHarness.Pump(window);

        Assert.DoesNotContain("PropertyGrid_Row_Tags", GridHarness.RowIds(grid));

        grid.SetCategoryExpanded("Data", true);
        GridHarness.Pump(window);

        Assert.Contains("PropertyGrid_Row_Tags", GridHarness.RowIds(grid));
        window.Close();
    });

    /// <summary>
    ///     A nested object expands to indented child rows and collapses again.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyGrid_NestedObject_ExpandsToIndentedChildren() => UiTest.Run(() =>
    {
        var (grid, window) = Create(new Widget());
        var parentIndent = GridHarness.Row(grid, "Margins").Depth;

        grid.SetExpanded("Margins", true);
        GridHarness.Pump(window);

        var ids = GridHarness.PropertyPaths(grid);
        Assert.Equal(["Margins", "Margins.Left", "Margins.Right", "Size"], ids.Where(p => p.StartsWith("Margins", StringComparison.Ordinal) || p == "Size").ToList());
        Assert.Equal(parentIndent + 1, GridHarness.Row(grid, "Margins.Left").Depth);

        grid.SetExpanded("Margins", false);
        GridHarness.Pump(window);

        Assert.DoesNotContain("Margins.Left", GridHarness.PropertyPaths(grid));
        window.Close();
    });

    /// <summary>
    ///     Read-only rows are dimmed and show their value as text rather than an editor.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyGrid_ReadOnlyRow_IsDimmedAndNotEditable() => UiTest.Run(() =>
    {
        var (grid, window) = Create(new Widget());
        var id = GridHarness.Row(grid, "Id");

        Assert.True(id.Item!.IsReadOnly);
        var editor = Assert.IsType<TextBlock>(id.Editor);
        Assert.True(editor.Opacity < 1);
        Assert.Equal("W-1042", editor.Text);
        Assert.True(id.NameLabel.Opacity < 1);
        Assert.Equal(1, GridHarness.Row(grid, "Caption").NameLabel.Opacity);
        window.Close();
    });

    /// <summary>
    ///     A value that differs from its default is bold; the default is regular.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyGrid_ChangedValue_IsBold() => UiTest.Run(() =>
    {
        var widget = new Widget();
        var (grid, window) = Create(widget);
        var caption = (TextBox)GridHarness.Editor(grid, "Caption");
        Assert.NotEqual(FontWeight.Bold, caption.FontWeight);

        widget.Caption = "Launch";
        grid.Refresh();
        GridHarness.Pump(window);

        Assert.Equal(FontWeight.Bold, caption.FontWeight);

        widget.Caption = "Widget";
        grid.Refresh();

        Assert.NotEqual(FontWeight.Bold, caption.FontWeight);
        window.Close();
    });

    /// <summary>
    ///     The description pane shows the display name and description of the selected property.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyGrid_Description_FollowsSelection() => UiTest.Run(() =>
    {
        var (grid, window) = Create(new Widget());
        var name = GridHarness.Get<TextBlock>(grid, "PropertyGrid_DescriptionName");
        var text = GridHarness.Get<TextBlock>(grid, "PropertyGrid_DescriptionText");
        Assert.Equal(string.Empty, name.Text ?? string.Empty);

        grid.SelectItem(grid.Session!.FindItem("Timeout"));

        Assert.Equal("Timeout", name.Text);
        Assert.Equal("Seconds before the widget goes idle.", text.Text);
        Assert.Same(grid.Session.FindItem("Timeout"), grid.SelectedItem);

        grid.SelectItem(grid.Session.FindItem("Mode"));

        Assert.Equal("Mode", name.Text);
        Assert.Equal("The display mode.", text.Text);
        window.Close();
    });

    /// <summary>
    ///     The toolbar, the search box and the description pane can be hidden.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyGrid_Chrome_CanBeHidden() => UiTest.Run(() =>
    {
        var (grid, window) = Create(new Widget());
        var toolbarButton = GridHarness.Get<ToggleButton>(grid, "PropertyGrid_SortCategorized");
        var search = GridHarness.Get<TextBox>(grid, "PropertyGrid_Search");
        var description = GridHarness.Get<Border>(grid, "PropertyGrid_Description");
        Assert.True(toolbarButton.IsVisible && search.IsVisible && description.IsVisible);

        grid.ShowToolbar = false;
        grid.ShowSearch = false;
        grid.ShowDescription = false;
        GridHarness.Pump(window);

        Assert.False(toolbarButton.IsVisible);
        Assert.False(search.IsVisible);
        Assert.False(description.IsVisible);
        window.Close();
    });

    /// <summary>
    ///     Dragging the column splitter resizes the name column of every row.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyGrid_ColumnSplitter_ResizesNameColumn() => UiTest.Run(() =>
    {
        var (grid, window) = Create(new Widget());
        var splitter = GridHarness.Get<Thumb>(grid, "PropertyGrid_ColumnSplitter");
        var before = grid.NameColumnWidth;

        splitter.RaiseEvent(new VectorEventArgs { RoutedEvent = Thumb.DragDeltaEvent, Vector = new Vector(40, 0) });
        GridHarness.Pump(window);

        Assert.Equal(before + 40, grid.NameColumnWidth);
        Assert.Equal(before + 40, GridHarness.Row(grid, "Caption").NameWidth);
        window.Close();
    });

    /// <summary>
    ///     A custom property source is shown through the Source property, and setting one of the content properties clears
    ///     the other.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyGrid_CustomSource_ReplacesSelectedObject() => UiTest.Run(() =>
    {
        var title = "Main";
        var custom = new CustomPropertySource([new CustomPropertyItem("Title", typeof(string), () => title, v => title = (string)v!)]);
        var (grid, window) = Create(new Widget());

        grid.Source = custom;
        GridHarness.Pump(window);

        Assert.Null(grid.SelectedObject);
        Assert.Equal(["Title"], GridHarness.PropertyPaths(grid));
        var box = (TextBox)GridHarness.Editor(grid, "Title");
        box.Focus();
        box.Text = "Renamed";
        GridHarness.Press(window, PhysicalKey.Enter);
        Assert.Equal("Renamed", title);

        grid.SelectedObject = new Widget();

        Assert.Null(grid.Source);
        Assert.Contains("Caption", GridHarness.PropertyPaths(grid));

        grid.SelectedObject = null;
        GridHarness.Pump(window);

        Assert.Empty(GridHarness.PropertyPaths(grid));
        Assert.Null(grid.Session);
        window.Close();
    });

    /// <summary>
    ///     An object that changes itself (change notification) is shown again with its new values.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyGrid_ObservableObject_ShowsExternalChanges() => UiTest.Run(() =>
    {
        var target = new Observable { Title = "one" };
        var grid = new PropertyGrid { SelectedObject = target };
        var window = GridHarness.Host(grid);
        var box = (TextBox)GridHarness.Editor(grid, "Title");
        Assert.Equal("one", box.Text);

        target.Title = "two";
        GridHarness.Pump(window);

        Assert.Equal("two", box.Text);
        window.Close();
    });

    /// <summary>
    ///     The row of a property is identified by its path for automation, including nested properties.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyGrid_Rows_HaveAutomationIdsAndNames() => UiTest.Run(() =>
    {
        var (grid, window) = Create(new Widget());
        grid.SetExpanded("Margins", true);
        GridHarness.Pump(window);

        var row = GridHarness.Get<Border>(grid, "PropertyGrid_Row_Margins.Left");

        Assert.Equal("Left", global::Avalonia.Automation.AutomationProperties.GetName(row));
        Assert.NotNull(GridHarness.Find<Control>(grid, "PropertyGrid_Editor_Margins.Left"));
        Assert.Equal("PropertyGrid", global::Avalonia.Automation.AutomationProperties.GetAutomationId(grid));
        window.Close();
    });
}

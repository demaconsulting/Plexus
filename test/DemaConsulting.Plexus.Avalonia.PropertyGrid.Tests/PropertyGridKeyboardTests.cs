using Avalonia.Controls;
using Avalonia.Input;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests;

/// <summary>
///     Tests of keyboard navigation and editing in the property grid.
/// </summary>
public class PropertyGridKeyboardTests
{
    /// <summary>
    ///     Creates a grid over a widget in a window; the grid has the keyboard focus.
    /// </summary>
    /// <param name="widget">The widget.</param>
    /// <returns>The grid and its window.</returns>
    private static (PropertyGrid Grid, Window Window) Create(Widget widget)
    {
        var grid = new PropertyGrid { SelectedObject = widget };
        var window = GridHarness.Host(grid);
        grid.Focus();
        GridHarness.Pump(window);
        return (grid, window);
    }

    /// <summary>
    ///     Up and Down move the selection through category and property rows and stop at the ends.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyGrid_Keyboard_UpDown_MoveSelectionThroughRows() => UiTest.Run(() =>
    {
        var (grid, window) = Create(new Widget());

        GridHarness.Press(window, PhysicalKey.ArrowDown);
        Assert.Null(grid.SelectedItem);
        Assert.Equal("Appearance", GridHarness.Get<TextBlock>(grid, "PropertyGrid_DescriptionName").Text);

        GridHarness.Press(window, PhysicalKey.ArrowDown);
        Assert.Equal("Caption", grid.SelectedItem?.Name);

        GridHarness.Press(window, PhysicalKey.ArrowDown);
        Assert.Equal("Fill", grid.SelectedItem?.Name);

        GridHarness.Press(window, PhysicalKey.ArrowUp);
        Assert.Equal("Caption", grid.SelectedItem?.Name);

        GridHarness.Press(window, PhysicalKey.End);
        Assert.Equal("Size", grid.SelectedItem?.Name);

        GridHarness.Press(window, PhysicalKey.ArrowDown);
        Assert.Equal("Size", grid.SelectedItem?.Name);

        GridHarness.Press(window, PhysicalKey.Home);
        Assert.Null(grid.SelectedItem);
        window.Close();
    });

    /// <summary>
    ///     Left collapses a category and Right expands it again.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyGrid_Keyboard_LeftRight_CollapseAndExpandCategory() => UiTest.Run(() =>
    {
        var (grid, window) = Create(new Widget());
        GridHarness.Press(window, PhysicalKey.ArrowDown);

        GridHarness.Press(window, PhysicalKey.ArrowLeft);

        Assert.False(grid.IsCategoryExpanded("Appearance"));
        Assert.DoesNotContain("PropertyGrid_Row_Caption", GridHarness.RowIds(grid));

        GridHarness.Press(window, PhysicalKey.ArrowRight);

        Assert.True(grid.IsCategoryExpanded("Appearance"));
        Assert.Contains("PropertyGrid_Row_Caption", GridHarness.RowIds(grid));
        window.Close();
    });

    /// <summary>
    ///     Right expands a nested object and then moves to its first child; Left returns to the parent and collapses it.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyGrid_Keyboard_LeftRight_ExpandNestedObjectAndMoveBetweenParentAndChild() => UiTest.Run(() =>
    {
        var (grid, window) = Create(new Widget());
        grid.SelectItem(grid.Session!.FindItem("Margins"));

        GridHarness.Press(window, PhysicalKey.ArrowRight);
        Assert.True(grid.IsExpanded("Margins"));

        GridHarness.Press(window, PhysicalKey.ArrowRight);
        Assert.Equal("Left", grid.SelectedItem?.Name);

        GridHarness.Press(window, PhysicalKey.ArrowLeft);
        Assert.Equal("Margins", grid.SelectedItem?.Name);

        GridHarness.Press(window, PhysicalKey.ArrowLeft);
        Assert.False(grid.IsExpanded("Margins"));

        GridHarness.Press(window, PhysicalKey.ArrowLeft);
        Assert.Null(grid.SelectedItem);
        window.Close();
    });

    /// <summary>
    ///     Enter and F2 move the focus into the editor of the selected row; Enter commits and returns the focus to the
    ///     grid; Escape abandons the edit.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyGrid_Keyboard_EnterAndF2_EditSelectedValue() => UiTest.Run(() =>
    {
        var widget = new Widget();
        var (grid, window) = Create(widget);
        grid.SelectItem(grid.Session!.FindItem("Caption"));
        var box = (TextBox)GridHarness.Editor(grid, "Caption");

        GridHarness.Press(window, PhysicalKey.Enter);

        Assert.Same(box, GridHarness.Focused(window));
        box.Text = "Typed";
        GridHarness.Press(window, PhysicalKey.Escape);

        Assert.Equal("Widget", widget.Caption);
        Assert.Equal("Widget", box.Text);
        Assert.Same(grid, GridHarness.Focused(window));

        GridHarness.Press(window, PhysicalKey.F2);
        Assert.Same(box, GridHarness.Focused(window));
        box.Text = "Typed";
        GridHarness.Press(window, PhysicalKey.Enter);

        Assert.Equal("Typed", widget.Caption);
        Assert.Same(grid, GridHarness.Focused(window));
        window.Close();
    });

    /// <summary>
    ///     Enter on a category or on a nested object without an editor expands and collapses it.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyGrid_Keyboard_Enter_TogglesCategoryAndNestedObject() => UiTest.Run(() =>
    {
        var (grid, window) = Create(new Widget());
        GridHarness.Press(window, PhysicalKey.ArrowDown);

        GridHarness.Press(window, PhysicalKey.Enter);
        Assert.False(grid.IsCategoryExpanded("Appearance"));

        GridHarness.Press(window, PhysicalKey.Enter);
        Assert.True(grid.IsCategoryExpanded("Appearance"));

        grid.SelectItem(grid.Session!.FindItem("Size"));
        GridHarness.Press(window, PhysicalKey.F2);
        Assert.True(grid.IsExpanded("Size"));
        window.Close();
    });

    /// <summary>
    ///     Tab moves from the name of a row to its value, then to the name of the next row; Shift+Tab goes back.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyGrid_Keyboard_Tab_MovesBetweenNameAndValue() => UiTest.Run(() =>
    {
        var (grid, window) = Create(new Widget());
        grid.SelectItem(grid.Session!.FindItem("Caption"));
        var caption = (TextBox)GridHarness.Editor(grid, "Caption");

        GridHarness.Press(window, PhysicalKey.Tab);
        Assert.Same(caption, GridHarness.Focused(window));

        GridHarness.Press(window, PhysicalKey.Tab);
        Assert.Same(grid, GridHarness.Focused(window));
        Assert.Equal("Fill", grid.SelectedItem?.Name);

        GridHarness.Press(window, PhysicalKey.Tab, RawInputModifiers.Shift);
        Assert.Same(caption, GridHarness.Focused(window));
        Assert.Equal("Caption", grid.SelectedItem?.Name);

        GridHarness.Press(window, PhysicalKey.Tab, RawInputModifiers.Shift);
        Assert.Same(grid, GridHarness.Focused(window));
        window.Close();
    });

    /// <summary>
    ///     Keys typed in the search box do not move the grid's selection.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyGrid_Keyboard_SearchBox_KeepsItsOwnKeys() => UiTest.Run(() =>
    {
        var (grid, window) = Create(new Widget());
        grid.SelectItem(grid.Session!.FindItem("Caption"));
        var search = GridHarness.Get<TextBox>(grid, "PropertyGrid_Search");
        search.Focus();

        GridHarness.Press(window, PhysicalKey.ArrowDown);

        Assert.Equal("Caption", grid.SelectedItem?.Name);
        window.Close();
    });
}

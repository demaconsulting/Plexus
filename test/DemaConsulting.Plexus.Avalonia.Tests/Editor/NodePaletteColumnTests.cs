using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.VisualTree;
using DemaConsulting.Plexus.Scene;

namespace DemaConsulting.Plexus.Avalonia.Tests;

/// <summary>
///     Tests of the node palette's multi-column layout: the column count for a width, the grid alignment of the boxes, full-width headers, the keyboard and scrolling.
/// </summary>
public class NodePaletteColumnTests
{
    /// <summary>
    ///     The column counts exercised by the width tests.
    /// </summary>
    private static readonly int[] WantedColumns = [1, 2, 3, 4];

    /// <summary>
    ///     The space the palette keeps around its entries on each side, in device-independent pixels.
    /// </summary>
    private const double Margin = 4;

    /// <summary>
    ///     The default smallest space between two cells, in device-independent pixels.
    /// </summary>
    private const double Spacing = 4;

    /// <summary>
    ///     Gets the palette width that holds exactly a number of columns of cells.
    /// </summary>
    /// <param name="host">The host, whose palette is already laid out.</param>
    /// <param name="columns">The number of columns.</param>
    /// <param name="spacing">The smallest space between two cells.</param>
    /// <returns>The width in device-independent pixels.</returns>
    private static double WidthFor(PaletteHost host, int columns, double spacing = Spacing)
    {
        // The items are measured only once the layout has run for the current list
        host.Flush();
        return (2 * Margin) + (columns * host.Palette.Items[0].DesiredSize.Width) + ((columns - 1) * spacing);
    }

    /// <summary>
    ///     Gets the horizontal center of an item's node box in window coordinates.
    /// </summary>
    /// <param name="host">The host.</param>
    /// <param name="item">The palette item.</param>
    /// <returns>The window x coordinate of the box center.</returns>
    private static double BoxCenterX(PaletteHost host, NodePaletteItem item) =>
        item.TranslatePoint(item.BoxOrigin, host.Window)!.Value.X + (item.Prototype.Box.Width * host.Palette.Zoom / 2);

    /// <summary>
    ///     Gets the vertical position of an item's node box top in window coordinates.
    /// </summary>
    /// <param name="host">The host.</param>
    /// <param name="item">The palette item.</param>
    /// <returns>The window y coordinate of the box top.</returns>
    private static double BoxTopY(PaletteHost host, NodePaletteItem item) => item.TranslatePoint(item.BoxOrigin, host.Window)!.Value.Y;

    /// <summary>
    ///     Gets the scroll viewer that holds the palette's entries.
    /// </summary>
    /// <param name="host">The host.</param>
    /// <returns>The scroll viewer.</returns>
    private static ScrollViewer ScrollerOf(PaletteHost host) => host.Palette.Items[0].FindAncestorOfType<ScrollViewer>()!;

    /// <summary>
    ///     Creates a plain list of simple node types, enough to fill several screens.
    /// </summary>
    /// <param name="count">The number of types.</param>
    /// <returns>The types.</returns>
    private static List<NodeType> ManyTypes(int count) =>
        [.. Enumerable.Range(0, count).Select(i => new NodeType($"many.T{i:00}") { Title = $"T{i:00}", Category = $"Group {(i / 12) + 1}" })];

    /// <summary>
    ///     Presses and releases a key in the host's window.
    /// </summary>
    /// <param name="host">The host.</param>
    /// <param name="key">The key.</param>
    private static void Press(PaletteHost host, Key key)
    {
        host.Window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null);
        host.Window.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, null);
        host.Flush();
    }

    /// <summary>
    ///     Validates that the column count follows the palette width: a width that holds exactly k cells gives k columns, one pixel less gives k - 1.
    /// </summary>
    [Fact]
    public Task NodePalette_Columns_FollowThePaletteWidth() => UiTest.Run(() =>
    {
        // Arrange: five items in one list, so the number of columns shows in the items' positions
        using var host = new PaletteHost();
        host.Palette.GroupByCategory = false;
        host.Flush();
        var counts = new List<(int Wanted, int Exact, int OneLess, int Columns)>();

        // Act: widths that hold 1 to 4 columns exactly, and one pixel less
        foreach (var wanted in WantedColumns)
        {
            host.SetPaletteWidth(WidthFor(host, wanted));
            var exact = host.Palette.ColumnCount;
            var columns = host.Palette.Items.Select(item => item.TranslatePoint(default, host.Palette)!.Value.X).Distinct().Count();
            host.SetPaletteWidth(WidthFor(host, wanted) - 1);
            counts.Add((wanted, exact, host.Palette.ColumnCount, columns));
        }

        // Assert
        Assert.Multiple(
            () => Assert.All(counts, c => Assert.Equal(c.Wanted, c.Exact)),
            () => Assert.All(counts, c => Assert.Equal(c.Wanted, c.Columns)),
            () => Assert.Equal([1, 1, 2, 3], counts.Select(c => c.OneLess)));
    });

    /// <summary>
    ///     Validates that every item of a multi-column palette has the same size and lies inside the palette's visible width.
    /// </summary>
    [Fact]
    public Task NodePalette_Columns_CellsAreEqualAndInsideThePalette() => UiTest.Run(() =>
    {
        // Arrange
        using var host = new PaletteHost();
        host.Palette.GroupByCategory = false;
        host.SetPaletteWidth(WidthFor(host, 3) + 7);

        // Act
        var items = host.Palette.Items;
        var rects = items.Select(item => new Rect(item.TranslatePoint(default, host.Palette)!.Value, item.Bounds.Size)).ToList();

        // Assert: equal widths, nothing beyond the palette's edges, and no overlap within a row
        Assert.Multiple(
            () => Assert.Single(items.Select(item => item.Bounds.Width).Distinct()),
            () => Assert.All(rects, r => Assert.True(r.Left >= Margin - 0.01 && r.Right <= host.Palette.Bounds.Width - Margin + 0.01, $"{r} is inside the palette")),
            () => Assert.False(rects[0].Intersects(rects[1]) || rects[1].Intersects(rects[2])),
            () => Assert.True(rects[1].Left - rects[0].Right >= Spacing - 0.01, "the cells keep the minimum spacing"));
    });

    /// <summary>
    ///     Validates that the node boxes line up: the same box center for every item of a column, and the same box top for every item of a row.
    /// </summary>
    [Fact]
    public Task NodePalette_Columns_BoxesAlignInAGrid() => UiTest.Run(() =>
    {
        // Arrange: three columns, two rows (3 + 2 items), types whose port labels differ widely
        using var host = new PaletteHost();
        host.Palette.GroupByCategory = false;
        host.SetPaletteWidth(WidthFor(host, 3));
        var items = host.Palette.Items;
        var cells = host.Palette.GetVisualDescendants().OfType<NodePaletteItem>().ToList();

        // Act
        var centers = items.Select(item => BoxCenterX(host, item)).ToList();
        var tops = items.Select(item => BoxTopY(host, item)).ToList();

        // Assert: columns 0 and 1 hold two items each (indexes 0/3 and 1/4), row tops are equal, columns differ
        Assert.Multiple(
            () => Assert.Equal(5, cells.Count),
            () => Assert.Equal(centers[0], centers[3], 0.01),
            () => Assert.Equal(centers[1], centers[4], 0.01),
            () => Assert.True(centers[1] - centers[0] > 100 && centers[2] - centers[1] > 100, "the columns are a cell apart"),
            () => Assert.Equal(tops[0], tops[1], 0.01),
            () => Assert.Equal(tops[1], tops[2], 0.01),
            () => Assert.Equal(tops[3], tops[4], 0.01),
            () => Assert.True(tops[3] > tops[0] + 50, "the second row is below the first"));
    });

    /// <summary>
    ///     Validates that category headers span the full width and every category starts a new row, also when several columns fit.
    /// </summary>
    [Fact]
    public Task NodePalette_Columns_HeadersSpanTheFullWidthAndSectionsStartNewRows() => UiTest.Run(() =>
    {
        // Arrange: grouped (Sources: 1, Processing: 2, Sinks: 1, Other: 1), width for three columns
        using var host = new PaletteHost();
        host.SetPaletteWidth(WidthFor(host, 3));
        var headers = host.Palette.GetVisualDescendants().OfType<Border>().Where(b => b.Child is TextBlock).ToList();

        // Act
        var source = host.ItemFor("test.Source");
        var mixer = host.ItemFor("test.Mixer");
        var badge = host.ItemFor("test.Badge");
        var sink = host.ItemFor("test.Sink");
        double TopOf(Control control) => control.TranslatePoint(default, host.Palette)!.Value.Y;

        // Assert: four headers of equal full width; Mixer and Badge share a row; each section starts below the one before
        Assert.Multiple(
            () => Assert.Equal(4, headers.Count),
            () => Assert.Single(headers.Select(header => header.Bounds.Width).Distinct()),
            () => Assert.All(headers, header => Assert.Equal(host.Palette.Bounds.Width - (2 * Margin), header.Bounds.Width, 0.01)),
            () => Assert.Equal(TopOf(mixer), TopOf(badge), 0.01),
            () => Assert.True(TopOf(mixer) >= TopOf(source) + source.Bounds.Height, "Processing starts below Sources"),
            () => Assert.True(TopOf(sink) >= TopOf(mixer) + mixer.Bounds.Height, "Sinks starts below Processing"),
            () => Assert.True(TopOf(mixer) >= TopOf(headers[1]) + headers[1].Bounds.Height, "the items are below their header"),
            () => Assert.Equal(3, host.Palette.ColumnCount));
    });

    /// <summary>
    ///     Validates the keyboard in several columns: Left and Right move within a row, Up and Down by a row to the nearest column.
    /// </summary>
    [Fact]
    public Task NodePalette_Columns_ArrowKeysMoveByCellAndRow() => UiTest.Run(() =>
    {
        // Arrange: three columns, rows [Source Mixer Badge] and [Sink Gain]
        using var host = new PaletteHost();
        host.Palette.GroupByCategory = false;
        host.SetPaletteWidth(WidthFor(host, 3));
        host.Palette.Focus();
        var steps = new List<string?>();
        void Step(Key key)
        {
            Press(host, key);
            steps.Add(host.Palette.SelectedNodeType?.Key);
        }

        // Act
        Step(Key.Right);
        Step(Key.Right);
        Step(Key.Right);
        Step(Key.Right);
        Step(Key.Down);
        Step(Key.Down);
        Step(Key.Left);
        Step(Key.Left);
        Step(Key.Left);
        Step(Key.Up);
        Step(Key.Up);
        Step(Key.Down);
        Step(Key.Right);
        Step(Key.Right);

        // Assert
        Assert.Equal(
            [
                "test.Source", "test.Mixer", "test.Badge", "test.Badge", // Right x4: stops at the end of the row
                "test.Gain", "test.Gain", // Down from column 2 to the nearest column of the shorter second row; no row below
                "test.Sink", "test.Sink", "test.Sink", // Left x3: stops at the start of the row
                "test.Source", "test.Source", // Up, Up: stops at the first row
                "test.Sink", // Down
                "test.Gain", "test.Gain", // Right x2: stops at the end of the shorter row
            ],
            steps);
    });

    /// <summary>
    ///     Validates that Up and Down cross category headers in several columns, and that Left and Right leave the keys alone in a single column.
    /// </summary>
    [Fact]
    public Task NodePalette_Columns_VerticalMoveCrossesSectionsAndSingleColumnIgnoresHorizontalKeys() => UiTest.Run(() =>
    {
        // Arrange: grouped, three columns; Sources (Source), Processing (Mixer Badge), Sinks (Sink), Other (Gain)
        using var host = new PaletteHost();
        host.SetPaletteWidth(WidthFor(host, 3));
        host.Palette.Focus();

        // Act
        host.Palette.SelectedNodeType = host.Types.Get("test.Source");
        var down = new List<string?>();
        foreach (var _ in Enumerable.Range(0, 4))
        {
            Press(host, Key.Down);
            down.Add(host.Palette.SelectedNodeType?.Key);
        }

        // One column: Right and Left are not the palette's keys
        host.SetPaletteWidth(WidthFor(host, 1));
        host.Palette.SelectedNodeType = host.Types.Get("test.Mixer");
        var single = host.Palette.ColumnCount;
        Press(host, Key.Right);
        var afterRight = host.Palette.SelectedNodeType?.Key;
        Press(host, Key.Down);
        var afterDown = host.Palette.SelectedNodeType?.Key;

        // Assert
        Assert.Multiple(
            () => Assert.Equal(["test.Mixer", "test.Sink", "test.Gain", "test.Gain"], down),
            () => Assert.Equal(1, single),
            () => Assert.Equal("test.Mixer", afterRight),
            () => Assert.Equal("test.Badge", afterDown));
    });

    /// <summary>
    ///     Validates that Enter and the search box keep working in several columns: Down from the search box selects the first item, Enter activates, Left and Right edit the text.
    /// </summary>
    [Fact]
    public Task NodePalette_Columns_SearchBoxKeysStillWork() => UiTest.Run(() =>
    {
        // Arrange
        using var host = new PaletteHost();
        host.Palette.GroupByCategory = false;
        host.SetPaletteWidth(WidthFor(host, 3));
        var activated = new List<string>();
        host.Palette.NodeTypeActivated += (_, e) => activated.Add(e.NodeType.Key);
        var box = host.Palette.GetVisualDescendants().OfType<TextBox>().Single();
        box.Focus();
        host.Flush();

        // Act
        Press(host, Key.Down);
        var afterDown = host.Palette.SelectedNodeType?.Key;
        Press(host, Key.Right);
        var afterRight = host.Palette.SelectedNodeType?.Key;
        Press(host, Key.Enter);

        // Assert: Right in the search box does not move the selection
        Assert.Multiple(
            () => Assert.Equal("test.Source", afterDown),
            () => Assert.Equal("test.Source", afterRight),
            () => Assert.Equal(["test.Source"], activated));
    });

    /// <summary>
    ///     Validates that ScrollIntoView and Select reveal an item that is below the view when the items are in several columns.
    /// </summary>
    [Fact]
    public Task NodePalette_Columns_ScrollIntoViewRevealsTheItem() => UiTest.Run(() =>
    {
        // Arrange: 48 simple types in four categories, three columns, far more than one screen
        using var host = new PaletteHost();
        var types = ManyTypes(48);
        host.Palette.Types = types;
        host.Palette.Zoom = 1.5;
        host.Flush();
        host.SetPaletteWidth(WidthFor(host, 3));
        var scroller = ScrollerOf(host);
        var last = types[^1];
        var overflows = scroller.Extent.Height > scroller.Viewport.Height;

        // Act
        var scrolled = host.Palette.ScrollIntoView(last);
        host.Flush();
        var item = host.ItemFor(last.Key);
        var top = item.TranslatePoint(default, scroller)!.Value.Y;
        var offsetAfter = scroller.Offset;
        host.Palette.ScrollIntoView(types[0]);
        host.Flush();
        var firstTop = host.ItemFor(types[0].Key).TranslatePoint(default, scroller)!.Value.Y;
        var selected = host.Palette.Select(types[30]);
        host.Flush();
        var middle = host.ItemFor(types[30].Key).TranslatePoint(default, scroller)!.Value.Y;

        // Assert: the last item (third column) is inside the view, the first brings the view back, Select selects and reveals
        Assert.Multiple(
            () => Assert.True(overflows, "the content is taller than the view"),
            () => Assert.Equal(3, host.Palette.ColumnCount),
            () => Assert.True(scrolled),
            () => Assert.True(offsetAfter.Y > 0),
            () => Assert.Equal(0, offsetAfter.X),
            () => Assert.InRange(top, 0, scroller.Viewport.Height - 1),
            () => Assert.InRange(firstTop, 0, scroller.Viewport.Height - 1),
            () => Assert.True(selected),
            () => Assert.Same(types[30], host.Palette.SelectedNodeType),
            () => Assert.InRange(middle, 0, scroller.Viewport.Height - 1));
    });

    /// <summary>
    ///     Validates that the palette scrolls horizontally only when one cell is wider than the palette, and not otherwise.
    /// </summary>
    [Fact]
    public Task NodePalette_Columns_ScrollHorizontallyOnlyWhenOneCellDoesNotFit() => UiTest.Run(() =>
    {
        // Arrange
        using var host = new PaletteHost();
        var scroller = ScrollerOf(host);

        // Act
        host.SetPaletteWidth(WidthFor(host, 3));
        var wideExtent = scroller.Extent.Width;
        var wideViewport = scroller.Viewport.Width;
        host.SetPaletteWidth(WidthFor(host, 1) - 40);
        var narrowExtent = scroller.Extent.Width;
        var narrowViewport = scroller.Viewport.Width;
        var narrowColumns = host.Palette.ColumnCount;

        // Assert
        Assert.Multiple(
            () => Assert.Equal(wideViewport, wideExtent, 0.01),
            () => Assert.Equal(1, narrowColumns),
            () => Assert.True(narrowExtent > narrowViewport + 1, "a cell wider than the palette scrolls"));
    });

    /// <summary>
    ///     Validates that the grid is laid out again when the filter, the zoom or the types change.
    /// </summary>
    [Fact]
    public Task NodePalette_Columns_LayOutAgainOnFilterZoomAndTypes() => UiTest.Run(() =>
    {
        // Arrange: three columns ungrouped
        using var host = new PaletteHost();
        host.Palette.GroupByCategory = false;
        host.SetPaletteWidth(WidthFor(host, 3));
        double TopOf(NodePaletteItem item) => item.TranslatePoint(default, host.Palette)!.Value.Y;
        var rowsBefore = host.Palette.Items.Select(TopOf).Distinct().Count();

        // Act: a filter leaves Source and Sink, which now share the first row
        host.Palette.Filter = "test.S";
        host.Flush();
        var filtered = host.Palette.Items.ToList();
        var filteredRows = filtered.Select(TopOf).Distinct().Count();
        host.Palette.Filter = null;
        host.Flush();

        // A smaller zoom makes the cells narrower, so more fit
        host.Palette.Zoom = 0.5;
        host.Flush();
        var smaller = host.Palette.ColumnCount;
        var smallerRows = host.Palette.Items.Select(TopOf).Distinct().Count();
        host.Palette.Zoom = 1;
        host.Flush();

        // More types make more rows
        host.Palette.Types = host.Types.Concat(host.Types.Select(t => new NodeType(t.Key + "2") { Title = t.Title })).ToList();
        host.Flush();
        var doubledRows = host.Palette.Items.Select(TopOf).Distinct().Count();

        // Assert
        Assert.Multiple(
            () => Assert.Equal(2, rowsBefore),
            () => Assert.Equal(["test.Source", "test.Sink"], filtered.Select(i => i.NodeType.Key)),
            () => Assert.Equal(1, filteredRows),
            () => Assert.True(smaller > 3),
            () => Assert.Equal(1, smallerRows),
            () => Assert.Equal(4, doubledRows));
    });

    /// <summary>
    ///     Validates MaxColumns (a cap, zero for no cap, negative counts as zero) and MinColumnSpacing (a larger space means fewer columns, negative counts as zero).
    /// </summary>
    [Fact]
    public Task NodePalette_Columns_MaxColumnsAndMinColumnSpacing() => UiTest.Run(() =>
    {
        // Arrange: a width that holds four columns at the default spacing
        using var host = new PaletteHost();
        host.Palette.GroupByCategory = false;
        host.SetPaletteWidth(WidthFor(host, 4));
        var defaults = (host.Palette.MaxColumns, host.Palette.MinColumnSpacing, host.Palette.ColumnCount);

        // Act
        host.Palette.MaxColumns = 2;
        host.Flush();
        var capped = host.Palette.ColumnCount;
        host.Palette.MaxColumns = 1;
        host.Flush();
        var single = host.Palette.ColumnCount;
        host.Palette.MaxColumns = -3;
        host.Flush();
        var negative = (host.Palette.MaxColumns, host.Palette.ColumnCount);
        host.Palette.MinColumnSpacing = 60;
        host.Flush();
        var wideSpacing = host.Palette.ColumnCount;
        var spacingGap = host.Palette.Items[1].TranslatePoint(default, host.Palette)!.Value.X - host.Palette.Items[0].TranslatePoint(new Point(host.Palette.Items[0].Bounds.Width, 0), host.Palette)!.Value.X;
        host.Palette.MinColumnSpacing = -5;
        var clamped = host.Palette.MinColumnSpacing;
        host.Flush();

        // Assert
        Assert.Multiple(
            () => Assert.Equal((0, 4.0, 4), defaults),
            () => Assert.Equal(2, capped),
            () => Assert.Equal(1, single),
            () => Assert.Equal((0, 4), negative),
            () => Assert.True(wideSpacing < 4),
            () => Assert.True(spacingGap >= 60 - 0.01, "the cells keep the larger space"),
            () => Assert.Equal(0, clamped),
            () => Assert.Equal(4, host.Palette.ColumnCount));
    });

    /// <summary>
    ///     Validates that the column grid works with a palette that follows an editor (no explicit types), and that an empty palette has one column.
    /// </summary>
    [Fact]
    public Task NodePalette_Columns_EmptyPaletteHasOneColumn() => UiTest.Run(() =>
    {
        // Arrange / Act
        using var host = new PaletteHost();
        host.SetPaletteWidth(WidthFor(host, 3));
        host.Palette.Filter = "zzz";
        host.Flush();

        // Assert
        Assert.Multiple(
            () => Assert.Empty(host.Palette.Items),
            () => Assert.Equal(1, host.Palette.ColumnCount));
    });
}

using Avalonia;
using Avalonia.Controls;
using AvaloniaRect = Avalonia.Rect;
using AvaloniaSize = Avalonia.Size;

namespace DemaConsulting.Plexus.Avalonia;

/// <summary>
///     The panel inside a <see cref="NodePalette"/>: category headers across the full width, and under each header the items in a grid of uniform cells.
/// </summary>
/// <remarks>
///     <para>
///         Every <see cref="NodePaletteItem"/> has the same minimum width (the palette's shared box column), so
///         the cells of the grid are all the same width: the column count is as many cells as fit in the
///         available width, at least one and at most <see cref="MaxColumns"/>, and the cells share the width
///         evenly. Each item's node box is centered in its cell, so the boxes line up down every column,
///         and every item of a row has the same box top, so they line up along every row. A row is as tall
///         as its tallest item. Any other child (a category header) starts a new section, takes the
///         full width and ends the previous section's last row.
///     </para>
///     <para>
///         The available width cannot come from the measure pass: the palette scrolls its content, and a
///         scroll viewer offers its content unlimited width. The palette therefore hands the width of its
///         viewport in as <see cref="AvailableWidth"/>. When even one cell is wider than that, the panel is
///         as wide as one cell and the scroll viewer scrolls horizontally.
///     </para>
/// </remarks>
internal sealed class NodePalettePanel : Panel
{
    /// <summary>
    ///     The vertical space, in device-independent pixels, between headers and rows.
    /// </summary>
    private readonly double _rowSpacing;

    /// <summary>
    ///     The row and column of every item, in child order.
    /// </summary>
    private readonly List<(int Row, int Column)> _cells = [];

    /// <summary>
    ///     The vertical extent of every band (a header, or one row of items), in layout order.
    /// </summary>
    private readonly List<(double Top, double Height)> _bands = [];

    /// <summary>
    ///     The band of every child, in child order.
    /// </summary>
    private readonly List<int> _childBands = [];

    /// <summary>
    ///     The width available to the content, or <see cref="double.NaN"/> when unknown.
    /// </summary>
    private double _availableWidth = double.NaN;

    /// <summary>
    ///     The most columns allowed; zero or less means no limit.
    /// </summary>
    private int _maxColumns;

    /// <summary>
    ///     The smallest horizontal space between two cells, in device-independent pixels.
    /// </summary>
    private double _minColumnSpacing = 4;

    /// <summary>
    ///     The number of columns of the last measure.
    /// </summary>
    private int _columnCount = 1;

    /// <summary>
    ///     Initializes a new instance of the <see cref="NodePalettePanel"/> class.
    /// </summary>
    /// <param name="rowSpacing">The vertical space between headers and rows, in device-independent pixels.</param>
    public NodePalettePanel(double rowSpacing) => _rowSpacing = rowSpacing;

    /// <summary>
    ///     Gets or sets the width, in device-independent pixels, the content may use; <see cref="double.NaN"/> (the default) lays the items out in one column.
    /// </summary>
    public double AvailableWidth
    {
        get => _availableWidth;
        set
        {
            // Equals treats NaN as equal to itself, so an unknown width does not invalidate on every pass
            if (Differs(_availableWidth, value))
            {
                _availableWidth = value;
                InvalidateMeasure();
            }
        }
    }

    /// <summary>
    ///     Gets or sets the most columns the items may be laid out in; zero or less (the default) means as many as fit.
    /// </summary>
    public int MaxColumns
    {
        get => _maxColumns;
        set
        {
            if (_maxColumns != value)
            {
                _maxColumns = value;
                InvalidateMeasure();
            }
        }
    }

    /// <summary>
    ///     Gets or sets the smallest horizontal space between two cells, in device-independent pixels.
    /// </summary>
    public double MinColumnSpacing
    {
        get => _minColumnSpacing;
        set
        {
            if (Differs(_minColumnSpacing, value))
            {
                _minColumnSpacing = value;
                InvalidateMeasure();
            }
        }
    }

    /// <summary>
    ///     Gets the number of columns of the last layout (one when there is nothing to lay out).
    /// </summary>
    public int ColumnCount => _columnCount;

    /// <summary>
    ///     Gets the row and column of every item of the last layout, in the order the items were added.
    /// </summary>
    /// <remarks>
    ///     Rows are numbered over the whole panel, so the first row of a section is one more than the last row of the section before it.
    /// </remarks>
    public IReadOnlyList<(int Row, int Column)> Cells => _cells;

    /// <summary>
    ///     Determines whether two widths differ, counting an unknown width (NaN) as different from every known one.
    /// </summary>
    /// <param name="a">The first width.</param>
    /// <param name="b">The second width.</param>
    /// <returns><see langword="true"/> when they differ.</returns>
    private static bool Differs(double a, double b) => double.IsNaN(a) != double.IsNaN(b) || Math.Abs(a - b) > 1e-9;

    /// <summary>
    ///     Computes the number of columns for a width.
    /// </summary>
    /// <param name="width">The available width, or NaN when unknown.</param>
    /// <param name="cellWidth">The width of one cell.</param>
    /// <param name="spacing">The smallest space between two cells.</param>
    /// <param name="maxColumns">The most columns allowed; zero or less for no limit.</param>
    /// <returns>The number of columns, at least one.</returns>
    internal static int ColumnsFor(double width, double cellWidth, double spacing, int maxColumns)
    {
        if (double.IsNaN(width) || double.IsInfinity(width) || cellWidth <= 0)
        {
            return 1;
        }

        var fitting = (int)Math.Floor((width + spacing) / (cellWidth + spacing));
        return Math.Clamp(fitting, 1, maxColumns > 0 ? maxColumns : int.MaxValue);
    }

    /// <inheritdoc />
    protected override AvaloniaSize MeasureOverride(AvaloniaSize availableSize)
    {
        var spacing = Math.Max(0, _minColumnSpacing);
        var width = double.IsNaN(_availableWidth) ? availableSize.Width : _availableWidth;

        // Items have a fixed size; measure them first, because the cell width decides the column count
        var cellWidth = 0.0;
        foreach (var item in Children.OfType<NodePaletteItem>())
        {
            item.Measure(AvaloniaSize.Infinity);
            cellWidth = Math.Max(cellWidth, item.DesiredSize.Width);
        }

        _columnCount = ColumnsFor(width, cellWidth, spacing, _maxColumns);
        var headerWidth = double.IsNaN(width) || double.IsInfinity(width) ? cellWidth : Math.Max(width, cellWidth);

        // Bands: a header is a band of its own; the items between two headers fill bands of _columnCount cells
        _cells.Clear();
        _bands.Clear();
        _childBands.Clear();
        var row = -1;
        var column = _columnCount;
        var bottom = 0.0;
        foreach (var child in Children)
        {
            // The next band starts below the previous one, after the row spacing
            var top = _bands.Count == 0 ? 0 : bottom + _rowSpacing;
            if (child is NodePaletteItem item)
            {
                if (column >= _columnCount)
                {
                    // The previous row is full, or a header came: open the next row
                    _bands.Add((top, 0));
                    row++;
                    column = 0;
                }

                var band = _bands.Count - 1;
                var height = Math.Max(_bands[band].Height, item.DesiredSize.Height);
                _bands[band] = (_bands[band].Top, height);
                bottom = _bands[band].Top + height;
                _cells.Add((row, column++));
                _childBands.Add(band);
            }
            else
            {
                child.Measure(new AvaloniaSize(headerWidth, double.PositiveInfinity));
                _bands.Add((top, child.DesiredSize.Height));
                _childBands.Add(_bands.Count - 1);
                bottom = top + child.DesiredSize.Height;

                // The header closes the open row
                column = _columnCount;
            }
        }

        return new AvaloniaSize((_columnCount * cellWidth) + ((_columnCount - 1) * spacing), bottom);
    }

    /// <inheritdoc />
    protected override AvaloniaSize ArrangeOverride(AvaloniaSize finalSize)
    {
        var spacing = Math.Max(0, _minColumnSpacing);
        var columns = _columnCount;

        // Equal cells that share the width: whole pixels keep every column's box centers identical, and the
        // left edges are spread evenly so the leftover pixels go between the cells, not to one side
        var cellWidth = columns > 1 ? Math.Floor((finalSize.Width - ((columns - 1) * spacing)) / columns) : finalSize.Width;
        var index = 0;
        for (var i = 0; i < Children.Count; i++)
        {
            var (top, height) = _bands[_childBands[i]];
            if (Children[i] is NodePaletteItem item)
            {
                var column = _cells[index++].Column;
                var left = columns > 1 ? Math.Round(column * (finalSize.Width - cellWidth) / (columns - 1)) : 0;
                item.Arrange(new AvaloniaRect(left, top, cellWidth, height));
            }
            else
            {
                Children[i].Arrange(new AvaloniaRect(0, top, finalSize.Width, height));
            }
        }

        return finalSize;
    }
}


namespace DemaConsulting.Plexus.Scene;

/// <summary>
///     A uniform-grid spatial index: items are bucketed by the grid cells their bounds touch so a region query only visits nearby items.
/// </summary>
/// <remarks>
///     Items keep their insertion order in query results, which for a scene is the z-order.
///     Items that would touch many cells (such as the background grid) are kept in a short
///     "large" list that every query checks, instead of being copied into hundreds of buckets.
///     Not thread-safe for concurrent mutation; read-only use after building is safe.
/// </remarks>
/// <typeparam name="T">The indexed item type.</typeparam>
internal sealed class SpatialIndex<T>
{
    /// <summary>
    ///     Items touching more cells than this are kept in the large list.
    /// </summary>
    private const int LargeCellCount = 64;

    /// <summary>
    ///     The side length of a grid cell.
    /// </summary>
    private readonly double _cellSize;

    /// <summary>
    ///     The items in insertion order.
    /// </summary>
    private readonly List<T> _items = [];

    /// <summary>
    ///     The bounds of each item, parallel to <see cref="_items"/>.
    /// </summary>
    private readonly List<Rect> _bounds = [];

    /// <summary>
    ///     Item indexes per grid cell.
    /// </summary>
    private readonly Dictionary<long, List<int>> _buckets = [];

    /// <summary>
    ///     Indexes of the items that are too large to be bucketed.
    /// </summary>
    private readonly List<int> _large = [];

    /// <summary>
    ///     Initializes a new instance of the <see cref="SpatialIndex{T}"/> class.
    /// </summary>
    /// <param name="cellSize">The side length of a grid cell; larger cells mean fewer buckets but more candidates per query.</param>
    public SpatialIndex(double cellSize = 128)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(cellSize, 0);
        _cellSize = cellSize;
    }

    /// <summary>
    ///     Gets the number of indexed items.
    /// </summary>
    public int Count => _items.Count;

    /// <summary>
    ///     Adds an item.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="bounds">The region the item occupies.</param>
    public void Add(T item, Rect bounds)
    {
        var index = _items.Count;
        _items.Add(item);
        _bounds.Add(bounds);

        // Work out which cells the bounds touch; huge items skip bucketing altogether
        var (x0, y0, x1, y1) = CellRange(bounds);
        if (((long)(x1 - x0) + 1) * ((long)(y1 - y0) + 1) > LargeCellCount)
        {
            _large.Add(index);
            return;
        }

        for (var cx = x0; cx <= x1; cx++)
        {
            for (var cy = y0; cy <= y1; cy++)
            {
                var key = CellKey(cx, cy);
                if (!_buckets.TryGetValue(key, out var bucket))
                {
                    bucket = [];
                    _buckets[key] = bucket;
                }

                bucket.Add(index);
            }
        }
    }

    /// <summary>
    ///     Finds the items whose bounds intersect a region.
    /// </summary>
    /// <param name="area">The query region.</param>
    /// <returns>The matching items in insertion order.</returns>
    public List<T> Query(Rect area) => [.. QueryIndexes(area).Select(i => _items[i])];

    /// <summary>
    ///     Finds the indexes of the items whose bounds intersect a region.
    /// </summary>
    /// <param name="area">The query region.</param>
    /// <returns>The insertion indexes of the matching items in ascending order.</returns>
    public List<int> QueryIndexes(Rect area)
    {
        var found = new HashSet<int>();
        var (x0, y0, x1, y1) = CellRange(area);

        // A huge region would visit more cells than there are buckets, so scan the buckets instead
        if (((long)(x1 - x0) + 1) * ((long)(y1 - y0) + 1) > _buckets.Count)
        {
            foreach (var bucket in _buckets.Values)
            {
                AddMatches(bucket, area, found);
            }
        }
        else
        {
            for (var cx = x0; cx <= x1; cx++)
            {
                for (var cy = y0; cy <= y1; cy++)
                {
                    if (_buckets.TryGetValue(CellKey(cx, cy), out var bucket))
                    {
                        AddMatches(bucket, area, found);
                    }
                }
            }
        }

        AddMatches(_large, area, found);
        var result = new List<int>(found);
        result.Sort();
        return result;
    }

    /// <summary>
    ///     Gets an indexed item by insertion index.
    /// </summary>
    /// <param name="index">The insertion index.</param>
    /// <returns>The item.</returns>
    public T ItemAt(int index) => _items[index];

    /// <summary>
    ///     Adds the candidates of a bucket whose bounds really intersect the region.
    /// </summary>
    /// <param name="candidates">Item indexes to check.</param>
    /// <param name="area">The query region.</param>
    /// <param name="found">The set receiving matches.</param>
    private void AddMatches(List<int> candidates, Rect area, HashSet<int> found)
    {
        foreach (var index in candidates.Where(i => _bounds[i].Intersects(area)))
        {
            found.Add(index);
        }
    }

    /// <summary>
    ///     Computes the inclusive range of cells a rectangle touches.
    /// </summary>
    /// <param name="rect">The rectangle.</param>
    /// <returns>The first and last column and row.</returns>
    private (int X0, int Y0, int X1, int Y1) CellRange(Rect rect) => (
        ToCell(rect.Left),
        ToCell(rect.Top),
        ToCell(rect.Right),
        ToCell(rect.Bottom));

    /// <summary>
    ///     Converts a coordinate to a cell index, clamped so extreme values cannot overflow.
    /// </summary>
    /// <param name="value">The coordinate.</param>
    /// <returns>The cell index.</returns>
    private int ToCell(double value) =>
        (int)Math.Clamp(Math.Floor(value / _cellSize), -1_000_000_000d, 1_000_000_000d);

    /// <summary>
    ///     Packs a cell's column and row into one dictionary key.
    /// </summary>
    /// <param name="cx">The column.</param>
    /// <param name="cy">The row.</param>
    /// <returns>The key.</returns>
    private static long CellKey(int cx, int cy) => ((long)cx << 32) ^ (uint)cy;
}

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid;

/// <summary>
///     How a grid orders and groups its items.
/// </summary>
public enum PropertySortMode
{
    /// <summary>
    ///     Grouped by category: the categories are ordered by their category rank (<c>CategoryOrder</c>, the lowest rank of
    ///     a category decides) and alphabetically within equal ranks; the items of a category are ordered with
    ///     parenthesized names first, then by item rank (<c>Order</c>, lower first) and then alphabetically by label.
    /// </summary>
    Categorized,

    /// <summary>
    ///     One flat list in alphabetical order of label.
    /// </summary>
    Alphabetical,
}

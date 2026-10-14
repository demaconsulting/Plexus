namespace DemaConsulting.Plexus.Avalonia.PropertyGrid;

/// <summary>
///     How a grid orders and groups its items.
/// </summary>
public enum PropertySortMode
{
    /// <summary>
    ///     Grouped by category (categories in alphabetical order), items alphabetical by label within each category.
    /// </summary>
    Categorized,

    /// <summary>
    ///     One flat list in alphabetical order of label.
    /// </summary>
    Alphabetical,
}

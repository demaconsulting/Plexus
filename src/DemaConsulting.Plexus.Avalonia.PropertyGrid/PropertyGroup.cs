namespace DemaConsulting.Plexus.Avalonia.PropertyGrid;

/// <summary>
///     A category heading and the items beneath it, produced by <see cref="PropertyOrdering.Group"/>.
/// </summary>
/// <param name="Category">The category name, or <see langword="null"/> for the single flat group of <see cref="PropertySortMode.Alphabetical"/>.</param>
/// <param name="Items">The items of the group in display order.</param>
public sealed record PropertyGroup(string? Category, IReadOnlyList<PropertyItem> Items);

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid;

/// <summary>
///     Orders and groups property items the way the WinForms property grid does.
/// </summary>
/// <remarks>
///     Within a group, items whose name is parenthesized (<see cref="PropertyItem.IsParenthesized"/>) come first, then
///     the others, each alphabetical by <see cref="PropertyItem.DisplayName"/>. Categories are ordered by their
///     <see cref="PropertyItem.CategoryOrder"/> rank (the lowest of their items) and then alphabetically. Comparison is
///     culture-invariant and
///     ignores case, so the order does not depend on the user's culture. The sort is stable: items that compare equal
///     keep their source order.
/// </remarks>
/// <example>
///     <code><![CDATA[
///     using DemaConsulting.Plexus.Avalonia.PropertyGrid;
///
///     using var source = new ReflectionPropertySource(new Person { Name = "Ada" });
///     foreach (PropertyGroup group in PropertyOrdering.Group(source.GetItems(), PropertySortMode.Categorized))
///     {
///         Console.WriteLine(group.Category);                  // a heading in your own UI
///         foreach (PropertyItem item in group.Items)
///         {
///             Console.WriteLine($"  {item.Label}");           // a row in your own UI
///         }
///     }
///
///     public class Person
///     {
///         public string Name { get; set; } = "";
///     }
///     ]]></code>
/// </example>
public static class PropertyOrdering
{
    /// <summary>
    ///     Orders items for display.
    /// </summary>
    /// <param name="items">The items.</param>
    /// <param name="mode">The sort mode.</param>
    /// <returns>
    ///     The items in display order: for <see cref="PropertySortMode.Categorized"/> by category then label, for
    ///     <see cref="PropertySortMode.Alphabetical"/> by label.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<PropertyItem> Order(IEnumerable<PropertyItem> items, PropertySortMode mode)
    {
        ArgumentNullException.ThrowIfNull(items);
        var comparer = StringComparer.InvariantCultureIgnoreCase;
        var list = items.ToList();

        // A category ranks as its lowest item, so every item of a category sorts together
        var ranks = list.GroupBy(i => i.Category, comparer).ToDictionary(g => g.Key, g => g.Min(i => i.CategoryOrder), comparer);
        var ordered = mode == PropertySortMode.Categorized
            ? list.OrderBy(i => ranks[i.Category]).ThenBy(i => i.Category, comparer).ThenBy(i => i.IsParenthesized ? 0 : 1).ThenBy(i => i.DisplayName, comparer)
            : list.OrderBy(i => i.IsParenthesized ? 0 : 1).ThenBy(i => i.DisplayName, comparer);
        return ordered.ToList();
    }

    /// <summary>
    ///     Orders items and groups them under category headings.
    /// </summary>
    /// <param name="items">The items.</param>
    /// <param name="mode">The sort mode.</param>
    /// <returns>
    ///     For <see cref="PropertySortMode.Categorized"/> one group per category in alphabetical order (categories that
    ///     differ only in case form one group, named like the first item's category); for
    ///     <see cref="PropertySortMode.Alphabetical"/> a single group with a <see langword="null"/> category.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<PropertyGroup> Group(IEnumerable<PropertyItem> items, PropertySortMode mode)
    {
        var ordered = Order(items, mode);
        if (mode == PropertySortMode.Alphabetical)
        {
            return [new PropertyGroup(null, ordered)];
        }

        var groups = new List<PropertyGroup>();
        var comparer = StringComparer.InvariantCultureIgnoreCase;
        var current = new List<PropertyItem>();
        string? category = null;
        foreach (var item in ordered)
        {
            if (category is not null && !comparer.Equals(category, item.Category))
            {
                groups.Add(new PropertyGroup(category, current));
                current = [];
            }

            category = current.Count == 0 ? item.Category : category;
            current.Add(item);
        }

        if (category is not null)
        {
            groups.Add(new PropertyGroup(category, current));
        }

        return groups;
    }
}

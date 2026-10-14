namespace DemaConsulting.Plexus.Avalonia.PropertyGrid.Diagrams;

/// <summary>
///     The values a grid has written into a Plexus source during one commit, before the source publishes them to the
///     diagram element as one undo step.
/// </summary>
internal sealed class WorkingValues
{
    /// <summary>
    ///     The written values by item name.
    /// </summary>
    private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);

    /// <summary>
    ///     Gets the written values.
    /// </summary>
    internal IReadOnlyDictionary<string, object?> Values => _values;

    /// <summary>
    ///     Gets the value written for an item.
    /// </summary>
    /// <param name="name">The item name.</param>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when the item was written.</returns>
    internal bool TryGet(string name, out object? value) => _values.TryGetValue(name, out value);

    /// <summary>
    ///     Records the value written for an item.
    /// </summary>
    /// <param name="name">The item name.</param>
    /// <param name="value">The value.</param>
    internal void Set(string name, object? value) => _values[name] = value;

    /// <summary>
    ///     Forgets every written value.
    /// </summary>
    internal void Clear() => _values.Clear();
}

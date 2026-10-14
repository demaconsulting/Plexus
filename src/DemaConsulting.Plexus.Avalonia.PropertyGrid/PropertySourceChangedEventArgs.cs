namespace DemaConsulting.Plexus.Avalonia.PropertyGrid;

/// <summary>
///     Describes a change reported by an <see cref="IPropertySource"/>.
/// </summary>
public sealed class PropertySourceChangedEventArgs : EventArgs
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="PropertySourceChangedEventArgs"/> class.
    /// </summary>
    /// <param name="propertyName">
    ///     The name of the property whose value changed, or <see langword="null"/> when any value or the set of items
    ///     may have changed.
    /// </param>
    public PropertySourceChangedEventArgs(string? propertyName = null) => PropertyName = propertyName;

    /// <summary>
    ///     Gets the name of the property whose value changed, or <see langword="null"/> when any value or the set of
    ///     items may have changed and everything must be re-read.
    /// </summary>
    public string? PropertyName { get; }

    /// <summary>
    ///     Gets a value indicating whether every item must be re-read.
    /// </summary>
    public bool IsAll => PropertyName is null;
}

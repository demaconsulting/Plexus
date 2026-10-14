namespace DemaConsulting.Plexus;

/// <summary>
///     Identifies what kind of change a <see cref="DiagramChangedEventArgs"/> describes.
/// </summary>
public enum DiagramChangeKind
{
    /// <summary>A page was added to the diagram.</summary>
    PageAdded,

    /// <summary>A page was removed from the diagram.</summary>
    PageRemoved,

    /// <summary>A page changed position in the page list.</summary>
    PageMoved,

    /// <summary>A page was renamed.</summary>
    PageRenamed,

    /// <summary>An element was added to a page.</summary>
    ElementAdded,

    /// <summary>An element was removed from a page.</summary>
    ElementRemoved,

    /// <summary>A property of an element changed; <see cref="DiagramChangedEventArgs.PropertyName"/> names it.</summary>
    ElementChanged,

    /// <summary>A diagram setting (theme, router, connection rules) changed; <see cref="DiagramChangedEventArgs.PropertyName"/> names it.</summary>
    SettingsChanged,
}

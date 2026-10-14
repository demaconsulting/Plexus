namespace DemaConsulting.Plexus;

/// <summary>
///     Describes one change applied to a <see cref="Diagram"/>.
/// </summary>
/// <remarks>
///     Raised for every applied change, including the effects of undo and redo, so painters and
///     controls only need to listen to this one event to know when to repaint.
/// </remarks>
/// <example>
///     React to each kind of change.
///     <code>
///     using DemaConsulting.Plexus;
///
///     var diagram = new Diagram(types);                    // types: the registry of the DemaConsulting.Plexus example
///     diagram.Changed += (_, e) =&gt;
///     {
///         switch (e.Kind)
///         {
///             case DiagramChangeKind.PageAdded:
///             case DiagramChangeKind.PageRemoved:
///             case DiagramChangeKind.PageMoved:
///             case DiagramChangeKind.PageRenamed:
///                 Console.WriteLine($"page list changed: {e.Page?.Name}");           // refresh your page tabs
///                 break;
///             case DiagramChangeKind.ElementAdded:
///             case DiagramChangeKind.ElementRemoved:
///                 Console.WriteLine($"{e.Kind}: {e.Element?.GetType().Name} on {e.Page?.Name}");
///                 break;
///             case DiagramChangeKind.ElementChanged:
///                 Console.WriteLine($"{e.Element?.Id}.{e.PropertyName} changed");   // "Name", "Bounds", "Route", ...
///                 break;
///             case DiagramChangeKind.SettingsChanged:
///                 Console.WriteLine($"setting {e.PropertyName} changed");           // "Theme", "Router", "WireStyler", ...
///                 break;
///         }
///     };
///     </code>
/// </example>
public sealed class DiagramChangedEventArgs : EventArgs
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="DiagramChangedEventArgs"/> class.
    /// </summary>
    /// <param name="kind">What kind of change occurred.</param>
    /// <param name="page">The affected page, if any.</param>
    /// <param name="element">The affected element, if any.</param>
    /// <param name="propertyName">The changed property, if applicable.</param>
    public DiagramChangedEventArgs(DiagramChangeKind kind, DiagramPage? page, DiagramElement? element, string? propertyName)
    {
        Kind = kind;
        Page = page;
        Element = element;
        PropertyName = propertyName;
    }

    /// <summary>
    ///     Gets what kind of change occurred.
    /// </summary>
    public DiagramChangeKind Kind { get; }

    /// <summary>
    ///     Gets the affected page, or null when the change is not page-specific.
    /// </summary>
    public DiagramPage? Page { get; }

    /// <summary>
    ///     Gets the affected element, or null when the change concerns a page or a setting.
    /// </summary>
    public DiagramElement? Element { get; }

    /// <summary>
    ///     Gets the name of the changed property for property and setting changes; otherwise null.
    /// </summary>
    public string? PropertyName { get; }
}

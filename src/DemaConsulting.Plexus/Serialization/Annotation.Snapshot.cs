namespace DemaConsulting.Plexus;

/// <content>
///     Snapshot support: initializing the fill of an annotation that is not attached yet.
/// </content>
public sealed partial class Annotation
{
    /// <summary>
    ///     Stores the fill color without history or notifications.
    /// </summary>
    /// <remarks>
    ///     Used while an annotation is being materialized from a snapshot, clipboard payload or
    ///     duplicate, before it is attached (the public setter requires an attached annotation).
    /// </remarks>
    /// <param name="fill">The fill, or null for the theme's annotation fill.</param>
    internal void InitializeFill(Color? fill) => _fill = fill;
}

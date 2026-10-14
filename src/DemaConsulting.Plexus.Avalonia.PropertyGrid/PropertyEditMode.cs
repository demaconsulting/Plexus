namespace DemaConsulting.Plexus.Avalonia.PropertyGrid;

/// <summary>
///     When a <see cref="PropertyEditSession"/> writes edits to the edited object.
/// </summary>
public enum PropertyEditMode
{
    /// <summary>
    ///     Each edit is written to the target immediately (inside the session's commit scope, so each edit can be one
    ///     undo step).
    /// </summary>
    Live,

    /// <summary>
    ///     Edits are buffered as pending edits without touching the target until
    ///     <see cref="PropertyEditSession.Commit"/>, and discarded by <see cref="PropertyEditSession.Cancel"/>.
    /// </summary>
    Staged,
}

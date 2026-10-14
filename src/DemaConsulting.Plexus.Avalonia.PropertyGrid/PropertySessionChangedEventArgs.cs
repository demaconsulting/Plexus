namespace DemaConsulting.Plexus.Avalonia.PropertyGrid;

/// <summary>
///     What a <see cref="PropertyEditSession.Changed"/> notification reports.
/// </summary>
public enum PropertySessionChangeKind
{
    /// <summary>
    ///     The set of pending edits changed (staged mode).
    /// </summary>
    PendingChanged,

    /// <summary>
    ///     A value was written to the target (live mode).
    /// </summary>
    Applied,

    /// <summary>
    ///     <see cref="PropertyEditSession.Commit"/> finished (some edits may have failed).
    /// </summary>
    Committed,

    /// <summary>
    ///     <see cref="PropertyEditSession.Cancel"/> discarded all pending edits.
    /// </summary>
    Cancelled,

    /// <summary>
    ///     The error of a property was set or cleared.
    /// </summary>
    ErrorChanged,
}

/// <summary>
///     Describes a change of a <see cref="PropertyEditSession"/>.
/// </summary>
public sealed class PropertySessionChangedEventArgs : EventArgs
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="PropertySessionChangedEventArgs"/> class.
    /// </summary>
    /// <param name="kind">What changed.</param>
    /// <param name="path">The path of the affected item, or <see langword="null"/> when several items are affected.</param>
    /// <param name="refreshAll">Whether every item must be re-read.</param>
    internal PropertySessionChangedEventArgs(PropertySessionChangeKind kind, string? path, bool refreshAll)
    {
        Kind = kind;
        Path = path;
        RefreshAll = refreshAll;
    }

    /// <summary>
    ///     Gets what changed.
    /// </summary>
    public PropertySessionChangeKind Kind { get; }

    /// <summary>
    ///     Gets the path of the affected item, or <see langword="null"/> when several items are affected.
    /// </summary>
    public string? Path { get; }

    /// <summary>
    ///     Gets a value indicating whether every item must be re-read: a property annotated
    ///     <c>[RefreshProperties(RefreshProperties.All)]</c> was written, or the session was cancelled.
    /// </summary>
    public bool RefreshAll { get; }
}

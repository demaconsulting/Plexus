namespace DemaConsulting.Plexus;

/// <summary>
///     The persisted state of a whole <see cref="Diagram"/>: a versioned list of page snapshots.
/// </summary>
/// <remarks>
///     Create one with <see cref="Diagram.ToSnapshot"/>, turn it into JSON with
///     <see cref="PlexusJson.Serialize"/> and restore it with <see cref="Diagram.FromSnapshot"/>.
///     Hosts may embed the snapshot (or its JSON) in their own file format; Plexus itself performs no file I/O.
///     Undo history, theme, router and connection rules are configuration or session state and are not stored.
/// </remarks>
public sealed record DiagramSnapshot
{
    /// <summary>
    ///     The only format version this library reads and writes.
    /// </summary>
    public const int CurrentFormatVersion = 1;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DiagramSnapshot"/> class as an empty snapshot of the current format version.
    /// </summary>
    public DiagramSnapshot()
    {
        // Nothing to initialize: every member has a usable default
    }

    /// <summary>
    ///     Gets the format version; readers reject versions they do not know.
    /// </summary>
    public int FormatVersion { get; init; } = CurrentFormatVersion;

    /// <summary>
    ///     Gets the pages in display order.
    /// </summary>
    public IReadOnlyList<PageSnapshot> Pages { get; init; } = [];
}

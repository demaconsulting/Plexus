namespace DemaConsulting.Plexus;

/// <content>
///     Snapshots and duplication: capturing a page and cloning elements within it.
/// </content>
public sealed partial class DiagramPage
{
    /// <summary>
    ///     Captures this page as a snapshot.
    /// </summary>
    /// <remarks>
    ///     Equivalent to the entry for this page in <see cref="Diagram.ToSnapshot"/>. Capturing does not change the page.
    /// </remarks>
    /// <returns>The page snapshot.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a node payload does not match its declared data type or serializes to invalid JSON.</exception>
    public PageSnapshot ToSnapshot() => SnapshotWriter.CapturePage(this);

    /// <summary>
    ///     Clones elements on this page and places the copies at an offset from the originals (undoable as one step).
    /// </summary>
    /// <remarks>
    ///     Intended for ctrl-drag. Uses the same clone path as <see cref="PlexusClipboard"/>: selecting a
    ///     group duplicates its members, wires are duplicated only when both of their nodes are, ids are
    ///     regenerated, node names get a numeric suffix when names must be unique (<c>Axis</c> becomes
    ///     <c>Axis1</c>), and stored wire routes move with their nodes. Node payloads are deep-copied through
    ///     the node type's <see cref="NodeType.DataType"/> or <see cref="NodeType.DataSerializer"/>; payloads
    ///     of types declaring neither are shared by reference with the original. Wires the connection rules
    ///     reject are skipped.
    /// </remarks>
    /// <param name="elements">The elements to duplicate; all must be on this page.</param>
    /// <param name="dx">The horizontal offset of the copies.</param>
    /// <param name="dy">The vertical offset of the copies.</param>
    /// <returns>Every created element in z-order (including group members and wires); the selection to show afterwards.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="elements"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when an element is null or not on this page.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an offset is not finite or exceeds 1e9 in magnitude.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the page is removed from its diagram, or a node payload cannot be serialized.</exception>
    public IReadOnlyList<DiagramElement> Duplicate(IEnumerable<DiagramElement> elements, double dx, double dy)
    {
        ArgumentNullException.ThrowIfNull(elements);
        EnsureAttached();
        EnsureOffset(dx, nameof(dx));
        EnsureOffset(dy, nameof(dy));

        // Resolve the selection first; an empty selection duplicates nothing and records nothing
        var carried = ClipboardSelection.Expand(elements, out var source);
        if (source is null)
        {
            return [];
        }

        if (!ReferenceEquals(source, this))
        {
            throw new ArgumentException("All elements must be on this page.", nameof(elements));
        }

        var content = SnapshotWriter.Capture(carried, keepDataReferences: true);
        var prepared = ElementImporter.Prepare(this, content, new ImportOptions { Dx = dx, Dy = dy });
        if (!prepared.Success)
        {
            // The content came from this very diagram, so this indicates a host bug (for example a type whose payload no longer round-trips)
            throw new InvalidOperationException($"The elements could not be duplicated: {prepared.Error}");
        }

        var created = new List<DiagramElement>();
        Diagram.Perform("Duplicate", plan => created = prepared.Apply(this, plan, exactZOrder: false));
        return created;
    }

    /// <summary>
    ///     Throws when a translation is not a usable finite offset.
    /// </summary>
    /// <param name="value">The offset.</param>
    /// <param name="paramName">The parameter name for the exception.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is not finite or too large.</exception>
    internal static void EnsureOffset(double value, string paramName)
    {
        if (!double.IsFinite(value) || Math.Abs(value) > ElementImporter.MaxCoordinate)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "Offsets must be finite and no larger than 1e9 in magnitude.");
        }
    }
}

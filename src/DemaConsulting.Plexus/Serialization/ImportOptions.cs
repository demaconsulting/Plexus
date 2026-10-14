namespace DemaConsulting.Plexus;

/// <summary>
///     Settings that distinguish restoring a snapshot from pasting or duplicating content.
/// </summary>
/// <remarks>
///     Restoring reproduces the stored state exactly (ids, names, positions, z-order) and treats any
///     inconsistency as an error. Cloning generates fresh ids, makes names unique, translates
///     geometry and degrades gracefully (warnings) where the target definition differs.
/// </remarks>
internal sealed record ImportOptions
{
    /// <summary>
    ///     Gets a value indicating whether the stored state is reproduced exactly (snapshot load) instead of cloned (paste, duplicate).
    /// </summary>
    public bool Restore { get; init; }

    /// <summary>
    ///     Gets the maximum total number of elements accepted.
    /// </summary>
    public int MaxElements { get; init; } = int.MaxValue;

    /// <summary>
    ///     Gets the position the top-left of the content's bounds should land on, or null to use <see cref="Dx"/> and <see cref="Dy"/>.
    /// </summary>
    public Point? At { get; init; }

    /// <summary>
    ///     Gets the horizontal offset applied when <see cref="At"/> is null.
    /// </summary>
    public double Dx { get; init; }

    /// <summary>
    ///     Gets the vertical offset applied when <see cref="At"/> is null.
    /// </summary>
    public double Dy { get; init; }
}

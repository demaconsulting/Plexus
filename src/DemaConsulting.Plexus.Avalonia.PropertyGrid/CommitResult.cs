namespace DemaConsulting.Plexus.Avalonia.PropertyGrid;

/// <summary>
///     The outcome of <see cref="PropertyEditSession.Commit"/>.
/// </summary>
/// <param name="Applied">The number of edits written to the target.</param>
/// <param name="Failed">The edits that could not be applied, each with its <see cref="PendingEdit.Error"/>; they stay pending.</param>
/// <param name="Error">
///     A failure of the commit scope itself (for example the host's undo transaction); <see langword="null"/> when the
///     scope ran normally. When set, no edit counts as committed and all stay pending.
/// </param>
public sealed record CommitResult(int Applied, IReadOnlyList<PendingEdit> Failed, string? Error)
{
    /// <summary>
    ///     Gets a value indicating whether every pending edit was applied.
    /// </summary>
    public bool Succeeded => Error is null && Failed.Count == 0;
}

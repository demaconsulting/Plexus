namespace DemaConsulting.Plexus.Avalonia.PropertyGrid;

/// <summary>
///     The outcome of <see cref="PropertyEditSession.Commit"/>.
/// </summary>
/// <remarks>
///     <para>
///         When a commit succeeds, <see cref="Succeeded"/> is true and <see cref="Applied"/> counts the edits written.
///         When it does not, read <see cref="Failed"/> to learn which rows were the cause: each entry carries the row's
///         <see cref="PendingEdit.Path"/> and the message in <see cref="PendingEdit.Error"/>, so a host can select the row and show the reason
///         without parsing <see cref="Error"/>.
///     </para>
///     <para>
///         A commit through a source that wraps the writes in one undo step, such as the Plexus node source, is atomic: when one row is
///         refused at commit time (a rejected value or a <c>Diagram.NodeRenaming</c> veto) nothing is applied, no undo entry is recorded,
///         <see cref="Applied"/> is 0, every edit stays pending and <see cref="Failed"/> lists the refused row.
///     </para>
/// </remarks>
/// <example>
///     <code>
///     using DemaConsulting.Plexus.Avalonia.PropertyGrid;
///
///     public static class CommitSample
///     {
///         // grid: a staged grid that shows an object
///         public static void Commit(PropertyGrid grid)
///         {
///             CommitResult result = grid.Session!.Commit();
///             if (!result.Succeeded)
///             {
///                 foreach (PendingEdit failure in result.Failed)
///                 {
///                     Console.WriteLine($"{failure.Path}: {failure.Error}");   // for example "Node.Name: Names cannot contain spaces."
///                 }
///             }
///         }
///     }
///     </code>
/// </example>
/// <param name="Applied">The number of edits written to the target.</param>
/// <param name="Failed">The edits that could not be applied, each with its <see cref="PendingEdit.Path"/> and <see cref="PendingEdit.Error"/>; they stay pending. Includes the row a commit scope refused.</param>
/// <param name="Error">
///     A failure of the commit scope itself (for example the host's undo transaction, or a refused row whose message it repeats); <see langword="null"/> when the
///     scope ran normally. When set, no edit counts as committed and all stay pending.
/// </param>
public sealed record CommitResult(int Applied, IReadOnlyList<PendingEdit> Failed, string? Error)
{
    /// <summary>
    ///     Gets a value indicating whether every pending edit was applied.
    /// </summary>
    public bool Succeeded => Error is null && Failed.Count == 0;
}

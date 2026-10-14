namespace DemaConsulting.Plexus.Avalonia.PropertyGrid;

/// <summary>
///     Implemented by an <see cref="IPropertySource"/> that must bracket the writes of every commit, for example to open
///     an undo transaction before the writes and publish a working copy after them. A <see cref="PropertyEditSession"/>
///     calls it around each commit, outside the host's own commit scope.
/// </summary>
/// <remarks>
///     <para>
///         Staged commits call <see cref="RunCommit"/> once for all pending edits; live edits call it once per edit. The
///         implementation must invoke the action it receives exactly once. When it throws, no edit counts as committed;
///         throw a <see cref="PropertyCommitRefusedException"/> to attribute the refusal to one property so the grid shows
///         the reason on that row.
///     </para>
///     <para>
///         This is what lets a source with a working copy (such as the Plexus node adapter) make the whole dialog one
///         undo step no matter which host shows it, so <c>grid.SelectedObject = node</c> needs no commit scope at all.
///     </para>
/// </remarks>
/// <example>
///     <code><![CDATA[
///     public sealed class SettingsSource : IPropertySource, IPropertyCommitScope
///     {
///         // ... items that write into a working copy ...
///
///         public void RunCommit(Action apply)
///         {
///             history.Transact("Edit settings", () =>
///             {
///                 apply();             // the grid writes its pending edits into the working copy
///                 PublishWorkingCopy();   // then the working copy replaces the live object, still inside the transaction
///             });
///         }
///     }
///     ]]></code>
/// </example>
public interface IPropertyCommitScope
{
    /// <summary>
    ///     Runs the writes of one commit.
    /// </summary>
    /// <param name="apply">
    ///     The action that performs the writes; the implementation must invoke it exactly once.
    /// </param>
    /// <exception cref="PropertyCommitRefusedException">The commit was refused because of one property.</exception>
    void RunCommit(Action apply);
}

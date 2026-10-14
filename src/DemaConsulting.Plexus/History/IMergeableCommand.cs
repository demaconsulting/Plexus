namespace DemaConsulting.Plexus;

/// <summary>
///     An <see cref="IUndoableCommand"/> that can absorb a following command inside a transaction.
/// </summary>
/// <remarks>
///     Merging keeps history compact when one gesture (for example a drag) produces many small
///     commands: inside a transaction each new command is offered to the previous one, and if it
///     accepts, only one entry remains. The next command has already been applied when it is offered.
/// </remarks>
public interface IMergeableCommand : IUndoableCommand
{
    /// <summary>
    ///     Offers the next, already-applied command for absorption.
    /// </summary>
    /// <param name="next">The command that was just executed.</param>
    /// <returns>
    ///     <see langword="true"/> when this command now also represents <paramref name="next"/>
    ///     (its Undo reverts both and its Do re-applies both); otherwise <see langword="false"/>.
    /// </returns>
    bool TryMerge(IUndoableCommand next);
}

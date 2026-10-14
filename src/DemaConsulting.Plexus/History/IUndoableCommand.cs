namespace DemaConsulting.Plexus;

/// <summary>
///     A reversible change that can be recorded in an <see cref="UndoHistory"/>.
/// </summary>
/// <remarks>
///     Implementations must make <see cref="Do"/> and <see cref="Undo"/> exact inverses of each
///     other and repeatable: after any sequence of Undo/Do pairs the model must be bit-for-bit
///     identical to the state before the first call. They run on the UI thread only.
/// </remarks>
public interface IUndoableCommand
{
    /// <summary>
    ///     Gets the human-readable description shown in "Undo ..." and "Redo ..." menu items.
    /// </summary>
    string Description { get; }

    /// <summary>
    ///     Applies the change (the first time and on every redo).
    /// </summary>
    void Do();

    /// <summary>
    ///     Reverts the change, restoring the exact prior state.
    /// </summary>
    void Undo();
}

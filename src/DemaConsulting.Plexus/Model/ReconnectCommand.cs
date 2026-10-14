namespace DemaConsulting.Plexus;

/// <summary>
///     Moves a wire to different ports together with the route that fits them, as one reversible step.
/// </summary>
/// <remarks>
///     The ports and the route change together because a route only makes sense for the ports it was
///     computed for; undo restores all three exactly, so the wire keeps its id throughout.
/// </remarks>
internal sealed class ReconnectCommand : IUndoableCommand
{
    /// <summary>
    ///     The wire that is reconnected.
    /// </summary>
    private readonly Wire _wire;

    /// <summary>
    ///     The ports and route before the change.
    /// </summary>
    private readonly (Port Source, Port Target, Point[] Route) _old;

    /// <summary>
    ///     The ports and route after the change.
    /// </summary>
    private readonly (Port Source, Port Target, Point[] Route) _new;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ReconnectCommand"/> class.
    /// </summary>
    /// <param name="wire">The wire to reconnect.</param>
    /// <param name="oldState">The ports and route the wire has now.</param>
    /// <param name="newState">The ports and route the wire should get.</param>
    public ReconnectCommand(Wire wire, (Port Source, Port Target, Point[] Route) oldState, (Port Source, Port Target, Point[] Route) newState)
    {
        _wire = wire;
        _old = oldState;
        _new = newState;
    }

    /// <inheritdoc />
    public string Description => "Reconnect wire";

    /// <inheritdoc />
    public void Do() => _wire.ApplyEnds(_new.Source, _new.Target, _new.Route);

    /// <inheritdoc />
    public void Undo() => _wire.ApplyEnds(_old.Source, _old.Target, _old.Route);
}

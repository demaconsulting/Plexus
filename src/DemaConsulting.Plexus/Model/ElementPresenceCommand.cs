namespace DemaConsulting.Plexus;

/// <summary>
///     Adds or removes one element from a page's lists, remembering the exact positions so the reverse is lossless.
/// </summary>
/// <remarks>
///     Element presence is the primitive behind adding, removing and moving elements between pages.
///     Positions in both the typed list (nodes, wires, ...) and the z-ordered element list are
///     captured the first time the command runs, so undo and redo restore ids, order and z-order exactly.
/// </remarks>
internal sealed class ElementPresenceCommand : IUndoableCommand
{
    /// <summary>
    ///     The page whose lists change.
    /// </summary>
    private readonly DiagramPage _page;

    /// <summary>
    ///     The element added or removed.
    /// </summary>
    private readonly DiagramElement _element;

    /// <summary>
    ///     True when Do adds the element; false when Do removes it.
    /// </summary>
    private readonly bool _add;

    /// <summary>
    ///     Index in the typed list, or -1 for "append" before it is known.
    /// </summary>
    private int _typedIndex = -1;

    /// <summary>
    ///     Index in the z-ordered element list, or -1 for "append" before it is known.
    /// </summary>
    private int _zIndex = -1;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ElementPresenceCommand"/> class.
    /// </summary>
    /// <param name="description">Human-readable description.</param>
    /// <param name="page">The page whose lists change.</param>
    /// <param name="element">The element to add or remove.</param>
    /// <param name="add">True to add on Do; false to remove on Do.</param>
    public ElementPresenceCommand(string description, DiagramPage page, DiagramElement element, bool add)
    {
        Description = description;
        _page = page;
        _element = element;
        _add = add;
    }

    /// <inheritdoc />
    public string Description { get; }

    /// <inheritdoc />
    public void Do()
    {
        if (_add)
        {
            Attach();
        }
        else
        {
            Detach();
        }
    }

    /// <inheritdoc />
    public void Undo()
    {
        if (_add)
        {
            Detach();
        }
        else
        {
            Attach();
        }
    }

    /// <summary>
    ///     Inserts the element at the remembered (or, the first time, final) position.
    /// </summary>
    private void Attach() => (_typedIndex, _zIndex) = _page.AttachCore(_element, _typedIndex, _zIndex);

    /// <summary>
    ///     Removes the element, remembering the position it had.
    /// </summary>
    private void Detach() => (_typedIndex, _zIndex) = _page.DetachCore(_element);
}

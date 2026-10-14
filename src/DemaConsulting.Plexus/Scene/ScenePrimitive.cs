namespace DemaConsulting.Plexus.Scene;

/// <summary>
///     Base class of the painter-agnostic drawing primitives a <see cref="DiagramScene"/> is made of.
/// </summary>
/// <remarks>
///     A primitive knows where it draws (<see cref="Bounds"/>, used for culling and spatial
///     queries) and which model element it belongs to, if any, so painters and hosts can map
///     drawing back to the model. Primitives are immutable once created and are drawn in list
///     order (later primitives paint over earlier ones). The set of primitive kinds is closed:
///     painters handle <see cref="RectPrimitive"/>, <see cref="PathPrimitive"/>,
///     <see cref="TextPrimitive"/>, <see cref="IconPrimitive"/> and <see cref="GridPrimitive"/>.
/// </remarks>
public abstract class ScenePrimitive
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ScenePrimitive"/> class.
    /// </summary>
    /// <param name="bounds">The rectangle the primitive draws in, strokes included.</param>
    /// <param name="element">The owning model element, or null.</param>
    private protected ScenePrimitive(Rect bounds, DiagramElement? element)
    {
        Bounds = bounds;
        Element = element;
    }

    /// <summary>
    ///     Gets the rectangle the primitive draws in, stroke width included.
    /// </summary>
    public Rect Bounds { get; }

    /// <summary>
    ///     Gets the model element the primitive belongs to, or null for decoration such as the grid.
    /// </summary>
    /// <remarks>
    ///     Primitives added by a custom <see cref="INodeVisual"/> without an element are attributed
    ///     to the node they were built for.
    /// </remarks>
    public DiagramElement? Element { get; internal set; }
}

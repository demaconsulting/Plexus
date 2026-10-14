namespace DemaConsulting.Plexus.Routing;

/// <summary>
///     Computes the interior bend points of a wire from its endpoints and the surrounding obstacles.
/// </summary>
/// <remarks>
///     Hosts replace <see cref="Diagram.Router"/> with their own implementation to change how wires
///     are laid out. Implementations must be deterministic for a given request, must not mutate the
///     request and must not depend on UI state, so routing is reproducible in tests and in headless
///     rendering.
///     <para>
///         What a router has to guarantee: nothing beyond returning points. The result is post-processed after the router returns: the diagram
///         removes bends that lie on a straight run between their neighbors and zero-length segments (see
///         <see cref="RouteCleaner"/>), so four collinear points come back as two, or as none for a straight wire. Your route does
///         not have to be orthogonal and does not have to include the stub points, although the standard routers
///         leave and enter ports through stubs of <see cref="RouteRequest.StubLength"/> and orthogonal routes look best with the
///         editor's segment handles. A router that returns null is treated as a straight line. To detect a backward wire (the end lies left of the start) compare
///         <c>request.End.X</c> with <c>request.Start.X</c>.
///     </para>
/// </remarks>
/// <example>
///     A router that always draws a single vertical run halfway between the ports.
///     <code>
///     using DemaConsulting.Plexus;
///     using DemaConsulting.Plexus.Geometry;
///     using DemaConsulting.Plexus.Routing;
///
///     var diagram = new Diagram(new NodeTypeRegistry());
///     diagram.Router = new MidpointRouter();
///
///     public sealed class MidpointRouter : IWireRouter
///     {
///         public IReadOnlyList&lt;Point&gt; Route(RouteRequest request)
///         {
///             var midX = (request.Start.X + request.End.X) / 2;
///             return [new Point(midX, request.Start.Y), new Point(midX, request.End.Y)];   // the ends are not part of the result
///         }
///     }
///     </code>
/// </example>
public interface IWireRouter
{
    /// <summary>
    ///     Computes the interior bend points of one wire for the endpoints, directions and obstacles in a request.
    /// </summary>
    /// <param name="request">The endpoints, directions and obstacles of the wire.</param>
    /// <returns>
    ///     The interior bend points in order from start to end; endpoints themselves are not
    ///     included. An empty list means a straight segment.
    /// </returns>
    IReadOnlyList<Point> Route(RouteRequest request);
}

namespace DemaConsulting.Plexus.Routing;

/// <summary>
///     Wire routing: the router contract, the default orthogonal router and pure route-editing helpers.
/// </summary>
/// <example>
///     Wires are routed automatically when they are connected or their nodes move. Replace or tune the router on the diagram
///     (<c>diagram</c> and <c>page</c> are the objects of the <c>DemaConsulting.Plexus</c> namespace example).
///     <code>
///     using System.Linq;
///     using DemaConsulting.Plexus;
///     using DemaConsulting.Plexus.Geometry;
///     using DemaConsulting.Plexus.Routing;
///
///     diagram.Router = new OverTheTopRouter();                   // wires connected or moved from now on use it
///     // ... or, instead of the line above, tune the default router: fewer bends, longer detours
///     diagram.Router = new OrthogonalRouter { BendPenalty = 40 };  // replaces the router assigned before
///     page.RerouteAll();                                         // re-route the wires that already exist
///
///     // A custom router: go straight unless a node box is in the way, then run over the top of the blockers.
///     public sealed class OverTheTopRouter : IWireRouter
///     {
///         public IReadOnlyList&lt;Point&gt; Route(RouteRequest request)
///         {
///             // Obstacles are node boxes (already inflated by the theme's margin) and include the wire's own end nodes
///             var blockers = request.Obstacles
///                 .Where(o =&gt; !o.Contains(request.Start) &amp;&amp; !o.Contains(request.End))
///                 .Where(o =&gt; o.Intersects(Rect.FromPoints(request.Start, request.End)))
///                 .ToList();
///             if (blockers.Count == 0)
///             {
///                 return [];   // an empty list means a straight segment
///             }
///
///             var top = blockers.Min(o =&gt; o.Top) - request.StubLength;
///             var out1 = request.Start.X + request.StubLength;
///             var in1 = request.End.X - request.StubLength;
///             return [new Point(out1, request.Start.Y), new Point(out1, top), new Point(in1, top), new Point(in1, request.End.Y)];
///         }
///     }
///     </code>
///     <para>
///         A router implements <see cref="IWireRouter"/>: given a <see cref="RouteRequest"/> (start and end anchors,
///         the sides the wire leaves and enters through, obstacle boxes and the segments of other wires) it returns
///         the interior bend points; the endpoints are not included. Routers must be deterministic and must not
///         touch the model. <see cref="OrthogonalRouter"/> is the default and routes around nodes with right-angle
///         bends; <see cref="SimpleRouter"/> is a cheap obstacle-ignoring Z-shaped elbow.
///     </para>
///     <para>
///         Users can also reshape a wire by hand. <see cref="RouteEditor"/> holds the pure operations that keep a
///         route orthogonal (move a segment, move a bend, add a jog) and <see cref="RouteCleaner"/> normalizes a
///         route without changing its drawn shape; <see cref="Wire.SetRoute"/> stores the result. A manually shaped
///         wire keeps its shape until one of its nodes moves.
///     </para>
/// </example>
internal static class NamespaceDoc
{
}

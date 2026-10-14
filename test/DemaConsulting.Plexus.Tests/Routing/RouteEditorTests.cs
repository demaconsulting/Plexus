using DemaConsulting.Plexus.Geometry;
using DemaConsulting.Plexus.Routing;

namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     Unit tests for <see cref="RouteEditor"/>: moving segments and bends and inserting jogs while the wire stays orthogonal.
/// </summary>
public class RouteEditorTests
{
    /// <summary>
    ///     The source anchor of the sample wire.
    /// </summary>
    private static readonly Point Start = new(0, 0);

    /// <summary>
    ///     The target anchor of the sample wire.
    /// </summary>
    private static readonly Point End = new(300, 150);

    /// <summary>
    ///     The interior bends of the sample wire: out along y=0 to x=150, down to y=150, then along to the target.
    /// </summary>
    private static readonly Point[] Route = [new(150, 0), new(150, 150)];

    /// <summary>
    ///     Validates that an interior segment moves perpendicular to itself and its neighbors stretch.
    /// </summary>
    [Fact]
    public void RouteEditor_MoveSegment_InteriorSegment_MovesAndNeighborsStretch()
    {
        // Arrange / Act: drag the vertical middle segment left
        var moved = RouteEditor.MoveSegment(Start, Route, End, 1, 120, 15);

        // Assert
        Assert.Equal([new Point(120, 0), new Point(120, 150)], moved);
    }

    /// <summary>
    ///     Validates that dragging the first stub keeps the port where it is and inserts a jog beside it.
    /// </summary>
    [Fact]
    public void RouteEditor_MoveSegment_FirstStub_InsertsJogNextToSourcePort()
    {
        // Arrange / Act: drag the segment leaving the source down to y=30
        var moved = RouteEditor.MoveSegment(Start, Route, End, 0, 30, 15);

        // Assert: one stub length straight out, a step to y=30, then along the moved line
        Assert.Equal([new Point(15, 0), new Point(15, 30), new Point(150, 30), new Point(150, 150)], moved);
    }

    /// <summary>
    ///     Validates that dragging the last stub inserts a jog next to the target port so the wire still arrives straight.
    /// </summary>
    [Fact]
    public void RouteEditor_MoveSegment_LastStub_InsertsJogNextToTargetPort()
    {
        // Arrange / Act: drag the segment arriving at the target up to y=120
        var moved = RouteEditor.MoveSegment(Start, Route, End, 2, 120, 15);

        // Assert
        Assert.Equal([new Point(150, 0), new Point(150, 120), new Point(285, 120), new Point(285, 150)], moved);
    }

    /// <summary>
    ///     Validates that a straight wire (a single segment between two ports) gets a jog at both ends when dragged.
    /// </summary>
    [Fact]
    public void RouteEditor_MoveSegment_StraightWire_InsertsJogAtBothEnds()
    {
        // Arrange / Act
        var moved = RouteEditor.MoveSegment(new Point(0, 0), [], new Point(300, 0), 0, 30, 15);

        // Assert
        Assert.Equal([new Point(15, 0), new Point(15, 30), new Point(285, 30), new Point(285, 0)], moved);
    }

    /// <summary>
    ///     Validates that dragging a stub back onto its own line inserts nothing.
    /// </summary>
    [Fact]
    public void RouteEditor_MoveSegment_StubToItsOwnLine_LeavesRouteUnchanged()
    {
        // Arrange / Act
        var moved = RouteEditor.MoveSegment(Start, Route, End, 0, 0, 15);

        // Assert
        Assert.Equal(Route, moved);
    }

    /// <summary>
    ///     Validates that moving a segment onto the line of its neighbors merges the bends away.
    /// </summary>
    [Fact]
    public void RouteEditor_MoveSegment_OntoNeighborLine_MergesCollinearBends()
    {
        // Arrange: a bump on a straight wire
        var bumped = RouteEditor.InsertJog(new Point(0, 0), [], new Point(300, 0), 0, new Point(150, 0), 15);
        Assert.NotNull(bumped);

        // Act: drag the bump's middle segment back onto the wire's line
        var flat = RouteEditor.MoveSegment(new Point(0, 0), bumped, new Point(300, 0), 2, 0, 15);

        // Assert: the wire is straight again
        Assert.Empty(flat!);
    }

    /// <summary>
    ///     Validates that moving a bend moves the horizontal segment to the new Y and the vertical segment to the new X.
    /// </summary>
    [Fact]
    public void RouteEditor_MoveBend_Corner_MovesBothAdjacentSegments()
    {
        // Arrange / Act: drag the second bend (150,150), between a vertical and the last horizontal segment
        var moved = RouteEditor.MoveBend(Start, Route, End, 1, new Point(180, 120), 15);

        // Assert: the vertical segment is at x=180, the last segment at y=120, and the last stub gets a jog
        Assert.Equal([new Point(180, 0), new Point(180, 120), new Point(285, 120), new Point(285, 150)], moved);
    }

    /// <summary>
    ///     Validates that moving the bend beside the source stub inserts a jog instead of moving the port.
    /// </summary>
    [Fact]
    public void RouteEditor_MoveBend_NextToStub_InsertsJog()
    {
        // Arrange / Act: drag the first bend (150,0)
        var moved = RouteEditor.MoveBend(Start, Route, End, 0, new Point(210, 45), 15);

        // Assert
        Assert.Equal([new Point(15, 0), new Point(15, 45), new Point(210, 45), new Point(210, 150)], moved);
    }

    /// <summary>
    ///     Validates that the vertical segment behind the source stub cannot be dragged into the stub.
    /// </summary>
    [Fact]
    public void RouteEditor_MoveBend_TowardPort_KeepsOneStubLength()
    {
        // Arrange / Act: drag the first bend far to the left of the port
        var moved = RouteEditor.MoveBend(Start, Route, End, 0, new Point(-60, 0), 15);

        // Assert: the vertical segment stays one stub length from the source
        Assert.Equal([new Point(15, 0), new Point(15, 150)], moved);
    }

    /// <summary>
    ///     Validates that unknown or unsupported edits report null instead of damaging the route.
    /// </summary>
    [Fact]
    public void RouteEditor_Edits_BadIndexOrDiagonalRoute_ReturnNull()
    {
        // Arrange: a route with a diagonal segment
        var diagonal = new[] { new Point(100, 50) };

        // Act / Assert
        Assert.Multiple(
            () => Assert.Null(RouteEditor.MoveSegment(Start, Route, End, 9, 10, 15)),
            () => Assert.Null(RouteEditor.MoveBend(Start, Route, End, 5, new Point(1, 1), 15)),
            () => Assert.Null(RouteEditor.MoveSegment(Start, diagonal, End, 1, 10, 15)),
            () => Assert.Null(RouteEditor.InsertJog(Start, diagonal, End, 0, new Point(50, 25), 15)),
            () => Assert.Null(RouteEditor.InsertJog(Start, Route, End, -1, new Point(0, 0), 15)));
    }

    /// <summary>
    ///     Validates that a jog is a small bump of four bends centered on the requested point.
    /// </summary>
    [Fact]
    public void RouteEditor_InsertJog_HorizontalSegment_AddsCenteredBump()
    {
        // Arrange / Act
        var jogged = RouteEditor.InsertJog(new Point(0, 0), [], new Point(300, 0), 0, new Point(150, 0), 15);

        // Assert: a bump one step (15) down, 15 wide, centered on x=150
        Assert.Equal([new Point(142.5, 0), new Point(142.5, 15), new Point(157.5, 15), new Point(157.5, 0)], jogged);
    }

    /// <summary>
    ///     Validates that a jog near the end of a segment is kept clear of the segment's end.
    /// </summary>
    [Fact]
    public void RouteEditor_InsertJog_NearSegmentEnd_IsClampedAwayFromTheEnd()
    {
        // Arrange / Act: ask for a jog right at the start of the wire
        var jogged = RouteEditor.InsertJog(new Point(0, 0), [], new Point(300, 0), 0, new Point(2, 0), 15)!;

        // Assert: the bump sits one half-width inside the segment
        Assert.Equal(new Point(7.5, 0), jogged[0]);
        Assert.Equal(new Point(22.5, 0), jogged[^1]);
    }

    /// <summary>
    ///     Validates that a jog on a vertical segment steps sideways.
    /// </summary>
    [Fact]
    public void RouteEditor_InsertJog_VerticalSegment_StepsSideways()
    {
        // Arrange / Act: segment 1 of the sample wire is the vertical one at x=150
        var jogged = RouteEditor.InsertJog(Start, Route, End, 1, new Point(150, 75), 15)!;

        // Assert: the new bends are on the vertical segment's line and one step to the right
        var bump = jogged.Skip(1).Take(4).ToList();
        Assert.Equal([new Point(150, 67.5), new Point(165, 67.5), new Point(165, 82.5), new Point(150, 82.5)], bump);
    }
}

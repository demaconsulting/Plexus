using System.Text.Json.Nodes;

namespace DemaConsulting.Plexus.Puppet;

/// <content>
///     Hover gestures: rest the real cursor on a node, a port, a wire or a diagram point so its tooltip opens, optionally
///     taking a screen copy while it shows.
/// </content>
/// <remarks>
///     Every hover first parks the cursor on empty canvas near the target (away from nodes, annotations, groups and
///     wires), then moves onto the target in small steps and rests there for <c>--wait</c> milliseconds. Parking resets
///     the editor's tooltip state, so the tooltip opens fresh even when the cursor was already on the target or had just
///     clicked it. The same safety checks as the other gestures apply: nothing moves unless the target is inside the
///     editor and on the instance's own window.
/// </remarks>
internal static partial class Commands
{
    /// <summary>
    ///     How far around an element the cursor must not park, in diagram units: room for name labels above a node and
    ///     ports and port labels beside it.
    /// </summary>
    private const double HoverObstacleMargin = 30;

    /// <summary>
    ///     How much further below a node the cursor must not park, in diagram units: room for the node's summary lines.
    /// </summary>
    private const double HoverSummaryAllowance = 60;

    /// <summary>
    ///     How far from the target a park point can be, in diagram units (the outermost ring of
    ///     <see cref="HoverPlanner.ParkCandidates"/>), plus a margin; wires farther away than this are ignored.
    /// </summary>
    private const double HoverParkReach = 300;

    /// <summary>
    ///     The most park candidates whose screen position is asked for before going straight to the target.
    /// </summary>
    private const int HoverParkTries = 8;

    /// <summary>
    ///     Hovers the body of a node, which shows the node's tooltip.
    /// </summary>
    /// <param name="parsed">The parsed command: the node name, <c>--wait</c>, <c>--shot</c>.</param>
    /// <returns>The exit code.</returns>
    internal static int HoverNode(ParsedCommand parsed)
    {
        RequireArgs(parsed, 1, "plexus-puppet hover-node <node> [--wait <ms>] [--shot <path.png>] [--instance <alias>]");
        var options = HoverOptions.Parse(parsed);
        using var context = Attach(parsed);
        InputDriver.BringToFront(context);
        var body = NodeBody(context.Alias, parsed.Positional[0]);
        return Hover(context, body, "hover-node", $"node={parsed.Positional[0]}", $"node '{parsed.Positional[0]}'", options);
    }

    /// <summary>
    ///     Hovers a port, which shows the port's tooltip (its label, type and description).
    /// </summary>
    /// <param name="parsed">The parsed command: <c>Node.port</c> (an optional <c>@in</c> or <c>@out</c> suffix picks a side), <c>--wait</c>, <c>--shot</c>.</param>
    /// <returns>The exit code.</returns>
    internal static int HoverPort(ParsedCommand parsed)
    {
        RequireArgs(parsed, 1, "plexus-puppet hover-port <Node.port[@in|@out]> [--wait <ms>] [--shot <path.png>] [--instance <alias>]");
        var options = HoverOptions.Parse(parsed);
        using var context = Attach(parsed);
        InputDriver.BringToFront(context);
        var port = PortPress(context.Alias, parsed.Positional[0], "in");
        return Hover(context, port, "hover-port", $"port={parsed.Positional[0]}", $"port '{parsed.Positional[0]}'", options);
    }

    /// <summary>
    ///     Hovers a wire, which shows the wire's tooltip.
    /// </summary>
    /// <param name="parsed">The parsed command: a wire id, or the source and target ports as <c>Node.port</c>; <c>--wait</c>, <c>--shot</c>.</param>
    /// <returns>The exit code.</returns>
    internal static int HoverWire(ParsedCommand parsed)
    {
        RequireArgs(parsed, 1, "plexus-puppet hover-wire (<wire id> | <Source.port> <Target.port>) [--wait <ms>] [--shot <path.png>] [--instance <alias>]");
        var options = HoverOptions.Parse(parsed);
        using var context = Attach(parsed);
        InputDriver.BringToFront(context);

        // Two ports name the wire that joins them: showing the source port's page first lets list-wires (current page) see it
        string id;
        string subject;
        if (parsed.Positional.Count >= 2)
        {
            var (source, target) = (parsed.Positional[0], parsed.Positional[1]);
            _ = PortPress(context.Alias, source, "out");
            id = HoverPlanner.FindWireId(Call(context.Alias, "list-wires"), source, target)
                ?? throw new PuppetException(ExitCodes.NotFound, "NOT_FOUND", $"No wire joins {source} to {target} (list them with: plexus-puppet wires).");
            subject = $"wire={source}->{target} id={id}";
        }
        else
        {
            id = parsed.Positional[0];
            subject = $"wire={id}";
        }

        // Rest on the middle of the wire's longest segment, the easiest part to hit
        var geometry = Call(context.Alias, "geometry", new JsonObject { ["element"] = id })!;
        if (!string.Equals(geometry["kind"]?.GetValue<string>(), "wire", StringComparison.Ordinal))
        {
            throw new PuppetException(ExitCodes.NotFound, "NOT_FOUND", $"'{id}' is not a wire.");
        }

        TargetPoint point;
        try
        {
            point = HoverPlanner.WirePoint(geometry);
        }
        catch (InvalidOperationException ex)
        {
            throw new PuppetException(ExitCodes.NotFound, "NOT_FOUND", $"Wire '{id}': {ex.Message}.");
        }

        return Hover(context, point, "hover-wire", subject, $"wire '{id}'", options);
    }

    /// <summary>
    ///     Hovers a diagram point (whatever is there, or empty canvas).
    /// </summary>
    /// <param name="parsed">The parsed command: x and y in diagram coordinates, <c>--wait</c>, <c>--shot</c>.</param>
    /// <returns>The exit code.</returns>
    internal static int HoverAt(ParsedCommand parsed)
    {
        RequireArgs(parsed, 2, "plexus-puppet hover-at <diagram-x> <diagram-y> [--wait <ms>] [--shot <path.png>] [--instance <alias>]");
        var x = Number(parsed.Positional[0], "diagram-x");
        var y = Number(parsed.Positional[1], "diagram-y");
        var options = HoverOptions.Parse(parsed);
        using var context = Attach(parsed);
        InputDriver.BringToFront(context);
        var target = DiagramPoint(context.Alias, x, y);
        return Hover(context, target, "hover-at", $"diagram=({x},{y})", $"the point ({x},{y})", options);
    }

    /// <summary>
    ///     Checks the target, parks the cursor on empty canvas, rests it on the target and optionally takes a screen copy.
    /// </summary>
    /// <param name="context">The attached instance, already in front.</param>
    /// <param name="target">Where to rest.</param>
    /// <param name="label">The command name for the result line.</param>
    /// <param name="subject">What was hovered, for the result line.</param>
    /// <param name="what">What the target is, for refusal messages.</param>
    /// <param name="options">The wait and the shot path.</param>
    /// <returns>The exit code.</returns>
    private static int Hover(AttachContext context, TargetPoint target, string label, string subject, string what, HoverOptions options)
    {
        // Refuse before anything moves: the target must be in the editor and on this instance's window
        Guard(context, target.Visible, target.Screen, what);
        var park = FindParkPoint(context, target);

        // Park, arrive in steps and rest; the shot is taken while the cursor still rests (and without raising the
        // window again, which could close the tooltip)
        InputDriver.Hover(park?.Screen, target.Screen, TimeSpan.FromMilliseconds(options.WaitMs));
        var shot = string.Empty;
        if (options.ShotPath is { } path)
        {
            _ = SaveShot(context, path, fromScreen: true, bringToFront: false);
            shot = $" shot=\"{Path.GetFullPath(path)}\"";
        }

        var parked = park is { } p ? $"({p.Screen.PixelX},{p.Screen.PixelY})" : "none";
        return Ok($"OK {label} {subject} at=({target.Screen.PixelX},{target.Screen.PixelY}) park={parked} wait={options.WaitMs}{shot}");
    }

    /// <summary>
    ///     Finds a point on empty canvas near a target, visible and on the instance's window, to park the cursor on first.
    /// </summary>
    /// <remarks>
    ///     The obstacles are the current page's nodes (with room for their names, ports and summaries), annotations and
    ///     groups, and the wires that pass near the target. Candidates come from <see cref="HoverPlanner.FreeParkPoints"/>.
    /// </remarks>
    /// <param name="context">The attached instance.</param>
    /// <param name="target">The target.</param>
    /// <returns>The park point, or <see langword="null"/> when no free, visible point was found.</returns>
    private static TargetPoint? FindParkPoint(AttachContext context, TargetPoint target)
    {
        // Nodes, with room above and beside them for names and ports, and below them for their summary lines
        var nodes = new Dictionary<string, DiagramBox>(StringComparer.Ordinal);
        var obstacles = new List<DiagramBox>();
        foreach (var node in Call(context.Alias, "list-nodes") as JsonArray ?? [])
        {
            if (node?["bounds"] is { } bounds && node["name"]?.GetValue<string>() is { } name)
            {
                var box = ReadBox(bounds);
                nodes[name] = box;
                var grown = box.Inflate(HoverObstacleMargin);
                obstacles.Add(grown with { Height = grown.Height + HoverSummaryAllowance });
            }
        }

        // Annotations and groups show tooltips too
        foreach (var command in new[] { "list-annotations", "list-groups" })
        {
            foreach (var item in Call(context.Alias, command) as JsonArray ?? [])
            {
                if (item?["bounds"] is { } bounds)
                {
                    obstacles.Add(ReadBox(bounds).Inflate(HoverObstacleMargin));
                }
            }
        }

        // Wires near the target: their drawn polylines (the stored route lacks the end points, which the geometry adds)
        var wires = new List<IReadOnlyList<(double X, double Y)>>();
        foreach (var wire in Call(context.Alias, "list-wires") as JsonArray ?? [])
        {
            if (wire?["id"]?.GetValue<string>() is { } id && IsWireNear(wire, nodes, target))
            {
                var points = Call(context.Alias, "geometry", new JsonObject { ["element"] = id })?["points"] as JsonArray ?? [];
                wires.Add([.. points.Select(GeometryReader.ReadPoint).Select(p => (p.DiagramX, p.DiagramY))]);
            }
        }

        // The first free candidate that is visible and really on this instance's window
        foreach (var (x, y) in HoverPlanner.FreeParkPoints(target.DiagramX, target.DiagramY, obstacles, wires).Take(HoverParkTries))
        {
            var candidate = DiagramPoint(context.Alias, x, y);
            if (candidate.Visible && InputDriver.IsOwnedBy(candidate.Screen, context.ProcessId))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    ///     Tests whether a wire could pass within park reach of a target, from its bend points and its end nodes' bounds.
    /// </summary>
    /// <param name="wire">The wire from <c>list-wires</c>.</param>
    /// <param name="nodes">The bounds of the page's nodes by name.</param>
    /// <param name="target">The target.</param>
    /// <returns><see langword="true"/> when the wire's extent, grown by the park reach, contains the target.</returns>
    private static bool IsWireNear(JsonNode wire, Dictionary<string, DiagramBox> nodes, TargetPoint target)
    {
        var xs = new List<double>();
        var ys = new List<double>();
        foreach (var point in wire["route"] as JsonArray ?? [])
        {
            xs.Add(point!["x"]!.GetValue<double>());
            ys.Add(point["y"]!.GetValue<double>());
        }

        foreach (var end in new[] { "source", "target" })
        {
            var reference = wire[end]?.GetValue<string>() ?? string.Empty;
            var dot = reference.LastIndexOf('.');
            if (dot > 0 && nodes.TryGetValue(reference[..dot], out var box))
            {
                xs.AddRange([box.X, box.X + box.Width]);
                ys.AddRange([box.Y, box.Y + box.Height]);
            }
        }

        // Without anything to go by, keep the wire (asking for its geometry is only slower)
        if (xs.Count == 0)
        {
            return true;
        }

        var extent = new DiagramBox(xs.Min(), ys.Min(), xs.Max() - xs.Min(), ys.Max() - ys.Min());
        return extent.Inflate(HoverParkReach).Contains(target.DiagramX, target.DiagramY);
    }

    /// <summary>
    ///     Reads a diagram rectangle (<c>{x, y, width, height}</c>) from a <c>list-*</c> result.
    /// </summary>
    /// <param name="bounds">The rectangle.</param>
    /// <returns>The box.</returns>
    private static DiagramBox ReadBox(JsonNode bounds) => new(
        bounds["x"]!.GetValue<double>(),
        bounds["y"]!.GetValue<double>(),
        bounds["width"]!.GetValue<double>(),
        bounds["height"]!.GetValue<double>());
}

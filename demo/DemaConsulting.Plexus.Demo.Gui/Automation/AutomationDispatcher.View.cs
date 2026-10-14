using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using DemaConsulting.Plexus.Geometry;
using DemaConsulting.Plexus.Scene;
using AvaloniaPoint = global::Avalonia.Point;
using Point = DemaConsulting.Plexus.Geometry.Point;
using Rect = DemaConsulting.Plexus.Geometry.Rect;

namespace DemaConsulting.Plexus.Demo.Gui.Automation;

/// <content>
///     View commands: <c>view</c>, <c>geometry</c>, <c>bring-into-view</c>, <c>zoom-to</c> and <c>tooltip</c>.
/// </content>
/// <remarks>
///     <c>geometry</c> exists so an external driver can use REAL operating-system mouse input: it
///     converts diagram coordinates to physical screen pixels (what <c>SetCursorPos</c> and
///     <c>SendInput</c> expect), taking the window position, the editor's zoom and offset and the
///     monitor scaling into account.
/// </remarks>
internal sealed partial class AutomationDispatcher
{
    /// <summary>
    ///     Registers the view commands.
    /// </summary>
    private void RegisterViewCommands()
    {
        Add("view", request =>
        {
            var editor = Session.Editor;
            if (AutomationJson.OptionalNumber(request, "zoom") is { } zoom)
            {
                editor.Zoom = zoom;
            }

            var offsetX = AutomationJson.OptionalNumber(request, "offsetX");
            var offsetY = AutomationJson.OptionalNumber(request, "offsetY");
            if (offsetX.HasValue || offsetY.HasValue)
            {
                editor.Offset = new AvaloniaPoint(offsetX ?? editor.Offset.X, offsetY ?? editor.Offset.Y);
            }

            if (AutomationJson.OptionalBool(request, "fit") ?? false)
            {
                editor.ZoomToFit();
            }

            return new JsonObject { ["zoom"] = editor.Zoom, ["offsetX"] = editor.Offset.X, ["offsetY"] = editor.Offset.Y };
        });
        Add("geometry", Geometry);

        // Navigation: show one element (switching pages, or showing the collapsed group that hides it), or fit several
        Add("bring-into-view", request =>
        {
            var reference = AutomationJson.OptionalString(request, "element") ?? AutomationJson.OptionalString(request, "node")
                ?? AutomationJson.OptionalString(request, "name")
                ?? throw new ArgumentException("Give 'element', 'node' or 'name'.");
            Session.Editor.BringIntoView(ResolveElement(reference));
            return ViewResult();
        });
        Add("zoom-to", request =>
        {
            Session.Editor.ZoomToFit(ResolveElements(request), AutomationJson.OptionalNumber(request, "margin") ?? 30);
            return ViewResult();
        });

        // The tooltip the editor would show, as the host's handlers left it
        Add("tooltip", request =>
        {
            object? content;
            if (AutomationJson.OptionalString(request, "port") is { } portReference)
            {
                var port = ResolvePort(portReference, preferOutput: false, AutomationJson.OptionalString(request, "direction"));
                content = Session.Editor.GetToolTipContent(port.Node, port);
            }
            else
            {
                var reference = AutomationJson.OptionalString(request, "element") ?? AutomationJson.OptionalString(request, "node")
                    ?? throw new ArgumentException("Give 'element', 'node' or 'port'.");
                content = Session.Editor.GetToolTipContent(ResolveElement(reference));
            }

            return new JsonObject { ["text"] = content?.ToString() };
        });
    }

    /// <summary>
    ///     Describes the editor's view after a navigation command: the shown page and the zoom and offset.
    /// </summary>
    /// <returns><c>{page, zoom, offsetX, offsetY}</c>, where <c>page</c> is the shown page's index.</returns>
    private JsonObject ViewResult()
    {
        var editor = Session.Editor;
        return new JsonObject
        {
            ["page"] = editor.Page is { } page ? Session.Diagram.Pages.IndexOf(page) : -1,
            ["zoom"] = editor.Zoom,
            ["offsetX"] = editor.Offset.X,
            ["offsetY"] = editor.Offset.Y,
        };
    }

    /// <summary>
    ///     Implements <c>geometry</c>: where an element, a port or a node's name is, in diagram and physical screen coordinates.
    /// </summary>
    /// <param name="request">The request with one of <c>node</c>, <c>port</c>, <c>element</c> or <c>editor</c>.</param>
    /// <returns>The result object.</returns>
    private JsonNode? Geometry(JsonObject request)
    {
        var editor = Session.Editor;
        var screen = new ScreenMapper(editor);
        var result = new JsonObject { ["scale"] = screen.Scale, ["zoom"] = editor.Zoom };

        if (AutomationJson.OptionalBool(request, "editor") ?? false)
        {
            result["kind"] = "editor";
            result["bounds"] = screen.EditorRect();
            return result;
        }

        if (request.TryGetPropertyValue("point", out var pointNode) && pointNode is JsonObject pointObject)
        {
            // An arbitrary diagram point, so a driver can aim at empty canvas (rubber band, drop target, wheel anchor)
            result["kind"] = "point";
            result["point"] = screen.Point(new Point(AutomationJson.RequireNumber(pointObject, "x"), AutomationJson.RequireNumber(pointObject, "y")));
            return result;
        }

        if (AutomationJson.OptionalString(request, "port") is { } portReference)
        {
            var port = ResolvePort(portReference, preferOutput: false, AutomationJson.OptionalString(request, "direction"));
            ShowPageOf([port.Node]);
            result["kind"] = "port";
            result["node"] = port.Node.Name;
            result["port"] = DescribePortGeometry(port, screen);
            return result;
        }

        var reference = AutomationJson.OptionalString(request, "node") ?? AutomationJson.OptionalString(request, "element")
            ?? throw new ArgumentException("Give 'node', 'port', 'element' or 'editor'.");
        var element = ResolveElement(reference);
        ShowPageOf([element]);
        result["kind"] = KindOf(element);
        result["id"] = element.Id;
        result["bounds"] = screen.Rect(element.GetBounds());
        switch (element)
        {
            case Node node:
                result["name"] = node.Name;
                DescribeNodeGeometry(node, screen, result);
                break;
            case Wire wire:
                DescribeWireGeometry(wire, screen, result);
                break;
            case Group group:
                DescribeGroupGeometry(group, screen, result);
                break;
            case Annotation annotation:
                // The resize handle sits in the bottom-right corner; aim a few units inside it
                var corner = new Point(annotation.Bounds.Right - 3, annotation.Bounds.Bottom - 3);
                result["resizeHandle"] = screen.Point(corner);
                break;
        }

        return result;
    }

    /// <summary>
    ///     Adds the name rectangle and the ports of a node to a geometry result.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="screen">The coordinate mapper.</param>
    /// <param name="result">The result to extend.</param>
    private void DescribeNodeGeometry(Node node, ScreenMapper screen, JsonObject result)
    {
        result["center"] = screen.Point(node.Bounds.Center);
        if (Session.Editor.Scene is { } scene && scene.TryGetNodeLayout(node, out var layout))
        {
            result["nameRect"] = screen.Rect(layout.Name.Bounds);
            result["title"] = screen.Rect(layout.Title.Bounds);
        }
        else
        {
            // Nodes inside a collapsed group are not drawn, so there is nothing to point at
            result["hidden"] = true;
        }

        result["ports"] = AutomationJson.ToArray(node.Ports, p => DescribePortGeometry(p, screen));
    }

    /// <summary>
    ///     Describes where a port is: its anchor (where wires attach) and a point inside its hit area (where a mouse press starts a connection).
    /// </summary>
    /// <param name="port">The port.</param>
    /// <param name="screen">The coordinate mapper.</param>
    /// <returns>The JSON description.</returns>
    private JsonObject DescribePortGeometry(Port port, ScreenMapper screen)
    {
        var description = new JsonObject
        {
            ["name"] = port.Name,
            ["direction"] = port.Direction == PortDirection.Input ? "in" : "out",
            ["anchor"] = screen.Point(port.Anchor),
        };
        if (Session.Editor.Scene is { } scene && scene.TryGetNodeLayout(port.Node, out var layout) && layout.Ports.FirstOrDefault(p => ReferenceEquals(p.Port, port)) is { } portLayout)
        {
            description["press"] = screen.Point(portLayout.Triangle.Center);
            description["hit"] = screen.Rect(portLayout.HitBounds);
        }

        return description;
    }

    /// <summary>
    ///     Adds the polyline and the segment midpoints of a wire to a geometry result.
    /// </summary>
    /// <param name="wire">The wire.</param>
    /// <param name="screen">The coordinate mapper.</param>
    /// <param name="result">The result to extend.</param>
    private void DescribeWireGeometry(Wire wire, ScreenMapper screen, JsonObject result)
    {
        IReadOnlyList<Point> points = Session.Editor.Scene is { } scene && scene.TryGetWirePoints(wire, out var drawn) ? drawn : wire.GetPoints();
        result["points"] = AutomationJson.ToArray(points, screen.Point);

        // A segment's midpoint is the natural place to grab a wire to drag that segment
        var middles = new List<Point>();
        for (var i = 0; i + 1 < points.Count; i++)
        {
            middles.Add(new Point((points[i].X + points[i + 1].X) / 2, (points[i].Y + points[i + 1].Y) / 2));
        }

        result["segmentMidpoints"] = AutomationJson.ToArray(middles, screen.Point);
    }

    /// <summary>
    ///     Adds the drawn frame, title bar and collapse toggle of a group to a geometry result, as the scene published them.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <param name="screen">The coordinate mapper.</param>
    /// <param name="result">The result to extend.</param>
    private void DescribeGroupGeometry(Group group, ScreenMapper screen, JsonObject result)
    {
        result["collapsed"] = group.IsCollapsed;
        if (Session.Editor.Scene is { } scene && scene.TryGetGroupLayout(group, out var layout))
        {
            result["bounds"] = screen.Rect(layout.Frame);
            result["header"] = screen.Rect(layout.Header);
            result["toggle"] = screen.Rect(layout.CollapseToggle);
        }
        else
        {
            // Groups inside a collapsed group are not drawn, so there is nothing to point at
            result["hidden"] = true;
        }
    }
    /// <summary>
    ///     Converts editor-relative and diagram coordinates to physical screen pixels.
    /// </summary>
    private readonly struct ScreenMapper
    {
        /// <summary>
        ///     The editor.
        /// </summary>
        private readonly global::DemaConsulting.Plexus.Avalonia.DiagramEditor _editor;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ScreenMapper"/> struct.
        /// </summary>
        /// <param name="editor">The editor whose view defines the mapping.</param>
        public ScreenMapper(global::DemaConsulting.Plexus.Avalonia.DiagramEditor editor)
        {
            _editor = editor;
            Scale = TopLevel.GetTopLevel(editor)?.RenderScaling ?? 1;
        }

        /// <summary>
        ///     Gets the monitor scaling (physical pixels per device-independent pixel).
        /// </summary>
        public double Scale { get; }

        /// <summary>
        ///     Maps a diagram point to a JSON object holding both coordinate systems.
        /// </summary>
        /// <param name="diagram">The point in diagram coordinates.</param>
        /// <returns><c>{diagram:{x,y}, screen:{x,y}, visible}</c>.</returns>
        public JsonObject Point(Point diagram)
        {
            var dip = _editor.DiagramToScreen(new AvaloniaPoint(diagram.X, diagram.Y));
            var pixel = _editor.PointToScreen(dip);
            return new JsonObject
            {
                ["diagram"] = AutomationJson.FromPoint(diagram),
                ["screen"] = new JsonObject { ["x"] = pixel.X, ["y"] = pixel.Y },
                ["visible"] = Contains(dip),
            };
        }

        /// <summary>
        ///     Maps a diagram rectangle to a JSON object holding both coordinate systems.
        /// </summary>
        /// <param name="diagram">The rectangle in diagram coordinates.</param>
        /// <returns><c>{diagram:{x,y,width,height}, screen:{x,y,width,height,centerX,centerY}, visible}</c>.</returns>
        public JsonObject Rect(Rect diagram)
        {
            var topLeft = _editor.DiagramToScreen(new AvaloniaPoint(diagram.Left, diagram.Top));
            var pixel = _editor.PointToScreen(topLeft);
            var width = diagram.Width * _editor.Zoom * Scale;
            var height = diagram.Height * _editor.Zoom * Scale;
            var center = _editor.DiagramToScreen(new AvaloniaPoint(diagram.Center.X, diagram.Center.Y));
            var centerPixel = _editor.PointToScreen(center);
            return new JsonObject
            {
                ["diagram"] = AutomationJson.FromRect(diagram),
                ["screen"] = new JsonObject
                {
                    ["x"] = pixel.X,
                    ["y"] = pixel.Y,
                    ["width"] = width,
                    ["height"] = height,
                    ["centerX"] = centerPixel.X,
                    ["centerY"] = centerPixel.Y,
                },
                ["visible"] = Contains(center),
            };
        }

        /// <summary>
        ///     Describes the editor control itself in screen pixels.
        /// </summary>
        /// <returns><c>{screen:{x,y,width,height}}</c>.</returns>
        public JsonObject EditorRect()
        {
            var pixel = _editor.PointToScreen(new AvaloniaPoint(0, 0));
            return new JsonObject
            {
                ["screen"] = new JsonObject
                {
                    ["x"] = pixel.X,
                    ["y"] = pixel.Y,
                    ["width"] = _editor.Bounds.Width * Scale,
                    ["height"] = _editor.Bounds.Height * Scale,
                },
            };
        }

        /// <summary>
        ///     Tests whether an editor-relative point lies inside the editor.
        /// </summary>
        /// <param name="dip">The point in device-independent pixels relative to the editor.</param>
        /// <returns>True when the point is inside the control's bounds.</returns>
        private bool Contains(AvaloniaPoint dip) =>
            dip.X >= 0 && dip.Y >= 0 && dip.X <= _editor.Bounds.Width && dip.Y <= _editor.Bounds.Height;
    }
}

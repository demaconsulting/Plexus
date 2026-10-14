using System.Text.Json.Nodes;
using DemaConsulting.Plexus.Puppet.Automation;
using FlaUI.Core.Input;

namespace DemaConsulting.Plexus.Puppet;

/// <content>
///     Real-mouse gestures: node and port gestures, group toggles, palette drags and canvas gestures.
/// </content>
/// <remarks>
///     Each gesture asks the demo's <c>geometry</c> command where things are (physical screen pixels), brings the window
///     to the front and then injects real operating-system input with <see cref="InputDriver"/>. The model is changed by
///     the real editor, not by the automation pipe, so these commands exercise hit testing, gestures and history exactly
///     as a user would.
/// </remarks>
internal static partial class Commands
{
    /// <summary>
    ///     Drags a node by a diagram offset.
    /// </summary>
    /// <param name="parsed">The parsed command: the node name, dx and dy in diagram units.</param>
    /// <returns>The exit code.</returns>
    internal static int DragNode(ParsedCommand parsed) => DragNodeBy(parsed, KeyChordModifiers.None, "drag-node");

    /// <summary>
    ///     Drags a copy of a node by a diagram offset (Ctrl held).
    /// </summary>
    /// <param name="parsed">The parsed command: the node name, dx and dy in diagram units.</param>
    /// <returns>The exit code.</returns>
    internal static int CtrlDragNode(ParsedCommand parsed) => DragNodeBy(parsed, KeyChordModifiers.Control, "ctrl-drag-node");

    /// <summary>
    ///     Double-clicks the body of a node.
    /// </summary>
    /// <param name="parsed">The parsed command: the node name.</param>
    /// <returns>The exit code.</returns>
    internal static int DoubleClickNode(ParsedCommand parsed) =>
        ClickNodeBody(parsed, "double-click-node", MouseButton.Left, KeyChordModifiers.None, 2);

    /// <summary>
    ///     Right-clicks the body of a node.
    /// </summary>
    /// <param name="parsed">The parsed command: the node name.</param>
    /// <returns>The exit code.</returns>
    internal static int RightClickNode(ParsedCommand parsed) =>
        ClickNodeBody(parsed, "right-click-node", MouseButton.Right, KeyChordModifiers.None, 1);

    /// <summary>
    ///     Clicks the body of a node, optionally with Shift or Ctrl held.
    /// </summary>
    /// <param name="parsed">The parsed command: the node name, <c>--shift</c>, <c>--ctrl</c>.</param>
    /// <returns>The exit code.</returns>
    internal static int ClickNode(ParsedCommand parsed)
    {
        var modifiers = (parsed.HasOption("shift") ? KeyChordModifiers.Shift : KeyChordModifiers.None)
            | (parsed.HasOption("ctrl") ? KeyChordModifiers.Control : KeyChordModifiers.None);
        return ClickNodeBody(parsed, "click-node", MouseButton.Left, modifiers, 1);
    }

    /// <summary>
    ///     Double-clicks the name label of a node, which starts an inline rename.
    /// </summary>
    /// <param name="parsed">The parsed command: the node name.</param>
    /// <returns>The exit code.</returns>
    internal static int DoubleClickName(ParsedCommand parsed)
    {
        RequireArgs(parsed, 1, "plexus-puppet double-click-name <node> [--instance <alias>]");
        using var context = Attach(parsed);
        InputDriver.BringToFront(context);
        var geometry = Call(context.Alias, "geometry", new JsonObject { ["node"] = parsed.Positional[0] })!;
        var name = GeometryReader.ReadRect(geometry["nameRect"] ?? throw new PuppetException(ExitCodes.NotFound, "NOT_FOUND", $"Node '{parsed.Positional[0]}' has no drawn name label."));
        var at = name.Screen.Center;
        Guard(context, name.Visible, at, $"the name of '{parsed.Positional[0]}'");
        InputDriver.Click(at, MouseButton.Left, KeyChordModifiers.None, 2);
        return Ok($"OK double-click-name node={parsed.Positional[0]} at=({at.PixelX},{at.PixelY})");
    }

    /// <summary>
    ///     Drags from one port to another to create a wire.
    /// </summary>
    /// <param name="parsed">The parsed command: <c>Node.port</c> twice (an optional <c>@in</c> or <c>@out</c> suffix picks a side).</param>
    /// <returns>The exit code.</returns>
    internal static int DragConnect(ParsedCommand parsed)
    {
        RequireArgs(parsed, 2, "plexus-puppet drag-connect <Node.port[@in|@out]> <Node.port[@in|@out]> [--instance <alias>]");
        using var context = Attach(parsed);
        InputDriver.BringToFront(context);
        var from = PortPress(context.Alias, parsed.Positional[0], "out");
        var to = PortPress(context.Alias, parsed.Positional[1], "in");
        Guard(context, from.Visible, from.Screen, $"port '{parsed.Positional[0]}'");
        Guard(context, to.Visible, to.Screen, $"port '{parsed.Positional[1]}'");
        InputDriver.Drag(from.Screen, to.Screen, KeyChordModifiers.None);
        return Ok($"OK drag-connect from={parsed.Positional[0]} to={parsed.Positional[1]} screen=({from.Screen.PixelX},{from.Screen.PixelY})->({to.Screen.PixelX},{to.Screen.PixelY})");
    }

    /// <summary>
    ///     Clicks the collapse toggle of a group.
    /// </summary>
    /// <param name="parsed">The parsed command: the group title or id.</param>
    /// <returns>The exit code.</returns>
    internal static int ClickToggle(ParsedCommand parsed)
    {
        RequireArgs(parsed, 1, "plexus-puppet click-toggle <group title or id> [--instance <alias>]");
        using var context = Attach(parsed);
        InputDriver.BringToFront(context);
        var geometry = Call(context.Alias, "geometry", new JsonObject { ["element"] = parsed.Positional[0] })!;
        var toggle = GeometryReader.ReadRect(geometry["toggle"] ?? throw new PuppetException(ExitCodes.NotFound, "NOT_FOUND", $"'{parsed.Positional[0]}' is not a visible group (it has no collapse toggle)."));
        var at = toggle.Screen.Center;
        Guard(context, toggle.Visible, at, $"the toggle of '{parsed.Positional[0]}'");
        InputDriver.Click(at, MouseButton.Left, KeyChordModifiers.None);
        return Ok($"OK click-toggle group={parsed.Positional[0]} at=({at.PixelX},{at.PixelY})");
    }

    /// <summary>
    ///     Drags a palette entry onto the canvas.
    /// </summary>
    /// <param name="parsed">The parsed command: the type (a full key like <c>demo.audio.Mixer</c> or its last part), the drop point in diagram coordinates, and optionally <c>--hold-shot</c> (a screen copy taken at the drop point before the button is released, which shows the drop ghost).</param>
    /// <returns>The exit code.</returns>
    internal static int DragPalette(ParsedCommand parsed)
    {
        RequireArgs(parsed, 3, "plexus-puppet drag-palette <type, e.g. Mixer or demo.audio.Mixer> <diagram-x> <diagram-y> [--hold-shot <path.png>] [--instance <alias>]");
        var type = parsed.Positional[0];
        var x = Number(parsed.Positional[1], "diagram-x");
        var y = Number(parsed.Positional[2], "diagram-y");
        using var context = Attach(parsed);
        InputDriver.BringToFront(context);
        var id = ResolvePaletteId(context, type);
        var item = ElementFinder.FindById(context, id)
            ?? throw new PuppetException(ExitCodes.NotFound, "NOT_FOUND", ElementFinder.BuildNotFoundMessage(context, id));
        var palette = ElementFinder.FindById(context, PaletteIds.Palette);

        // The palette scrolls and clips its items: ask the entry to scroll itself into view before reading where it is
        RevealPaletteItem(item, palette);
        var bounds = item.BoundingRectangle;
        var from = new ScreenPoint(bounds.X + (bounds.Width / 2.0), bounds.Y + (bounds.Height / 2.0));
        var drop = DiagramPoint(context.Alias, x, y);

        // Still outside the palette's bounds means the entry could not be revealed (an older host without the pattern)
        if (palette is not null && !palette.BoundingRectangle.Contains(from.PixelX, from.PixelY))
        {
            throw PuppetException.Obscured($"palette entry '{id}' is scrolled out of the palette and could not be scrolled into view; type part of its name in the {PaletteIds.Filter} box (or scroll the palette) so it is visible, then retry.");
        }

        Guard(context, true, from, $"palette entry '{id}'");
        Guard(context, drop.Visible, drop.Screen, $"the drop point ({x},{y})");
        InputDriver.Drag(from, drop.Screen, KeyChordModifiers.None, parsed.GetOption("hold-shot") is { } shot ? () => SaveShot(context, shot, fromScreen: true) : null);
        return Ok($"OK drag-palette type={type} drop=({x},{y}) screen=({drop.Screen.PixelX},{drop.Screen.PixelY})");
    }

    /// <summary>
    ///     Scrolls a palette entry into view with the UI Automation ScrollItem pattern, and waits until it lies inside the palette.
    /// </summary>
    /// <param name="item">The palette entry.</param>
    /// <param name="palette">The palette control, or null when it could not be found.</param>
    private static void RevealPaletteItem(FlaUI.Core.AutomationElements.AutomationElement item, FlaUI.Core.AutomationElements.AutomationElement? palette)
    {
        // Entries that are already visible need no scrolling
        if (palette is null || IsInside(palette, item))
        {
            return;
        }

        if (item.Patterns.ScrollItem.TryGetPattern(out var scrollItem))
        {
            scrollItem.ScrollIntoView();
        }

        // The scroll takes effect at the next layout pass, so poll briefly for the new position
        for (var attempt = 0; attempt < 20 && !IsInside(palette, item); attempt++)
        {
            Thread.Sleep(50);
        }
    }

    /// <summary>
    ///     Determines whether the center of an element lies inside another element's bounds.
    /// </summary>
    /// <param name="container">The containing element.</param>
    /// <param name="item">The element to test.</param>
    /// <returns><see langword="true"/> when the item's center is inside the container.</returns>
    private static bool IsInside(FlaUI.Core.AutomationElements.AutomationElement container, FlaUI.Core.AutomationElements.AutomationElement item)
    {
        var bounds = item.BoundingRectangle;
        return container.BoundingRectangle.Contains((int)(bounds.X + (bounds.Width / 2.0)), (int)(bounds.Y + (bounds.Height / 2.0)));
    }

    /// <summary>
    ///     Finds the AutomationId of a palette entry: <c>NodePalette_</c> plus the type key with punctuation turned into underscores.
    /// </summary>
    /// <param name="context">The attached instance.</param>
    /// <param name="type">A full type key, or just its last part (for example <c>Mixer</c>).</param>
    /// <returns>The AutomationId; for a short name the single palette entry that ends with it.</returns>
    /// <exception cref="PuppetException">Thrown when a short name matches several entries.</exception>
    private static string ResolvePaletteId(AttachContext context, string type)
    {
        // Only a short name needs the ids that exist, so the tree is not walked for a full key
        var existing = type.Contains('.')
            ? []
            : context.Roots().SelectMany(root => UiTreeDumper.CollectAutomationIds(root, 12));
        return PaletteIds.Resolve(type, existing);
    }

    /// <summary>
    ///     Clicks a diagram point (empty canvas or whatever is there).
    /// </summary>
    /// <param name="parsed">The parsed command: x and y in diagram coordinates, <c>--shift</c>, <c>--ctrl</c>.</param>
    /// <returns>The exit code.</returns>
    internal static int ClickAt(ParsedCommand parsed)
    {
        RequireArgs(parsed, 2, "plexus-puppet click-at <diagram-x> <diagram-y> [--shift|--ctrl] [--double] [--instance <alias>]");
        var x = Number(parsed.Positional[0], "diagram-x");
        var y = Number(parsed.Positional[1], "diagram-y");
        var modifiers = (parsed.HasOption("shift") ? KeyChordModifiers.Shift : KeyChordModifiers.None)
            | (parsed.HasOption("ctrl") ? KeyChordModifiers.Control : KeyChordModifiers.None);
        using var context = Attach(parsed);
        InputDriver.BringToFront(context);
        var target = DiagramPoint(context.Alias, x, y);
        Guard(context, target.Visible, target.Screen, $"the point ({x},{y})");
        InputDriver.Click(target.Screen, MouseButton.Left, modifiers, parsed.HasOption("double") ? 2 : 1);
        return Ok($"OK click-at diagram=({x},{y}) screen=({target.Screen.PixelX},{target.Screen.PixelY})");
    }

    /// <summary>
    ///     Drags between two diagram points (a rubber band on empty canvas, or a drag of whatever is at the start).
    /// </summary>
    /// <param name="parsed">The parsed command: x1 y1 x2 y2 in diagram coordinates, <c>--shift</c>, <c>--ctrl</c>.</param>
    /// <returns>The exit code.</returns>
    internal static int DragAt(ParsedCommand parsed)
    {
        RequireArgs(parsed, 4, "plexus-puppet drag-at <x1> <y1> <x2> <y2> (diagram coordinates) [--shift|--ctrl] [--instance <alias>]");
        var x1 = Number(parsed.Positional[0], "x1");
        var y1 = Number(parsed.Positional[1], "y1");
        var x2 = Number(parsed.Positional[2], "x2");
        var y2 = Number(parsed.Positional[3], "y2");
        var modifiers = (parsed.HasOption("shift") ? KeyChordModifiers.Shift : KeyChordModifiers.None)
            | (parsed.HasOption("ctrl") ? KeyChordModifiers.Control : KeyChordModifiers.None);
        using var context = Attach(parsed);
        InputDriver.BringToFront(context);
        var from = DiagramPoint(context.Alias, x1, y1);
        var to = DiagramPoint(context.Alias, x2, y2);
        Guard(context, from.Visible, from.Screen, $"the start ({x1},{y1})");
        Guard(context, to.Visible, to.Screen, $"the end ({x2},{y2})");
        InputDriver.Drag(from.Screen, to.Screen, modifiers);
        return Ok($"OK drag-at diagram=({x1},{y1})->({x2},{y2}) screen=({from.Screen.PixelX},{from.Screen.PixelY})->({to.Screen.PixelX},{to.Screen.PixelY})");
    }

    /// <summary>
    ///     Drags a control (for example the palette splitter) by a screen offset, pressing at the center of its bounds.
    /// </summary>
    /// <param name="parsed">The parsed command: the AutomationId, dx and dy in physical screen pixels.</param>
    /// <returns>The exit code.</returns>
    internal static int DragElement(ParsedCommand parsed)
    {
        RequireArgs(parsed, 3, "plexus-puppet drag-element <automationId> <dx> <dy> (physical screen pixels) [--instance <alias>]");
        var id = parsed.Positional[0];
        var dx = Number(parsed.Positional[1], "dx");
        var dy = Number(parsed.Positional[2], "dy");
        using var context = Attach(parsed);
        InputDriver.BringToFront(context);
        var element = ElementFinder.FindById(context, id)
            ?? throw new PuppetException(ExitCodes.NotFound, "NOT_FOUND", ElementFinder.BuildNotFoundMessage(context, id));
        var bounds = element.BoundingRectangle;
        var from = new ScreenPoint(bounds.X + (bounds.Width / 2.0), bounds.Y + (bounds.Height / 2.0));
        var to = new ScreenPoint(from.X + dx, from.Y + dy);
        Guard(context, true, from, $"element '{id}'");
        Guard(context, true, to, "the drop position");
        InputDriver.Drag(from, to, KeyChordModifiers.None);
        return Ok($"OK drag-element id={id} delta=({dx},{dy}) screen=({from.PixelX},{from.PixelY})->({to.PixelX},{to.PixelY})");
    }

    /// <summary>
    ///     Turns the mouse wheel over the canvas (with Ctrl it zooms around the cursor).
    /// </summary>
    /// <param name="parsed">The parsed command: the notches (positive up), <c>--ctrl</c>, <c>--at x,y</c> in diagram coordinates.</param>
    /// <returns>The exit code.</returns>
    internal static int Wheel(ParsedCommand parsed)
    {
        RequireArgs(parsed, 1, "plexus-puppet wheel <notches (+up/-down)> [--ctrl] [--at <diagram-x>,<diagram-y>] [--instance <alias>]");
        var notches = (int)Number(parsed.Positional[0], "notches");
        using var context = Attach(parsed);
        InputDriver.BringToFront(context);
        TargetPoint target;
        if (parsed.GetOption("at") is { } at)
        {
            var parts = at.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
            {
                throw PuppetException.Usage("--at needs '<diagram-x>,<diagram-y>'.");
            }

            target = DiagramPoint(context.Alias, Number(parts[0], "--at x"), Number(parts[1], "--at y"));
        }
        else
        {
            var editor = Call(context.Alias, "geometry", new JsonObject { ["editor"] = true })!;
            var rect = GeometryReader.ReadRect(new JsonObject
            {
                ["diagram"] = new JsonObject { ["x"] = 0, ["y"] = 0, ["width"] = 0, ["height"] = 0 },
                ["screen"] = editor["bounds"]!["screen"]!.DeepClone(),
            });
            target = new TargetPoint(0, 0, rect.Screen.Center, true);
        }

        Guard(context, target.Visible, target.Screen, "the wheel position");
        InputDriver.Wheel(target.Screen, notches, parsed.HasOption("ctrl") ? KeyChordModifiers.Control : KeyChordModifiers.None);
        return Ok($"OK wheel notches={notches} ctrl={parsed.HasOption("ctrl")} at=({target.Screen.PixelX},{target.Screen.PixelY})");
    }

    /// <summary>
    ///     Drags a node's body by a diagram offset with the given modifiers held.
    /// </summary>
    /// <param name="parsed">The parsed command: the node name, dx and dy.</param>
    /// <param name="modifiers">The modifiers to hold.</param>
    /// <param name="label">The command name for messages.</param>
    /// <returns>The exit code.</returns>
    private static int DragNodeBy(ParsedCommand parsed, KeyChordModifiers modifiers, string label)
    {
        RequireArgs(parsed, 3, $"plexus-puppet {label} <node> <dx> <dy> (diagram units) [--instance <alias>]");
        var dx = Number(parsed.Positional[1], "dx");
        var dy = Number(parsed.Positional[2], "dy");
        using var context = Attach(parsed);
        InputDriver.BringToFront(context);
        var body = NodeBody(context.Alias, parsed.Positional[0]);
        var end = DiagramPoint(context.Alias, body.DiagramX + dx, body.DiagramY + dy);
        Guard(context, body.Visible, body.Screen, $"node '{parsed.Positional[0]}'");
        Guard(context, true, end.Screen, "the drop position");
        InputDriver.Drag(body.Screen, end.Screen, modifiers);
        return Ok($"OK {label} node={parsed.Positional[0]} delta=({dx},{dy}) screen=({body.Screen.PixelX},{body.Screen.PixelY})->({end.Screen.PixelX},{end.Screen.PixelY})");
    }

    /// <summary>
    ///     Clicks the body of a node.
    /// </summary>
    /// <param name="parsed">The parsed command: the node name.</param>
    /// <param name="label">The command name for messages.</param>
    /// <param name="button">The mouse button.</param>
    /// <param name="modifiers">The modifiers to hold.</param>
    /// <param name="clicks">The number of clicks.</param>
    /// <returns>The exit code.</returns>
    private static int ClickNodeBody(ParsedCommand parsed, string label, MouseButton button, KeyChordModifiers modifiers, int clicks)
    {
        RequireArgs(parsed, 1, $"plexus-puppet {label} <node> [--instance <alias>]");
        using var context = Attach(parsed);
        InputDriver.BringToFront(context);
        var body = NodeBody(context.Alias, parsed.Positional[0]);
        Guard(context, body.Visible, body.Screen, $"node '{parsed.Positional[0]}'");
        InputDriver.Click(body.Screen, button, modifiers, clicks);
        return Ok($"OK {label} node={parsed.Positional[0]} at=({body.Screen.PixelX},{body.Screen.PixelY})");
    }

    /// <summary>
    ///     Asks where to press on a node's body.
    /// </summary>
    /// <param name="alias">The instance alias.</param>
    /// <param name="name">The node name.</param>
    /// <returns>The point, in both coordinate systems.</returns>
    private static TargetPoint NodeBody(string alias, string name)
    {
        var geometry = Call(alias, "geometry", new JsonObject { ["node"] = name })!;
        try
        {
            return GeometryReader.BodyPoint(geometry);
        }
        catch (InvalidOperationException ex)
        {
            throw new PuppetException(ExitCodes.NotFound, "NOT_FOUND", $"Node '{name}': {ex.Message}.");
        }
    }

    /// <summary>
    ///     Asks where a diagram point is on the screen.
    /// </summary>
    /// <param name="alias">The instance alias.</param>
    /// <param name="x">The diagram x coordinate.</param>
    /// <param name="y">The diagram y coordinate.</param>
    /// <returns>The point, in both coordinate systems.</returns>
    private static TargetPoint DiagramPoint(string alias, double x, double y)
    {
        var geometry = Call(alias, "geometry", new JsonObject { ["point"] = new JsonObject { ["x"] = x, ["y"] = y } })!;
        return GeometryReader.ReadPoint(geometry["point"]);
    }

    /// <summary>
    ///     Asks where to press to start a connection at a port.
    /// </summary>
    /// <param name="alias">The instance alias.</param>
    /// <param name="reference"><c>Node.port</c>, optionally followed by <c>@in</c> or <c>@out</c>.</param>
    /// <param name="preferred">The side to try first when the reference names none (<c>out</c> for a source, <c>in</c> for a target).</param>
    /// <returns>The point inside the port's hit area.</returns>
    private static TargetPoint PortPress(string alias, string reference, string preferred)
    {
        var at = reference.LastIndexOf('@');
        var path = at < 0 ? reference : reference[..at];
        var explicitSide = at < 0 ? null : reference[(at + 1)..];

        // Some nodes (the flow meter) have a port of the same name on both sides: prefer the side the role needs, else let the demo decide
        JsonNode geometry;
        try
        {
            geometry = Call(alias, "geometry", new JsonObject { ["port"] = path, ["direction"] = explicitSide ?? preferred })!;
        }
        catch (PuppetException) when (explicitSide is null)
        {
            geometry = Call(alias, "geometry", new JsonObject { ["port"] = path })!;
        }

        return GeometryReader.PortPressPoint(geometry["port"]!);
    }

    /// <summary>
    ///     Refuses to send real input unless the target is visible and on the instance's own window.
    /// </summary>
    /// <param name="context">The attached instance.</param>
    /// <param name="visible">Whether the geometry says the target is inside the editor.</param>
    /// <param name="at">The target pixel.</param>
    /// <param name="what">What the target is, for the message.</param>
    /// <exception cref="PuppetException">Thrown with <see cref="ExitCodes.Obscured"/> when the input would not land on the instance.</exception>
    private static void Guard(AttachContext context, bool visible, ScreenPoint at, string what)
    {
        if (!visible)
        {
            throw PuppetException.Obscured($"{what} is scrolled out of the editor; bring it into view first (for example: plexus-puppet pipe view '{{\"fit\":true}}').");
        }

        // Another window (typically the terminal running the Puppet) may have taken the foreground back since
        // BringToFront: try again a few times before giving up
        for (var attempt = 0; attempt < 3 && !InputDriver.IsOwnedBy(at, context.ProcessId); attempt++)
        {
            InputDriver.BringToFront(context);
            Thread.Sleep(200);
        }

        if (!InputDriver.IsOwnedBy(at, context.ProcessId))
        {
            throw PuppetException.Obscured($"{what} at ({at.PixelX},{at.PixelY}) is on {InputDriver.DescribeOwner(at)}, not on instance '{context.Alias}' (covered by another window, off screen or the desktop is locked). No input was sent.");
        }
    }
}

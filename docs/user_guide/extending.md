# Extending Plexus

Almost everything in Plexus has a default that works and a hook that replaces it. This chapter covers the hooks of the
model and the painters that a host reaches for after the basics: custom node visuals, the scene and hit testing, node
data serializers, host-defined undoable commands, a custom clipboard and text measurement. The hooks for routing,
connection rules, styling and renaming are covered in the chapters where they belong.

## Custom Node Visuals

Nodes are drawn by a standard template (header with the type title, icon, ports, name, property summary) driven by the
node type. To add to or replace it, assign an `INodeVisual` to `NodeType.Visual`. A visual does not draw: it adds
painter-agnostic *primitives* (rectangles, paths, text, icons) to a context, so a custom visual renders identically in
the headless renderer and the Avalonia editor.

The smallest visual keeps the standard look and adds to it:

```csharp
using DemaConsulting.Plexus.Scene;

public sealed class BadgeVisual : INodeVisual
{
    public void Build(NodeVisualContext context)
    {
        StandardNodeVisual.Instance.Build(context);                      // the standard node first
        var box = context.Layout.Box;
        context.Add(new RectPrimitive(new Rect(box.Right - 14, box.Top - 4, 18, 18), Color.Red, null, 0, 9, context.Node));
    }
}

types.Add(new NodeType("math.Alarm") { Title = "ALARM", Visual = new BadgeVisual() });
```

To replace only some parts, derive from `StandardNodeVisual`: it builds a node from virtual parts (`AddGlow`, `AddBox`,
`AddTitle`, `AddIcon`, `AddName`, `AddSummary`, `AddPorts`) and a derived visual overrides the ones it wants. This one
draws a pill-shaped box and keeps everything else:

```csharp
public sealed class PillVisual : StandardNodeVisual
{
    protected override void AddBox(NodeVisualContext context) =>
        context.Add(new RectPrimitive(context.Layout.Box, context.Fill, context.Border, context.BorderThickness, 20, context.Node));
}

types.Add(new NodeType("math.Pill") { Title = "PILL", Visual = new PillVisual() });
```

The demo's logic gates are drawn this way: `GateVisual` derives from `StandardNodeVisual`, replaces the box with the
gate symbol (paths with cubic curves) and the title with centered text, and leaves the icon and the glow out, while the
instance name, the property summary and the port triangles with their labels still come from the standard parts. The
gate is drawn inside the node's own box, so hit testing, selection and wire attachment work unchanged.

A visual can honor the style that a `NodeStyler` produced: `context.Style` is the resolved `NodeStyle` (never null),
and `Fill`, `Border`, `BorderThickness` and `HeaderTextStyle` give the styled value or the theme fallback with the
opacity applied; `Faded(color)` applies the opacity to any other color. `StandardNodeVisual` reads them in its parts,
and `AddGlow` draws the halo. `context.Layout` holds the computed geometry (box, title, icon, name, ports, summary),
`context.Theme` the theme and `context.PortColor(port)` the color of a port's type.

## The Scene and Hit Testing

The flow is model, then scene, then painter. `SceneBuilder.Build(page, options)` creates a `DiagramScene`: an ordered
list of primitives plus the geometry needed for hit testing and layout queries. The headless renderer and the Avalonia
editor paint the same scene, and hit testing answers from the very geometry that was drawn.

```csharp
var scene = SceneBuilder.Build(page, new SceneOptions { ShowGrid = false });
HitResult hit = scene.HitTest(new Point(250, 90), tolerance: 4);
if (hit.Kind == HitKind.Node)
{
    ShowMessage($"Over {hit.Node!.Name}");
}
```

A `HitResult` says what is under a point (`HitKind` is one of Node, NodeName, Port, Wire, WireBend, WireSourceHandle,
WireTargetHandle, Annotation, AnnotationResizeHandle, Group, GroupHeader, GroupCollapseToggle, CollapsedGroup or
Canvas), with the element, the port, the wire's segment index or the bend index where they apply. The handles are drawn
and hit **only for selected elements**: a bend or an end of a wire is reported as `WireBend`, `WireSourceHandle` or
`WireTargetHandle` only when the wire is in `SceneOptions.Selection` (and the resize handle of an annotation as
`AnnotationResizeHandle` only for a selected annotation); an unselected wire is hit as a plain `Wire` with its segment
index. A headless test or tool that wants to grab a handle builds the scene with the wire selected,
`new SceneOptions { Selection = [wire] }`, and hit tests that scene. The scene also
answers `TryGetNodeLayout`, `TryGetGroupLayout` and `TryGetWirePoints` (the polyline a wire is displayed with), `Query`
(the primitives in a region) and `Bounds`. In the Avalonia editor, `Editor.HitTest(point)` takes a point relative to the
control and `Editor.Scene` is the scene it currently shows, so a host can build overlays and drop targets on the same
geometry:

```csharp
var under = Editor.HitTest(new Avalonia.Point(120, 80));
if (under.Kind == HitKind.Port)
{
    ShowMessage($"Port {under.Port}");
}
```

Text measurement goes through the `ITextMeasurer` interface. The core provides an approximate measurer (the default for
`SceneOptions.TextMeasurer`), the rendering package measures with CanvasNet fonts (`CanvasTextMeasurer`) and the Avalonia
package with Avalonia text APIs (`AvaloniaTextMeasurer`, which the editor uses). Port anchors and node boxes never
depend on the measurer, but the name, label and summary rectangles do, so pass the painter's measurer in
`SceneOptions` when a hit test must agree with the pixels. Hosts that draw diagrams themselves can consume the scene
directly: every primitive is plain data (`RectPrimitive`, `PathPrimitive`, `TextPrimitive`, `IconPrimitive`,
`GridPrimitive`).

## Host Data Serializers

`NodeType.DataType` serializes a node's `Data` with `System.Text.Json` defaults. When the payload needs other settings
(camel-case names, custom converters, another JSON library), supply an `INodeDataSerializer` instead; it takes
precedence:

```csharp
using System.Text.Json;

public sealed record ValveData(double Opening, string Label);

public sealed class ValveSerializer : INodeDataSerializer
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public string Serialize(object data) => JsonSerializer.Serialize((ValveData)data, Options);

    public object? Deserialize(string json) => JsonSerializer.Deserialize<ValveData>(json, Options);
}

types.Add(new NodeType("plumbing.Valve") { DataSerializer = new ValveSerializer() });
```

The JSON that `Deserialize` receives is untrusted input, because it comes from files and clipboards. Returning `null`
or throwing `JsonException`, `FormatException`, `InvalidOperationException`, `ArgumentException`,
`NotSupportedException` or `InvalidCastException` makes the payload unreadable: loading a file throws
`SnapshotException`, and a paste inserts the node without data and adds a warning to the `PasteResult`.

## Host-Defined Commands

State outside the diagram joins the undo history with `History.Execute("description", do, undo)` (see *Undo, Redo and
Dirty Tracking*). A richer operation implements `IUndoableCommand`, whose `Do` and `Undo` must be exact inverses and
repeatable:

```csharp
public sealed class SetTagCommand(Dictionary<string, string> tags, string key, string value) : IUndoableCommand
{
    private string? _previous;

    public string Description => $"Set tag {key}";

    public void Do()
    {
        tags.TryGetValue(key, out _previous);
        tags[key] = value;
    }

    public void Undo()
    {
        if (_previous is null) tags.Remove(key); else tags[key] = _previous;
    }
}

diagram.History.Execute(new SetTagCommand([], "owner", "me"));
```

A command that can absorb a following command inside a transaction implements `IMergeableCommand`, which keeps the
history compact when one gesture produces many small commands.

## A Custom Clipboard

The editor copies and pastes through the operating-system clipboard by default. Replace it with an
`IDiagramClipboard` for a private in-application clipboard, a remote session or a test double; the interface has two
methods and exchanges the JSON text of `PlexusClipboard`:

```csharp
using DemaConsulting.Plexus.Avalonia;

public sealed class InMemoryClipboard : IDiagramClipboard
{
    private string? _json;

    public Task SetAsync(string json) { _json = json; return Task.CompletedTask; }

    public Task<string?> GetAsync() => Task.FromResult(_json);     // null: nothing to paste
}

Editor.Clipboard = new InMemoryClipboard();
```

## Icons in Your Own Controls

`PlexusIcons.GetImage(icon)` returns an Avalonia `IImage` for an `IconSource`, loading and caching it on first use, so a
host's own lists, menus and tool windows can show the same icons as the nodes:

```csharp
if (types.Get("math.Add").Icon is { } icon)
{
    var image = PlexusIcons.GetImage(icon);     // cached after the first call; null when the SVG cannot be loaded
}
```

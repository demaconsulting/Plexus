# Describing Your Domain

The host declares its vocabulary in a `NodeTypeRegistry`: the kinds of data that flow through ports (port types) and
the kinds of nodes a user can place (node types). The registry is configuration that the host creates once at start-up
and gives to every diagram. Plexus ships no node types of its own.

## Port Types

A port type is a host-chosen string key with a color. Ports of a type are drawn with its color, and so are the wires
leaving them when a wire styler asks for it (see *Appearance: Themes and Styling*). Nothing is predefined; a key that
was never registered still renders, in neutral gray.

```csharp
types.PortTypes.Add(new PortType("number", Color.Parse("#FF8C00")));   // orange
types.PortTypes.Add(new PortType("bool", Color.Parse("#2EBF5B")));     // green
```

`Color.Parse` accepts `#RRGGBB` and `#AARRGGBB`; `Color.FromRgb`, `Color.FromArgb` and named constants such as
`Color.Orange` exist as well. The demo declares five port types (number, bool, fluid, audio and flow), one for each of
its domains, so that the colors tell the domains apart at a glance.

## Node Types

A node type is declared with an object initializer. Only the key is required; everything else has a sensible default.

```csharp
types.Add(new NodeType("math.Sine")
{
    Title = "SINE",                   // the header text; defaults to the last segment of the key
    DisplayName = "Sine wave",        // the host-facing name used by palettes, menus and accessibility
    Category = "Math",                // the palette groups types under category headers
    DefaultSize = new Size(90, 105),  // otherwise the theme's default size, grown to fit the ports
    Icon = IconSource.FromSvg("<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 16 16'>" +
                              "<path d='M1 8 C4 0 7 0 8 8 S12 16 15 8' fill='none' stroke='#FFB04D'/></svg>"),
    Outputs = { new PortDefinition("value", "number") },
    Properties =
    {
        new PropertyDefinition("Amplitude", 1.0) { ShowInSummary = true },
        new PropertyDefinition("Frequency", 1.0) { Unit = "Hz", ShowInSummary = true },
    },
});
```

Notes:

- The key is an opaque string, namespaced by convention (`com.acme.Amplifier`, `demo.math.Add`). It is written to
  files and to the clipboard, so choose keys that never change; a paste into another application requires an exact
  match.
- A type is frozen when it is added to the registry: its port and property lists cannot change afterwards.
- `NodeType.Category` is `null` for an uncategorized type. The `NodePalette` shows categories in registration order and
  uncategorized types last.
- `IsResizable` is a hint for hosts that let users resize nodes of a type; the editor does not resize nodes itself.
- Every registry is independent. Two instances of one application may know different types, which is exactly what
  makes the clipboard failure path testable (see *Saving, Loading and the Clipboard*).

## Ports

Inputs are drawn on the left edge and outputs on the right edge, top to bottom in declaration order. A port has a
`Name` (used in code and in `"Node.port"` paths), a type key and a `Label` (the text drawn beside it, which defaults to
the name).

```csharp
types.Add(new NodeType("audio.Mixer")
{
    Title = "MIXER",
    Inputs =
    {
        new PortDefinition("in", "number") { AllowsMultipleWires = true },   // fan-in: several wires may end here
        new PortDefinition("gain", "number") { Label = "Gain (dB)" },        // the name is for code, the label for people
    },
    Outputs = { new PortDefinition("out", "number") },
});
```

An input accepts one wire unless its definition says `AllowsMultipleWires = true`; outputs always fan out. Port names
must be unique within a direction. An input and an output of one node may share a name, in which case
`node.Port("flow")` is ambiguous and throws: use `node.Input("flow")` or `node.Output("flow")`. Wires are covered in
*Wires, Routing and Connection Rules*.

## Properties

Properties are the typed values a user edits on a node. The kind of a property is inferred from the default value, and
a string property with `Choices` becomes a choice property.

| Default value          | Kind      | A host editor typically uses |
|------------------------|-----------|------------------------------|
| `true`                 | boolean   | a check box                  |
| `3`, `3L`              | integer   | a numeric box                |
| `1.5`                  | double    | a numeric box                |
| `"text"`               | string    | a text box                   |
| `"Fast"` with `Choices`| choice    | a combo box                  |
| `Color.Red`            | color     | a color picker               |

```csharp
types.Add(new NodeType("math.Clamp")
{
    Title = "CLAMP",
    Inputs = { new PortDefinition("value", "number") },
    Outputs = { new PortDefinition("result", "number") },
    Properties =
    {
        new PropertyDefinition("Min", 0.0) { ShowInSummary = true },
        new PropertyDefinition("Max", 1.0) { ShowInSummary = true, Unit = "V" },
        new PropertyDefinition("Mode", "Hard") { Choices = ["Hard", "Soft"], Description = "How limits apply" },
        new PropertyDefinition("Enabled", true) { Category = "Behavior" },
    },
});
```

Presentation members (`DisplayName`, `Category`, `Description`, `Unit`, `EditorHint`, `IsReadOnly`, `Format`) are hints
for the property grid, the host's own property editor and for the node summary; they never restrict programmatic
changes. `ShowInSummary` puts `Name: value unit` in the text drawn under the node, and `Format` customizes how a
value is written there.

A node's values live in its `PropertyBag`. Reading and writing are one-liners; every write is validated against the
declaration, converted where lossless and recorded as an undo entry:

```csharp
a.Properties["Value"] = 4.0;                            // ArgumentException when the value does not fit the kind
double value = a.Properties.Get<double>("Value");
a.Properties.Reset("Value");                            // back to the declared default (undoable)
a.Properties.PropertyChanged += (_, e) => ShowMessage($"{e.PropertyName} changed");
```

Properties that were never declared are *ad hoc*: they can be set and read like declared ones and are saved with the
node, but have no definition to validate against. A definition parses the text a user typed with
`definition.TryParse(text, out value, out error)` and formats a value for an edit box with
`PropertyDefinition.FormatForEdit(value)`; `node.Properties.TrySetAll(...)` applies a whole dialog at once (see *Editing
Properties* in *The Avalonia Editor*).

## Icons

An icon is SVG supplied by the host; Plexus ships no icon library. Keep icons within the common SVG subset (paths,
basic shapes, fills, strokes, groups) so the headless renderer and the Avalonia editor look alike. A malformed icon is
skipped rather than failing the render.

```csharp
var inline = IconSource.FromSvg("<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 16 16'>...</svg>");
var fromFile = IconSource.FromSvgFile("icons/sine.svg");                     // read immediately
var embedded = IconSource.FromSvgResource(System.Reflection.Assembly.GetExecutingAssembly(), "MyApp.Icons.sine.svg");
```

The demo embeds its icons so that it works from any working directory: the project file contains
`<EmbeddedResource Include="Icons\*.svg" LogicalName="Icons.%(Filename)%(Extension)" />` and the node types ask for
`Icons.Sine.svg`. Without `LogicalName` the resource name is the root namespace followed by the folder path and file
name (`MyApp.Icons.sine.svg`); `assembly.GetManifestResourceNames()` lists the real names. An icon can be recolored per
node with `NodeStyle.IconTint` (see *Appearance: Themes and Styling*).

## Summary Lines

By default a node shows the properties marked `ShowInSummary`. A node type can compute its own lines instead; the
function runs every time the scene is built, so the text follows property changes.

```csharp
types.Add(new NodeType("math.Gain")
{
    Title = "GAIN",
    Properties = { new PropertyDefinition("Gain", 1.0) },
    Summary = node => [$"x {node.Properties.Get<double>("Gain"):0.##}"],
});
```

## Host Data

Properties are flat, typed and editable by the generic machinery. When a node needs a richer payload, attach any
application object to `Node.Data`. Declare the exact CLR type as `NodeType.DataType` and the payload survives the
clipboard and JSON files (serialized with `System.Text.Json`; no type name is ever read from a document).

```csharp
public sealed record TaskDetails(string Notes, int Priority);

types.Add(new NodeType("flow.Task")
{
    Title = "TASK",
    Inputs = { new PortDefinition("in", "flow") { AllowsMultipleWires = true } },
    Outputs = { new PortDefinition("out", "flow") },
    DataType = typeof(TaskDetails),
});

var task = page.AddNode("flow.Task", 0, 300);
task.Data = new TaskDetails("Review the invoice", 4);    // undoable; replaces the whole payload
var details = (TaskDetails?)task.Data;
```

Assigning `Data` replaces the payload and is one undo step, so prefer immutable records: change a payload by assigning
a modified copy (`task.Data = details with { Priority = 5 }`). A type without `DataType` or `DataSerializer` has its
`Data` left out of files and of the clipboard. The demo's workflow `Task` node does exactly this, with a mutable class
whose properties carry `System.ComponentModel` annotations: the property grid (see *The Property Grid*) shows and edits
such host data next to the declared properties, working on a copy made with `node.CloneData()`.

![The workflow sample: tasks, decisions and approvals joined by flow wires](images/workflow.png)

A custom serializer for payloads that need other JSON settings is described in *Extending Plexus*.

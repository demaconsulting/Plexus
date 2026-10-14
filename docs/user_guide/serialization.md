# Serialization and Custom File Formats

Plexus Core does no file I/O and prescribes no file format: the host owns the document. What Plexus provides is a
complete, versioned, format-neutral description of a diagram, the *snapshot*, a JSON syntax for it and an equivalent
tree of plain values for every other serializer. A host either
uses the JSON as its file format or, as most real applications do, writes the tree into a format of its own, next to
the host's own objects. This chapter covers the built-in JSON path, a complete TOML example with the `Tomlyn` library,
the host's own data inside and beside the diagram, and the rules for keeping files readable over time.

## The Built-in Path: JSON

For a whole diagram the quick path is two calls:

```csharp
File.WriteAllText("diagram.json", diagram.ToJson());                       // ToJson(indented: true) by default
Diagram loaded = Diagram.FromJson(File.ReadAllText("diagram.json"), types, options);    // options is optional: see below
```

There are stream forms too (`diagram.ToJson(stream)` and `Diagram.FromJson(stream, types, options)`, UTF-8, the stream
stays open), which is what platform file pickers hand out.

`FromJson` needs a registry holding every node type the file uses: the node types themselves are not stored, because
they are the host's vocabulary. The loaded diagram has an empty, clean undo history, and it has no theme, router,
rules or stylers of its own unless you pass a `DiagramOptions`, because configuration is never saved (see
*Configuration* in *Saving, Loading and the Clipboard*).

### What a Snapshot Contains

`Diagram.ToSnapshot()` captures the document as plain, immutable records, and `Diagram.FromSnapshot()` rebuilds a
diagram from them. `ToJson` and `FromJson` are shorthand for these two combined with `PlexusJson.Serialize` and
`PlexusJson.Deserialize`. The records are the JSON document's object model and carry `System.Text.Json` types
(`PropertySnapshot.Value` is a `JsonElement`, `NodeSnapshot.Data` is JSON text), so they suit hosts that write JSON.
For any other format use the *tree* below, which has the same schema without those types.

- `DiagramSnapshot`: the `FormatVersion` and the `Pages`.
- `PageSnapshot`: the `Name`, `Nodes`, `Wires`, `Annotations`, `Groups` and `ZOrder` (ids, bottom-most first).
- `NodeSnapshot`: `Id`, `TypeKey`, `Name`, `Bounds`, `Properties` and `Data` (the host payload as JSON text).
- `PropertySnapshot`: the `Kind` and the `Value`, which is a `JsonElement`.
- `WireSnapshot`: `Id`, the source and target node ids and port names, and the `Route` (the interior bend points).
  Port lists are not part of the snapshot: loading recomputes each node's ports from its properties and data (see
  *Ports That Vary per Node* in *Describing Your Domain*) before it resolves the wires' port names.
- `AnnotationSnapshot`: `Id`, `Text`, `Bounds` and `Fill` (`#AARRGGBB`, or `null` for the theme's fill).
- `GroupSnapshot`: `Id`, `Title`, `MemberIds`, `IsCollapsed` and `CollapsedPosition`.

Elements refer to each other by id: a wire names its two nodes, a group names its members, and `ZOrder` lists ids. Ids,
names, bounds, stored wire routes (the router is not consulted when loading), z-order, collapsed groups, property
values and node payloads are restored exactly. The JSON uses camelCase member names, `"#AARRGGBB"` colors and no type
names; a property is written as `{"kind": "double", "value": 2.5}` so that an `int` stays an `int` and a `long` a
`long`.

`Diagram.FromJson` and `FromSnapshot` throw `SnapshotException` for a snapshot they cannot restore (an unsupported
format version, unknown node types, invalid content); its `MissingNodeTypes` lists every unknown key at once (to open
such a file anyway, see *Files With Unknown Node Types* in *Saving, Loading and the Clipboard*). Text that is not valid
snapshot JSON throws `System.Text.Json.JsonException`.

### The Format-Neutral Tree

`diagram.ToTree()` returns the same document as plain values: a dictionary (`IDictionary<string, object?>`) whose
values are only `string`, `bool`, `long`, `double`, `null`, lists (`IList<object?>`) and nested dictionaries. Host data
is nested values too, colors are `"#AARRGGBB"` strings and property kinds are the plain strings `bool`, `int`, `long`,
`double`, `string`, `choice` and `color`. The schema is exactly that of the JSON, so there is one description of the
format, and any serializer that can write dictionaries and lists can write a diagram. `Diagram.FromTree(tree, types,
options)` reads it back with the same validation as `FromJson`.

```csharp
IDictionary<string, object?> tree = diagram.ToTree();         // a copy: changing it does not change the diagram
Diagram loaded = Diagram.FromTree(tree, types, options);
```

`FromTree` accepts what other libraries produce: any integer or floating-point type, `IDictionary`, `IReadOnlyDictionary`
or any sequence of key/value pairs for objects (a TOML table, for example), any list type, and `JsonElement` values. A
tree it cannot use (an unsupported value such as a date, a non-string key, nesting that is too deep or a wrong shape)
throws `SnapshotException`, so a host needs no JSON knowledge to handle it.

## Writing Your Own Format

When the file format is a project decision (TOML or YAML for hand-editable and diff-friendly files, a database, an
archive with several parts), do not translate the diagram yourself: hand its *tree* to your serializer. The tree is the
complete model, so a format adapter has two jobs, writing the tree in the target syntax and reading it back, and
everything else (validation, ids, routes, groups, node payloads, z-order) stays Plexus's business.

The example uses [Tomlyn](https://github.com/xoofx/Tomlyn), a TOML library for .NET (NuGet package `Tomlyn`, version
2.10.1 at the time of writing):

```bash
dotnet add package Tomlyn
```

Tomlyn reads a table of plain values into a `Dictionary<string, object?>`, which `FromTree` accepts as it is. Writing needs
one small step, because Tomlyn writes only its own `TomlTable`, `TomlArray` and `TomlTableArray` for untyped values and
not generic lists. The converter below makes them out of the tree (a list of dictionaries becomes the `[[sections]]` of
TOML); it names no member of the snapshot, so it keeps working when a later version of Plexus adds members:

```csharp
using Tomlyn.Model;

static class TomlTree
{
    public static Dictionary<string, object?> ToToml(IDictionary<string, object?> tree) =>
        tree.ToDictionary(p => p.Key, p => Convert(p.Value));

    static object? Convert(object? value) => value switch
    {
        IDictionary<string, object?> map => Table(map),
        IList<object?> { Count: > 0 } list when list.All(i => i is IDictionary<string, object?>) => TableArray(list),
        IList<object?> list => Array(list),
        _ => value,
    };

    static TomlTable Table(IDictionary<string, object?> map)
    {
        var table = new TomlTable();
        foreach (var (key, value) in map) table[key] = Convert(value)!;
        return table;
    }

    static TomlTableArray TableArray(IList<object?> list)
    {
        var tables = new TomlTableArray();
        foreach (var item in list) tables.Add(Table((IDictionary<string, object?>)item!));
        return tables;
    }

    static TomlArray Array(IList<object?> list)
    {
        var array = new TomlArray();
        foreach (var item in list) array.Add(Convert(item));
        return array;
    }
}
```

With the converter, a diagram is one TOML table inside a file of your own:

```csharp
using System.Text.Json;
using Tomlyn;

static class TomlStore
{
    static readonly TomlSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static void Save(string path, ProjectFile file, Diagram diagram)
    {
        file.Diagram = TomlTree.ToToml(diagram.ToTree());
        File.WriteAllText(path, TomlSerializer.Serialize(file, Options));
    }

    public static (ProjectFile File, Diagram Diagram) Load(string path, NodeTypeRegistry types)
    {
        var file = TomlSerializer.Deserialize<ProjectFile>(File.ReadAllText(path), Options)!;
        return (file, Diagram.FromTree(file.Diagram, types));
    }
}
```

That is the whole adapter: two lines of host code per direction on top of the converter. `ProjectFile` is the host's
document class, described next. The property naming policy makes Tomlyn read and write `project`, `targets` and
`diagram` as lower-case keys; without it the keys follow the CLR names exactly and are case-sensitive.

## Your Own Objects in the File

A real document holds more than the diagram: per-node data of the host's own types, and objects that are not part of
Plexus at all.

### Per-Node Host Data

A node's `Data` is the host's own object (see *Describing Your Domain*). Declare its exact type on the node type and it
is part of the document, as nested values in the tree (JSON text in `NodeSnapshot.Data`). The converter above turns it
into a TOML table with the members of the record as keys, so the host's payload is as readable in the file as the rest
of it:

```csharp
public sealed record TaskDetails(string Notes, int Priority);

types.Add(new NodeType("flow.Task")
{
    Title = "TASK",
    Inputs = { new PortDefinition("in", "flow") { AllowsMultipleWires = true } },
    DataType = typeof(TaskDetails),                  // serialized with System.Text.Json defaults
});
```

When the payload needs other serializer settings (camel-case members, converters, another library) implement
`INodeDataSerializer` as shown in *Extending Plexus*. Its `Serialize` must return valid JSON, which the document embeds
as it is, so a custom serializer works with the adapter unchanged: whatever JSON it writes becomes a TOML table, and
what the table is turned back into is the text its `Deserialize` receives. The JSON form is also what makes the
payload independent of the target format: the TOML file never contains a CLR type name.

### Document-Level Objects

Objects that are not part of Plexus (project metadata, a list of build targets, user settings) are ordinary classes.
Put the diagram in a `Dictionary<string, object?>` member of the host's document class and Tomlyn does the rest:

```csharp
public sealed class ProjectInfo
{
    public string Name { get; set; } = "";
    public string Owner { get; set; } = "";
    public int SchemaVersion { get; set; } = 1;
}

public sealed class BuildTarget
{
    public string Name { get; set; } = "";
    public string Runtime { get; set; } = "";
    public bool Enabled { get; set; } = true;
}

public sealed class ProjectFile
{
    public ProjectInfo Project { get; set; } = new();
    public List<BuildTarget> Targets { get; set; } = [];
    public Dictionary<string, object?> Diagram { get; set; } = new();       // the Plexus part: the tree of a diagram
}
```

Saving and loading a project is then `TomlStore.Save("project.toml", file, diagram)` and
`var (file, diagram) = TomlStore.Load("project.toml", types)`. Tomlyn writes the host's tables and the diagram as
ordinary TOML sections (the order is Tomlyn's), and a node's host data is a table of its own:

```toml
[project]
name = "Amplifier"
owner = "Acme"
schemaVersion = 1

[diagram]
formatVersion = 1

[[diagram.pages]]
name = "Main"

[[diagram.pages.nodes]]
id = "f19ac6e90b2f49d888d103f28345203c"
typeKey = "flow.Task"
name = "T1"

[diagram.pages.nodes.data]
Notes = "Review the\ninvoice"
Priority = 4

[[targets]]
name = "desktop"
runtime = "win-x64"
enabled = true
```

The excerpt leaves out the bounds, properties, wires, annotations and groups of the page.

The document class, not Plexus, decides what else lives in the file, so the same pattern holds for any number of host
objects: add members to `ProjectFile`. A host that must preserve keys it does not know (written by a newer version of
its own application) keeps them in a dictionary member as well.

## Shaping the File

The generic adapter writes the tree as it is, which includes the `kind` of every property:

```toml
[diagram.pages.nodes.properties.Value]
kind = "double"
value = 2.5
```

TOML values already carry a type, so a file meant to be edited by hand is nicer with `Value = 2.5` and nothing else.
The kind can be recovered on loading from the node type's definition, which makes the shaping a pass over the tree in
each direction:

```csharp
static class FlatProperties
{
    static IEnumerable<IDictionary<string, object?>> Nodes(IDictionary<string, object?> diagram) =>
        ((IEnumerable<object?>)diagram["pages"]!).Cast<IDictionary<string, object?>>()
            .SelectMany(page => ((IEnumerable<object?>)page["nodes"]!).Cast<IDictionary<string, object?>>());

    public static void Flatten(IDictionary<string, object?> diagram)            // {kind, value} -> value
    {
        foreach (var node in Nodes(diagram))
        {
            var properties = (IDictionary<string, object?>)node["properties"]!;
            foreach (var name in properties.Keys.ToList())
                properties[name] = ((IDictionary<string, object?>)properties[name]!)["value"];
        }
    }

    public static void Restore(IDictionary<string, object?> diagram, NodeTypeRegistry types)     // value -> {kind, value}
    {
        foreach (var node in Nodes(diagram))
        {
            var type = types.Get((string)node["typeKey"]!);
            var properties = (IDictionary<string, object?>)node["properties"]!;
            foreach (var name in properties.Keys.ToList())
            {
                var kind = type.FindProperty(name)?.ValueKind switch
                {
                    PropertyValueKind.Boolean => "bool", PropertyValueKind.Int32 => "int", PropertyValueKind.Int64 => "long",
                    PropertyValueKind.Double => "double", PropertyValueKind.String => "string",
                    PropertyValueKind.Choice => "choice", PropertyValueKind.Color => "color",
                    _ => properties[name] switch { bool => "bool", long => "long", double => "double", _ => "string" },
                };
                properties[name] = new Dictionary<string, object?> { ["kind"] = kind, ["value"] = properties[name] };
            }
        }
    }
}
```

Call `FlatProperties.Flatten(tree)` on the tree before converting it for the serializer and
`FlatProperties.Restore(file.Diagram, types)` right after deserializing, before `Diagram.FromTree`. This is a deliberate
trade: the file is tidier, and the adapter now knows about the `kind` names and about the node types, so it is the part
to revisit when the format changes. Properties that were never declared (ad hoc) have no definition, so their kind is
inferred from the TOML type: an integer is read as `long`, which is a different kind than a declared `int`.

A fully hand-designed schema (shorter keys, a bounds array instead of a table, a `from = "A.value"` string for wires)
is the same idea taken further: map the tree to classes of your own by hand. There are six record types and the mapping
is mechanical, but it is code that has to be kept in step with the format.

## Versioning and Compatibility

**Two version numbers.** `DiagramSnapshot.FormatVersion` is the version of the diagram format that Plexus reads and
writes (`DiagramSnapshot.CurrentFormatVersion`, currently 1; the `formatVersion` of the tree); a reader rejects a
version it does not know with a `SnapshotException`, and the JSON reader ignores members it does not know, so additions
are compatible. The host's own format needs a version as well (`ProjectInfo.SchemaVersion` above), and it is the host's
version that governs the adapter and the host's own objects. Check the host's version first, migrate older files to the
current shape while they are still a tree (rename a key, add a default) and only then hand the diagram part to Plexus.

**Unknown node types.** A file may use node types that the running application does not have: it was written by a newer
version, or by a build with a different set of plug-ins. By default loading is all or nothing, so the loaders throw a
`SnapshotException` whose `MissingNodeTypes` lists every missing key; tell the user which types are missing. Passing
`UnknownNodeTypes = UnknownNodeTypeHandling.Skip` in the `DiagramOptions` opens what is understood and reports the rest
in `diagram.LoadReport` (see *Files With Unknown Node Types* in *Saving, Loading and the Clipboard*). Either way, keep
the keys of node types stable: they are written to files and to the clipboard.

**Evolving your own objects.** Give every new member of a host record or class a default, so that files written
before it existed still load, and never reuse the name of a removed member for something else. System.Text.Json and
Tomlyn both ignore members they do not know by default, which is what lets an older application open a newer file
(and, on saving, silently drop what it did not understand: keep the extra keys in a dictionary member when that matters).

**Saving safely.** Write to a temporary file and replace the target when the write succeeded, so that a crash never
leaves half a document, and call `diagram.History.MarkClean()` only after the replacement.

## Untrusted Input

A file is untrusted input, exactly like the clipboard. Plexus reads it with explicit, versioned JSON, safe value types,
a maximum nesting depth and no type names: a document cannot choose which types are created, and the host payload is
always deserialized as the type the node type declares. A custom format keeps these guarantees only if its adapter does:

- Parse with a library that has depth and size limits (Tomlyn has `TomlSerializerOptions.MaxDepth`) and check the size
  of the file before reading it into memory.
- Never deserialize with type names taken from the file; the adapter above maps only to the generic tree and to your
  own fixed classes.
- Treat every exception of loading as a failed Open, not as a bug: `TomlException` (or the exceptions of your format),
  `System.Text.Json.JsonException`, `SnapshotException`, `InvalidCastException` and `KeyNotFoundException` from an
  adapter that expected a table where the file has a string, and I/O errors. Load completely before replacing the open
  document, so that a bad file changes nothing.
- A custom `INodeDataSerializer` must treat its input as untrusted too; returning `null` or throwing one of the
  documented exceptions marks the payload unreadable instead of failing the process.

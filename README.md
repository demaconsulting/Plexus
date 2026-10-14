# Plexus

[![GitHub forks][badge-forks]][link-forks]
[![GitHub stars][badge-stars]][link-stars]
[![GitHub contributors][badge-contributors]][link-contributors]
[![License][badge-license]][link-license]
[![Build][badge-build]][link-build]
[![Quality Gate][badge-quality]][link-quality]
[![Security][badge-security]][link-security]
[![NuGet][badge-nuget]][link-nuget]

Plexus is a general-purpose .NET library for building and editing node-based block diagrams with typed ports, routed
wires, annotations, nestable collapsible groups, multiple pages, full undo/redo, and cross-application copy/paste. It
ships a UI-free core, a headless PNG renderer built on CanvasNet, and an Avalonia editor control.

## Features

- 🔌 **Typed Ports** - Directional, colored port types with single-wire inputs by default and optional fan-in
- 🧭 **Routed Wires** - Orthogonal, obstacle-avoiding routing with draggable segments and crossing jumps
- 🎨 **Dynamic Styling** - Color, dash, light up, tint or fade wires and nodes from what they connect or hold
- 🗂️ **Pages and Groups** - Multiple pages, nestable collapsible groups and sticky-note annotations
- ↩️ **Undo/Redo** - Every mutation is undoable, with transactions to group a gesture into one step
- 📋 **Copy/Paste** - Versioned JSON clipboard that treats pasted data as untrusted input
- 🖼️ **Headless Rendering** - Render any page to a PNG image without a UI framework
- 🖱️ **Avalonia Editor** - Select, move, connect, pan, zoom, rename and drag-and-drop out of the box
- 🧩 **Node Palette** - Searchable palette showing node types exactly as they look on the canvas
- 📝 **Property Grid** - Generic, annotation-driven property grid with a one-line node properties dialog
- ✨ **Easy by Default** - Working diagram in a few lines; theme, router and text measurer are replaceable
- 🔄 **Multi-Target** - Core supports .NET 8, 9 and 10; the other packages target .NET 10
- ✅ **Continuous Compliance** - [Compliance evidence][link-continuous-compliance] generated on every CI run

## Packages

| Package                                       | Purpose                                                          |
|-----------------------------------------------|------------------------------------------------------------------|
| `DemaConsulting.Plexus`                       | UI-free core: model, undo history, routing, scene, clipboard     |
| `DemaConsulting.Plexus.Rendering`             | Headless PNG renderer (depends on CanvasNet)                     |
| `DemaConsulting.Plexus.Avalonia`              | Avalonia editor control (depends on Avalonia)                    |
| `DemaConsulting.Plexus.Avalonia.PropertyGrid` | Property grid and node dialog (depends on Avalonia and the core) |

The Avalonia package does not depend on the rendering package, and the core depends on no UI or drawing library, so
headless use needs no Avalonia. The property grid package is generic (it edits any annotated .NET object); its Plexus
adapter shows a node, an annotation or a group with one assignment and makes every commit one undo step.

## Installation

Install the packages you need using the .NET CLI:

```bash
dotnet add package DemaConsulting.Plexus
dotnet add package DemaConsulting.Plexus.Rendering
dotnet add package DemaConsulting.Plexus.Avalonia
dotnet add package DemaConsulting.Plexus.Avalonia.PropertyGrid
```

## Quick Start

Declare node types, build a diagram, connect two nodes and render the page to a PNG file:

```csharp
using DemaConsulting.Plexus;
using DemaConsulting.Plexus.Geometry;
using DemaConsulting.Plexus.Rendering;

// 1. Declare the vocabulary: port types, node types, ports and properties
var types = new NodeTypeRegistry();
types.PortTypes.Add(new PortType("f32", Color.Parse("#FF8C00")));
types.Add(new NodeType("acme.Producer")
{
    Title = "PRODUCER",
    Outputs = { new PortDefinition("out", "f32") },
    Properties = { new PropertyDefinition("Rate", 10.0) { Unit = "Hz", ShowInSummary = true } },
});
types.Add(new NodeType("acme.Consumer")
{
    Title = "CONSUMER",
    Inputs = { new PortDefinition("in", "f32") },
});

// 2. Build a diagram: pages hold nodes, wires, annotations and groups
var diagram = new Diagram(types);
var page = diagram.Pages.Add("Main");
page.AddNode("acme.Producer", 30, 30, "A");
page.AddNode("acme.Consumer", 300, 90, "B");
page.Connect("A.out", "B.in");

// 3. Render the page headlessly
PlexusRenderer.SavePng(page, "diagram.png");

// 4. Every change is undoable
diagram.History.Undo(); // removes the wire
```

To edit the same diagram interactively, add the Avalonia editor control to a window and set its `Page`:

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:plexus="https://github.com/demaconsulting/plexus">
  <plexus:DiagramEditor x:Name="Editor" ShowGrid="True" SnapToGrid="True" />
</Window>
```

```csharp
Editor.Page = page;
```

To let users add nodes, put a `NodePalette` beside the editor and point it at the editor; it shows every node type of the
editor's diagram as it will look on the canvas, with a search box, and needs no other code (drag an item onto the editor,
or double-click it, to add the node):

```xml
<plexus:NodePalette Editor="{Binding #Editor}" />
```

To edit a node's properties, open the ready-made dialog of the property grid package, for example when the user
double-clicks a node (OK applies the rename, the changed properties and the host data as one undo step):

```csharp
using DemaConsulting.Plexus.Avalonia.PropertyGrid.Diagrams;

Editor.NodeDoubleClicked += async (_, e) => await NodePropertiesDialog.ShowAsync(window, e.Node);
```

See the [User Guide][link-user-guide] for concepts, customization (themes, routers, connection rules, custom node
visuals and host data), clipboard and snapshot handling, and the full editor API.

## Visual Tour

The screenshots are taken from the demo application (see below), which is built only from the public API.

The demo editor: node palette, page tabs, the diagram editor and a status bar. The host supplies the node types and
Plexus does everything else.

![The Plexus demo application: a node palette, page tabs, the diagram editor with wired nodes and a status bar][img-overview]

The node palette draws every node type exactly as the canvas does. Dragging a type over the canvas shows a translucent
ghost of where the node will land.

![A node dragged from the palette over the canvas, with a translucent ghost showing where it will be placed][img-drop-ghost]

The property grid edits a node and its host data in a dialog (or a docked panel), with categories, drop-downs, a
slider, nested objects, collections and validation; OK is one undo step.

![The properties dialog of a workflow Task node with categories, bold changed values, a slider, a nested object and a collection][img-dialog]

Wires are routed orthogonally around nodes, with junction dots where wires of one net branch apart and hops where
wires of different nets cross.

![The wiring sample: junction dots, square T corners and crossing hops in many wire configurations][img-wiring]

Any page renders headlessly to PNG, without a window, from the command line or from code.

![Command-line rendering of a diagram page to a PNG file with plexus-demo-render][img-render]

## Demo and Puppet

The repository includes applications that are **not shipped** in any package:

- **Demo** (`demo/`): `plexus-demo`, an Avalonia desktop application showing how little a host must write to get a
  complete editor, `plexus-demo-render`, a command line tool that renders diagram JSON files to PNG, and the shared
  demo node types and file format. Run the demo with
  `dotnet run --project demo/DemaConsulting.Plexus.Demo.Gui`.
- **Puppet** (`tools/`): `plexus-puppet`, a Windows tool that lets an AI coding agent or test script drive the real
  demo GUI with UI Automation, a named pipe and real mouse input, to find usability problems that headless tests
  cannot.

## Building

Plexus requires the .NET 10 SDK. To build and test the solution:

```bash
pwsh ./build.ps1
```

Before submitting changes, apply automatic formatting and run the lint checks:

```bash
pwsh ./fix.ps1
pwsh ./lint.ps1
```

## Documentation

- **User Guide**: Concepts and task-by-task usage: declaring node types, building diagrams, undo, files and clipboard,
  rendering, the editor, the palette, the property grid, styling, wires, extension points and testing. It is published
  as `Plexus User Guide.pdf` with every [release][link-releases]; the Markdown sources are in the
  [user guide folder][link-user-guide].
- **API Reference**: Every package ships its API reference as Markdown in the package's `api/` folder, generated from
  the XML doc comments by [ApiMark][link-apimark] and organized for gradual disclosure, so a developer or a coding agent
  can read a small targeted page instead of one large document. Start at `api/api.md` (the index), open a namespace page
  for a complete usage example and overview, then follow the links to type and member pages. For example, the
  `DemaConsulting.Plexus` namespace page is a "hello diagram": declare node types, build a diagram, connect two nodes,
  undo and save as JSON. When building from source, the same files are written to `src/DemaConsulting.Plexus*/api/`.
- **Compliance documents**: Each release also ships the build notes, software design and verification design,
  requirements and justifications, trace matrix, code quality report, and review plan and report, generated on every CI
  run.

## Contributing

Contributions are welcome. See [CONTRIBUTING.md][link-contributing] for development setup, coding
standards, and the pull request process.

## License

Copyright (c) DEMA Consulting. Licensed under the MIT License. See [LICENSE][link-license] for details.

By contributing to this project, you agree that your contributions will be licensed under the MIT License.

<!-- Badge References -->
[badge-forks]: https://img.shields.io/github/forks/demaconsulting/Plexus?style=plastic
[badge-stars]: https://img.shields.io/github/stars/demaconsulting/Plexus?style=plastic
[badge-contributors]: https://img.shields.io/github/contributors/demaconsulting/Plexus?style=plastic
[badge-license]: https://img.shields.io/github/license/demaconsulting/Plexus?style=plastic
[badge-build]: https://img.shields.io/github/actions/workflow/status/demaconsulting/Plexus/build_on_push.yaml?style=plastic
[badge-quality]: https://sonarcloud.io/api/project_badges/measure?project=demaconsulting_Plexus&metric=alert_status
[badge-security]: https://sonarcloud.io/api/project_badges/measure?project=demaconsulting_Plexus&metric=security_rating
[badge-nuget]: https://img.shields.io/nuget/v/DemaConsulting.Plexus?style=plastic

<!-- Link References -->
[link-forks]: https://github.com/demaconsulting/Plexus/network/members
[link-stars]: https://github.com/demaconsulting/Plexus/stargazers
[link-contributors]: https://github.com/demaconsulting/Plexus/graphs/contributors
[link-license]: https://github.com/demaconsulting/Plexus/blob/main/LICENSE
[link-build]: https://github.com/demaconsulting/Plexus/actions/workflows/build_on_push.yaml
[link-quality]: https://sonarcloud.io/dashboard?id=demaconsulting_Plexus
[link-security]: https://sonarcloud.io/dashboard?id=demaconsulting_Plexus
[link-nuget]: https://www.nuget.org/packages/DemaConsulting.Plexus
[link-continuous-compliance]: https://github.com/demaconsulting/ContinuousCompliance
[link-apimark]: https://github.com/demaconsulting/ApiMark
[link-contributing]: https://github.com/demaconsulting/Plexus/blob/main/CONTRIBUTING.md
[link-user-guide]: https://github.com/demaconsulting/Plexus/tree/main/docs/user_guide
[link-releases]: https://github.com/demaconsulting/Plexus/releases/latest

<!-- Image References -->
[img-overview]: https://raw.githubusercontent.com/demaconsulting/Plexus/main/docs/user_guide/images/overview.png
[img-drop-ghost]: https://raw.githubusercontent.com/demaconsulting/Plexus/main/docs/user_guide/images/drop-ghost.png
[img-dialog]: https://raw.githubusercontent.com/demaconsulting/Plexus/main/docs/user_guide/images/property-grid-dialog.png
[img-wiring]: https://raw.githubusercontent.com/demaconsulting/Plexus/main/docs/user_guide/images/wiring.png
[img-render]: https://raw.githubusercontent.com/demaconsulting/Plexus/main/docs/user_guide/images/render-cli.png

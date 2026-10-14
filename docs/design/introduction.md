# Introduction

This document provides the detailed design for Plexus, a general-purpose .NET library for building and editing
node-based block diagrams with typed ports, routed wires, annotations, nestable collapsible groups, multiple pages,
full undo/redo, and cross-application copy/paste.

## Purpose

The purpose of this document is to serve as the design entry point and provide detailed design
specifications for the Plexus software systems. This documentation enables formal code
review by providing implementation specifications, supports compliance auditing by maintaining
clear traceability from requirements through design to code, aids maintenance by documenting
system structure and interactions, and ensures quality assurance through detailed technical
specifications.

This document is intended for:

- Software developers implementing and maintaining the libraries
- Code reviewers validating implementation against design
- Compliance auditors tracing requirements through design to implementation
- Quality assurance teams validating system behavior

## Scope

Plexus is delivered as four NuGet packages. Each package contains exactly one software system:

| Package                                     | Software System            | Purpose                                |
|---------------------------------------------|----------------------------|----------------------------------------|
| DemaConsulting.Plexus                       | Plexus                     | UI-free core: model, history, routing  |
| DemaConsulting.Plexus.Rendering             | PlexusRendering            | Headless PNG rendering using CanvasNet |
| DemaConsulting.Plexus.Avalonia              | PlexusAvalonia             | Avalonia diagram editor control        |
| DemaConsulting.Plexus.Avalonia.PropertyGrid | PlexusAvaloniaPropertyGrid | Generic Avalonia property grid control |

This document covers the detailed design of these four systems and their constituent software items (see _Software
Structure_), and the following OTS items:

- **Avalonia** — UI framework hosting the editor control
- **CanvasNet** — drawing, text, SVG and PNG library used by the headless renderer
- **SvgSkia** — SVG icon library used by the editor control
- **ApiMark** — API reference generation tool
- **BuildMark** — build-notes documentation tool
- **FileAssert** — document assertion tool
- **Pandoc** — Markdown-to-HTML conversion tool
- **ReqStream** — requirements traceability tool
- **ReviewMark** — file review enforcement tool
- **SarifMark** — SARIF report conversion tool
- **SonarMark** — SonarCloud quality report tool
- **SysML2Tools** — architecture model lint and diagram rendering tool
- **VersionMark** — tool-version documentation tool
- **WeasyPrint** — HTML-to-PDF conversion tool
- **xUnit** — unit-testing framework

Version applicability: This design applies to all versions of Plexus.

The following topics are explicitly excluded from this design documentation:

- External library internals and third-party OTS components
- Build pipeline configuration and CI/CD processes
- Deployment, packaging, and distribution mechanisms
- Infrastructure and hosting environment details
- Test projects and test infrastructure
- The demo applications under `demo/` and the agent tooling under `tools/`, which are not shipped and are not
  software items; they demonstrate and exercise the libraries but carry no requirements

## Software Structure

The software structure is modeled in SysML2 under `docs/sysml2/` and rendered to the
diagram below by SysML2Tools as part of the build pipeline. AI agents should query the
SysML2 model directly (see the `sysml2tools-query` skill) rather than parsing this
diagram or the prose below.

![Software Structure](SoftwareStructureView.svg)

The software items are classified as follows. Folder depth alone does not determine classification; this list is
authoritative.

```text
Plexus (system)
├── Geometry (unit)
├── ConnectionRules (unit)
├── Model (subsystem)
│   ├── Diagram (unit)
│   ├── PageCollection (unit)
│   ├── DiagramPage (unit)
│   ├── Node (unit)
│   ├── Wire (unit)
│   ├── Annotation (unit)
│   └── Group (unit)
├── History (subsystem)
│   └── UndoHistory (unit)
├── Types (subsystem)
│   ├── NodeTypeRegistry (unit)
│   └── IconSource (unit)
├── Properties (subsystem)
│   ├── PropertyDefinition (unit)
│   └── PropertyBag (unit)
├── Routing (subsystem)
│   ├── OrthogonalRouter (unit)
│   └── SimpleRouter (unit)
├── Scene (subsystem)
│   ├── DiagramTheme (unit)
│   ├── NodeLayout (unit)
│   ├── TextMeasurement (unit)
│   ├── SceneBuilder (unit)
│   └── DiagramScene (unit)
└── Serialization (subsystem)
    ├── Snapshot (unit)
    └── PlexusClipboard (unit)

PlexusRendering (system) — depends on Plexus and CanvasNet
├── PlexusRenderer (unit)
├── ScenePainter (unit)
├── CanvasTextMeasurer (unit)
└── IconCache (unit)

PlexusAvalonia (system) — depends on Plexus, Avalonia and SvgSkia
├── Editor (subsystem)
│   ├── DiagramEditor (unit)
│   ├── Gestures (unit)
│   ├── DiagramClipboard (unit)
│   ├── NodeTypeDragSource (unit)
│   └── NodePalette (unit)
├── ScenePainter (unit)
├── AvaloniaTextMeasurer (unit)
└── PlexusIcons (unit)

PlexusAvaloniaPropertyGrid (system) — depends on Avalonia; only its Plexus adapter units depend on the Plexus core
├── PropertyModel (unit)
├── PropertySources (unit)
├── EditSession (unit)
├── EditorRegistry (unit)
├── PropertyGridControl (unit)
├── PropertyEditors (unit)
├── CollectionEditorUi (unit)
├── PropertyGridDialog (unit)
├── PlexusNodeSource (unit)
└── NodePropertiesDialog (unit)
```

Unit names may repeat across systems (`ScenePainter` exists in both PlexusRendering and PlexusAvalonia); identifiers
are qualified by system, for example `PlexusRendering-ScenePainter` and `PlexusAvalonia-ScenePainter`.

Dependencies between systems are one-directional. Plexus depends on no UI or drawing library. PlexusRendering depends
on Plexus and CanvasNet; PlexusAvalonia depends on Plexus, Avalonia and SvgSkia and does not depend on
PlexusRendering. Both painters consume the same scene, layout and hit-test geometry built by the Plexus `Scene`
subsystem, and text measurement is abstracted behind a core interface that each painter package implements.

## Folder Layout

The source code folder structure mirrors the software structure organization, with file paths
and descriptions as follows:

```text
src/DemaConsulting.Plexus/                  — Plexus system (net8.0; net9.0; net10.0)
├── Geometry/                               — Geometry unit
├── Connections/                            — ConnectionRules unit
├── Model/                                  — Model subsystem (Diagram, PageCollection, DiagramPage, Node, Wire,
│                                             Annotation and Group units)
├── History/                                — History subsystem (UndoHistory unit)
├── Types/                                  — Types subsystem (NodeTypeRegistry and IconSource units)
├── Properties/                             — Properties subsystem (PropertyDefinition and PropertyBag units)
├── Routing/                                — Routing subsystem (OrthogonalRouter and SimpleRouter units)
├── Scene/                                  — Scene subsystem (DiagramTheme, NodeLayout, TextMeasurement,
│                                             SceneBuilder and DiagramScene units)
└── Serialization/                          — Serialization subsystem (Snapshot and PlexusClipboard units)

src/DemaConsulting.Plexus.Rendering/        — PlexusRendering system (net10.0); one file group per unit
src/DemaConsulting.Plexus.Avalonia.PropertyGrid/ — PlexusAvaloniaPropertyGrid system (net10.0); Plexus/ is the adapter
src/DemaConsulting.Plexus.Avalonia/         — PlexusAvalonia system (net10.0)
├── DiagramEditor*.cs and event args        — Editor subsystem, DiagramEditor unit
├── Interaction/                            — Editor subsystem, Gestures unit
├── IDiagramClipboard.cs, SystemDiagramClipboard.cs, TextFormats.cs
│                                           — Editor subsystem, DiagramClipboard unit
├── NodeTypeDragSource.cs                   — Editor subsystem, NodeTypeDragSource unit
├── NodePalette*.cs, NodePrototype*.cs, NodeTypeActivatedEventArgs.cs
│                                           — Editor subsystem, NodePalette unit
├── ScenePainter.cs, PlexusAvaloniaExtensions.cs — ScenePainter unit
├── AvaloniaTextMeasurer.cs                 — AvaloniaTextMeasurer unit
└── PlexusIcons.cs                          — PlexusIcons unit

test/DemaConsulting.Plexus.Tests/           — Plexus system tests
test/DemaConsulting.Plexus.Rendering.Tests/ — PlexusRendering system tests
test/DemaConsulting.Plexus.Avalonia.Tests/  — PlexusAvalonia system tests
test/DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests/ — PlexusAvaloniaPropertyGrid system tests

demo/                                       — Not shipped: demo model, CLI renderer and Avalonia demo application
tools/                                      — Not shipped: Puppet agent tooling that drives the demo application
```

The `demo/` and `tools/` folders (and the test projects that accompany them) are not software items: they are not
distributed in any package, have no requirements, and are excluded from ReviewMark.

## Key Design Decisions

These decisions apply across the packages; decisions specific to one system are recorded in that system's design.

- **Four packages in one repository.** The core, the headless renderer, the editor control and the property grid are
  separate NuGet packages so that headless use needs no Avalonia, the core needs no drawing library, and the property
  grid can edit any annotated object without the editor. Dependencies are one-directional (see _Software Structure_).
- **The scene lives in the core.** The scene (layout, hit-testing, drag and drop geometry) contains geometry but no
  drawing dependency, so it belongs in the core; a package of its own would be one more reference for every consumer.
- **One scene, two painters.** Layout, hit-testing and drop geometry exist once and both painters consume them, so what
  a user clicks is what the renderer draws. Visual parity between the painters is "close enough to look the same", not
  pixel-identical, because their text engines and SVG renderers are different OTS libraries.
- **Text measurement is a core interface.** Each painter package implements it with its own text APIs; wire anchors
  use a deterministic estimate instead, so routing never depends on a painter (see _Plexus System Design_).
- **Hosts supply the vocabulary.** Node types, port types, icons, connection rules and data payload serializers come
  from the host, with defaults for each; Plexus is aimed at many independent applications, not one product, and ships
  no node-type catalog.
- **Target frameworks.** The core multi-targets net8.0, net9.0 and net10.0 because every host references it; the
  renderer, the editor and the property grid target net10.0 only.
- **API governance and reference.** Public API tracking follows the repository template's defaults, with no extra
  tracking tooling. The API reference is generated from the XML documentation by ApiMark and packed into each package
  (see _ApiMark_ in the OTS design documents).
- **OTS libraries are verified through Plexus's own tests.** No dedicated OTS test project exists for CanvasNet or
  Avalonia; the unit tests of the packages that use them cover the functionality Plexus relies on.
- **Demo and tooling are examples, not products.** The demo application (a reference host with node domains, a
  properties dialog on double-click, multiple pages and clipboard exchange between instances) and the command-line
  renderer share one demo model and JSON format. The Puppet driver combines UI Automation for windows, menus and
  dialogs, a named pipe for model commands and geometry queries, and real operating-system mouse input at the screen
  coordinates the pipe reports, so the control's real gesture code is exercised. The pipe endpoint exists only in the
  demo build and never in a shipped package.

## Known Limitations and Future Considerations

- **Windows-only demo automation.** The Puppet driver depends on Windows-only UI Automation libraries, so the demo
  automation and the real-input verification run on Windows only. The packages themselves have no platform
  restriction.
- **Real-input verification needs an interactive desktop.** The mouse-gesture and operating-system-clipboard scenarios
  between two demo instances need an unlocked interactive desktop and cannot run in CI; the Puppet scenarios are run
  manually before a release.
- **SVG subset parity.** The two painters use different SVG engines (see _SvgSkia_ and _CanvasNet_ in the OTS design
  documents); hosts should keep icons within the common subset.
- **Clipboard format evolution.** Negotiation of a future clipboard format version and of mismatched node-type
  definitions that share a key is not yet designed (see _Plexus System Design_).
- **Rendering framework range.** The renderer targets a single framework; multi-targeting it like the core is an open
  question (see _PlexusRendering System Design_).
- **Headless test adapter.** The Avalonia-based test projects wait for an Avalonia headless xUnit adapter that supports
  the xUnit v3 generation in use (see _PlexusAvalonia System Design_).

## Document Conventions

Throughout this document:

- Class names, method names, property names, and file names appear in `monospace` font.
- The word **shall** denotes a design constraint that the implementation must satisfy.
- Section headings within each chapter follow a consistent structure: overview, data model,
  methods/algorithms, and interactions with other units.
- Text tables are used in preference to diagrams, which may not render in all PDF viewers.

## Companion Artifact Structure

Each software item has corresponding artifacts in parallel directory trees:

- Requirements: `docs/reqstream/{system}[/{subsystem}...]/{item}.yaml` (kebab-case)
- Design docs: `docs/design/{system}[/{subsystem}...]/{item}.md` (kebab-case)
- Verification design: `docs/verification/{system}[/{subsystem}...]/{item}.md` (kebab-case)
- Source code: `src/DemaConsulting.{System}/...` (PascalCase for C#); the unit-to-file mapping is recorded in the
  SysML2 model
- Tests: `test/DemaConsulting.{System}.Tests/[{Subsystem}/]{Item}Tests.cs` (PascalCase for C#); system tests are
  `{SystemName}Tests.cs`
- SysML2 model: `docs/sysml2/model/{system}[/{subsystem}...]/{item}.sysml` (kebab-case)
- OTS items: `docs/reqstream/ots/{ots-name}.yaml`, `docs/design/ots/{ots-name}.md` and
  `docs/verification/ots/{ots-name}.md`
- Review-sets: defined in `.reviewmark.yaml`

System names in documentation paths are kebab-case: `plexus`, `plexus-rendering`, `plexus-avalonia` and
`plexus-avalonia-property-grid`.

## References

- Plexus User Guide — the compiled User Guide document for this repository.
- Plexus Repository — the Plexus source repository hosted on
  GitHub.

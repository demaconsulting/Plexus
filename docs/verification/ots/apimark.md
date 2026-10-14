## ApiMark Verification

This document provides the verification evidence for the ApiMark OTS software item. Requirements for this OTS
item are defined in the ApiMark OTS Software Requirements document.

### Required Functionality

`DemaConsulting.ApiMark.MSBuild` reads each compiled shipped assembly and its XML documentation file and writes a
gradual-disclosure Markdown API reference — an `api.md` index, a page per namespace, a page per type and a page
per member — into the project's `api/` folder, which is packed into the NuGet package. Beyond existence, three
properties of that output are relied upon: namespace documentation (with its usage example first) reaches the
namespace pages, documented examples render as fenced code blocks a reader can copy, and — for PlexusRendering,
PlexusAvalonia and PlexusAvaloniaPropertyGrid, which set `ApiMarkEnforceDocs=Public` and
`ApiMarkEnforceDocsSeverity=Error` — a public member
without an XML doc summary fails the build. ApiMark contributes no runtime asset to any shipped package.

### Verification Approach

ApiMark is verified by asserting the **real output of the real build**. The MSBuild integration runs during the
ordinary `dotnet build` of each shipped project, `dotnet pack` then bundles the generated tree, and the
`.github/workflows/build.yaml` build job runs

```text
dotnet fileassert --results artifacts/fileassert-api-docs-<os>.trx api-docs
```

against the produced `.nupkg` files immediately afterwards, on each of the three build operating systems. The
TRX lands in `artifacts/`, which ReqStream consumes (`--tests "artifacts/**/*.trx"`), so the result is ordinary
traceability evidence. If ApiMark had not run, or had run without producing content, the assertions would fail and
the build with them. All four shipped packages (core, rendering, Avalonia editor and property grid) are asserted.

ApiMark's own `--validate` self-test is deliberately not used as evidence: it does not exercise generation of this
repository's configuration, and asserting the artifacts that the packages actually ship is the stronger and more
honest evidence.

Several assertions check content rather than existence, because existence alone would not detect the failure
modes that matter: a reference whose examples rendered as prose, or whose namespace documentation was dropped,
would still produce a complete-looking file tree. Documentation-coverage enforcement is verified by the build
itself — with severity `Error`, a single undocumented public member of PlexusRendering, PlexusAvalonia or
PlexusAvaloniaPropertyGrid fails the
build, so a green build cannot coexist with one — and the assertions add the positive half: that members flagged
during adoption are now documented and reach the generated reference.

### Test Scenarios

#### ApiMark_CoreApiDocsPacked

**Scenario**: ApiMark generates the API reference for the core package during the build and `dotnet pack` bundles it.

**Expected**: The `DemaConsulting.Plexus` `.nupkg` contains exactly one `api/api.md` (listing the
`DemaConsulting.Plexus.Scene` namespace), exactly one `api/DemaConsulting.Plexus.md`, the `Diagram` type page, and
at least 100 pages under `api/**/*.md`; the multi-targeted project produces the tree once.

**Requirement coverage**: `Plexus-OTS-ApiMark-Generate`.

#### ApiMark_RenderingApiDocsPacked

**Scenario**: ApiMark generates the API reference for the rendering package during the build.

**Expected**: The `DemaConsulting.Plexus.Rendering` `.nupkg` contains `api/api.md` naming the namespace, the
namespace page, the `PlexusRenderer` type page and at least 10 pages under `api/**/*.md`.

**Requirement coverage**: `Plexus-OTS-ApiMark-Generate`.

#### ApiMark_AvaloniaApiDocsPacked

**Scenario**: ApiMark generates the API reference for the Avalonia package during the build.

**Expected**: The `DemaConsulting.Plexus.Avalonia` `.nupkg` contains `api/api.md` naming the namespace, the
namespace page, the `DiagramEditor` type page and at least 50 pages under `api/**/*.md`.

**Requirement coverage**: `Plexus-OTS-ApiMark-Generate`.

#### ApiMark_PropertyGridApiDocsPacked

**Scenario**: ApiMark generates the API reference for the property grid package during the build.

**Expected**: The `DemaConsulting.Plexus.Avalonia.PropertyGrid` `.nupkg` contains `api/api.md` naming both the
`DemaConsulting.Plexus.Avalonia.PropertyGrid` namespace and its `.Plexus` adapter namespace, the root namespace page,
the `PropertyGrid` type page, the `Plexus` adapter namespace page and at least 100 pages under `api/**/*.md`.

**Requirement coverage**: `Plexus-OTS-ApiMark-Generate`.

#### ApiMark_NamespaceDocRendered

**Scenario**: The `NamespaceDoc` carrier classes of each public namespace are generated into the namespace pages.

**Expected**: The root namespace page of the core package contains the "hello diagram" example introduction
followed, in order, by a fenced `csharp` block with `Pages.Add`, `Connect("A.out", "B.in")`, `History.Undo` and
`ToJson`; the Scene, Routing and Geometry pages carry their examples (`DiagramTheme.Classic with`, `IWireRouter`,
`Color.Parse`); the Rendering page shows `PlexusRenderer.SavePng`; the Avalonia page shows the `plexus:DiagramEditor`
and `plexus:NodePalette` XAML; the PropertyGrid page shows the `SelectedObject = new Mixer()` example and the
`PropertyEditSession` staged-editing example, and its `Plexus` adapter page lists `NodePropertySource`.

**Requirement coverage**: `Plexus-OTS-ApiMark-RenderExamples`.

#### ApiMark_ExampleCodeRendered

**Scenario**: Members and types documented with `<example><code>` are generated to their own pages.

**Expected**: The `DiagramPage`, `UndoHistory`, `ConnectionRules` and `Diagram.ToJson` pages each contain a fenced
`csharp` code block with the documented calls (`page.AddNode`, `BeginTransaction`, `ConnectionRules.All`,
`ToJson`), so the examples are presented as copyable code rather than prose.

**Requirement coverage**: `Plexus-OTS-ApiMark-RenderExamples`.

#### ApiMark_RenderingMembersDocumented

**Scenario**: Documentation-coverage enforcement is active for the rendering package and the implicit
constructor of `RenderOptions` is documented.

**Expected**: The generated `RenderOptions` constructor page contains the constructor summary and no
"No description provided" placeholder.

**Requirement coverage**: `Plexus-OTS-ApiMark-EnforceDocs`.

#### ApiMark_AvaloniaMembersDocumented

**Scenario**: Documentation-coverage enforcement is active for the Avalonia package and the members it flagged
during adoption (the `Render` overrides of `DiagramEditor` and `NodePalette`, whose documentation had been an
`inheritdoc` that ApiMark cannot resolve against the framework) are documented.

**Expected**: Both generated `Render` pages contain their own summary prose ("Draws the visible part of the
diagram", "Draws the palette entries").

**Requirement coverage**: `Plexus-OTS-ApiMark-EnforceDocs`.

#### ApiMark_PropertyGridMembersDocumented

**Scenario**: Documentation-coverage enforcement is active for the property grid package and the public members of
its main control and edit session are documented.

**Expected**: The generated `PropertyGrid` constructor page contains the constructor summary and no
"No description provided" placeholder; the `PropertyGrid.Refresh` and `PropertyEditSession.Commit` pages contain
their own summary prose ("Re-reads the source's items and values", "Writes the pending edits to the target").

### Evidence Limitations

- **Core package enforcement.** `ApiMarkEnforceDocs` is not enabled for the core package. ApiMark 0.5.7 reports the
  compiler-synthesized members of the public delegate `ConnectionValidator` (`Invoke`, `BeginInvoke`, `EndInvoke`
  and the constructor) as undocumented, and they cannot be documented in source. The core package is still
  covered by the C# compiler's CS1591, which is an error through `TreatWarningsAsErrors`.
- **Example correctness.** That an example compiles and behaves as described is a property of the doc comments, not
  of ApiMark, and is not asserted by these tests; the examples were compiled and run against the shipped assemblies
  when they were written.
- **Language tag.** ApiMark labels every example code block `csharp`, including the XAML examples.

### Requirements Coverage

- **`Plexus-OTS-ApiMark-Generate`**: ApiMark_CoreApiDocsPacked, ApiMark_RenderingApiDocsPacked,
  ApiMark_AvaloniaApiDocsPacked, ApiMark_PropertyGridApiDocsPacked
- **`Plexus-OTS-ApiMark-RenderExamples`**: ApiMark_NamespaceDocRendered, ApiMark_ExampleCodeRendered
- **`Plexus-OTS-ApiMark-EnforceDocs`**: ApiMark_RenderingMembersDocumented, ApiMark_AvaloniaMembersDocumented,
  ApiMark_PropertyGridMembersDocumented

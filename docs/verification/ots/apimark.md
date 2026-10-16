## ApiMark Verification

This document provides the verification evidence for the ApiMark OTS software item. Requirements for this OTS
item are defined in the ApiMark OTS Software Requirements document.

### Required Functionality

`DemaConsulting.ApiMark.MSBuild` reads each compiled shipped assembly and its XML documentation file and writes a
gradual-disclosure Markdown API reference — an `api.md` index, a page per namespace, a page per type and a page
per member — into the project's `api/` folder, which is packed into the NuGet package. Beyond existence, these
properties of that output are relied upon: namespace documentation (with its usage example first) reaches the
namespace pages, documented examples render as fenced code blocks a reader can copy (in the language the example
declares), signatures carry parameter defaults, nullability, record and init-only forms and generic type parameters,
the core types that the dependent packages mention link to the core package's own reference, and — for all four
shipped packages, which set `ApiMarkEnforceDocs=Public` and `ApiMarkEnforceDocsSeverity=Error` — a public member
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
itself — with severity `Error`, a single undocumented public member of any shipped package fails the
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

#### ApiMark_ExampleLanguageRendered

**Scenario**: The XAML examples of the Avalonia package declare `<code lang="xml">`.

**Expected**: The `NodePalette` type page contains the palette XAML (`Grid`, `plexus:NodePalette`, `GridSplitter`)
in a fenced `xml` block and no longer carries the "XAML, not C#" workaround comment; the Avalonia namespace page
contains the `MainWindow.axaml` window markup in a fenced `xml` block while its C# examples stay fenced as `csharp`.

**Requirement coverage**: `Plexus-OTS-ApiMark-RenderExamples`.

#### ApiMark_SignaturesRendered

**Scenario**: ApiMark renders C# signatures from the compiled assembly instead of relying on the doc comments to
describe them.

**Expected**: The `DiagramPage.Connect` page shows the optional parameters as
`IReadOnlyList<Geometry.Point>? route = null, string? id = null` under a breadcrumb line back to the namespace and
type; the `Diagram.WireStyler` page shows `Func<Wire, Scene.WireStyle?>?`; the `DiagramTheme` page declares
`public sealed record DiagramTheme` and its `NodeFill` page shows `{ get; init; }`; the `PropertyBag.Get` page shows
the generic signature `public T Get<T>(string name)`.

**Requirement coverage**: `Plexus-OTS-ApiMark-RenderSignatures`.

#### ApiMark_CrossPackageLinksRendered

**Scenario**: The Rendering, Avalonia and PropertyGrid projects set `ApiMarkExternalLinks` to map the core assembly to
the core package's `api/` folder.

**Expected**: The `PlexusRenderer.SavePng` page (Rendering), the `NodeDoubleClickedEventArgs` page (Avalonia) and the
`NodePropertySource` page (PropertyGrid) each contain a Markdown link whose target is
`../…/demaconsulting.plexus/{version}/api/DemaConsulting.Plexus/{Type}.md`, the layout of the core package beside the
dependent package in the NuGet package cache. The assertion proves that the link is generated; ApiMark does not
verify that the target exists, and it exists only where the core package is restored.

**Requirement coverage**: `Plexus-OTS-ApiMark-LinkPackages`.

#### ApiMark_CoreMembersDocumented

**Scenario**: Documentation-coverage enforcement is active for the core package, whose public delegate
`ConnectionValidator` has compiler-synthesized members that earlier ApiMark versions reported as undocumented.

**Expected**: The generated `ConnectionValidator` page declares the delegate signature and contains its summary and
no "No description provided" placeholder.

**Requirement coverage**: `Plexus-OTS-ApiMark-EnforceDocs`.

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

- **Cross-package link targets.** The link assertions show that ApiMark generated the link, not that its target
  exists: the target resolves only where the core package of the same version sits beside the dependent package, as in
  the NuGet package cache, and not in a build from source.
- **Example correctness.** That an example compiles and behaves as described is a property of the doc comments, not
  of ApiMark, and is not asserted by these tests; the examples were compiled against the shipped assemblies when
  ApiMark was first adopted (0.5.8), and later ApiMark updates change only how they are rendered.

### Requirements Coverage

- **`Plexus-OTS-ApiMark-Generate`**: ApiMark_CoreApiDocsPacked, ApiMark_RenderingApiDocsPacked,
  ApiMark_AvaloniaApiDocsPacked, ApiMark_PropertyGridApiDocsPacked
- **`Plexus-OTS-ApiMark-RenderExamples`**: ApiMark_NamespaceDocRendered, ApiMark_ExampleCodeRendered,
  ApiMark_ExampleLanguageRendered
- **`Plexus-OTS-ApiMark-RenderSignatures`**: ApiMark_SignaturesRendered
- **`Plexus-OTS-ApiMark-LinkPackages`**: ApiMark_CrossPackageLinksRendered
- **`Plexus-OTS-ApiMark-EnforceDocs`**: ApiMark_CoreMembersDocumented, ApiMark_RenderingMembersDocumented,
  ApiMark_AvaloniaMembersDocumented, ApiMark_PropertyGridMembersDocumented

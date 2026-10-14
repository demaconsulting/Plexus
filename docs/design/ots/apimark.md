## ApiMark

### Purpose

DemaConsulting.ApiMark.MSBuild (ApiMark) generates the API reference documentation of the four shipped
packages from their compiled assemblies and XML documentation files. It was chosen because its output is
organized for _gradual disclosure_ — an `api.md` index, then a page per namespace, per type and per member —
which matches the stated audience for the reference: a coding agent (or developer) assembling an application
against Plexus, which reads a small targeted page rather than one large document. It also renders
`<example><code>` blocks as fenced code, honors `NamespaceDoc` carrier classes for namespace-level overviews, and
offers opt-in documentation-coverage enforcement.

### Features Used

- **Assembly-based generation**: reads each shipped assembly and its XML documentation file and writes Markdown
  under `src/{Project}/api/` (the default output directory), regenerated on every build and ignored by git.
- **Gradual-disclosure output**: `api.md` index, namespace pages, type pages and member pages whose folder
  structure mirrors the namespace hierarchy.
- **Namespace documentation**: one internal `NamespaceDoc` class per public namespace carries the namespace
  summary and its leading usage example.
- **Protected members** (`ApiMarkVisibility=PublicAndProtected`): protected and virtual extension points of public
  types (for example the `StandardNodeVisual.Add*` methods) are documented as well as the public members. Every such
  member therefore needs a real XML summary, because an inherited or missing description renders as a placeholder.
- **Example rendering**: `<example><code>` blocks on namespaces, types and members are rendered as fenced code.
- **NuGet package inclusion** (`ApiMarkPackDocs=true`): the generated tree is packed into the `api/` folder of
  each package, so consumers and agents read it without visiting an external site.
- **Documentation-coverage enforcement** (`ApiMarkEnforceDocs=Public`, `ApiMarkEnforceDocsSeverity=Error`):
  enabled for PlexusRendering, PlexusAvalonia and PlexusAvaloniaPropertyGrid, so a public member without an XML doc
  summary fails the build.
  It is not enabled for the core package because ApiMark 0.5.7 flags the compiler-synthesized members of a
  public delegate (`ConnectionValidator`) as undocumented and they cannot be documented in source; the C#
  compiler's CS1591 still applies there.

### Integration Pattern

ApiMark is referenced as `<PackageReference Include="DemaConsulting.ApiMark.MSBuild" Version="0.5.7"
PrivateAssets="All" />` in each of the four shipped projects, so it never becomes a dependency of a consuming
project. Its target runs after `Build`. For the multi-targeted core project (`net8.0;net9.0;net10.0`) it runs once,
for the first target framework only, which is correct because the public surface is identical across target
frameworks; the same single `api/` tree is added to the package. The `build.yaml` workflow builds and then packs the
solution, after which FileAssert asserts the contents of the produced `.nupkg` files (tag `api-docs` in
`.fileassert.yaml`). ApiMark contributes no runtime asset to any package; the only thing it can change is the content
of the `api/` documentation folder. Its version is captured by VersionMark as `apimark-msbuild`.

### Known Limitations

- Documentation-coverage enforcement is not enabled for the core package (see _Features Used_); a public member there
  can lack a summary except where the C# compiler's CS1591 reports it.
- Protected and virtual members are documented (`PublicAndProtected`), so each needs a real summary; an inherited or
  missing description renders as a placeholder.

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
- **Protected members** (`ApiMarkVisibility`, whose C# default is `PublicAndProtected`): protected extension points of
  public types (for example the `StandardNodeVisual.Add*` methods) are documented as well as the public members.
  Every such member therefore needs a real XML summary, because an inherited or missing description renders as a
  placeholder.
- **Signature detail**: signatures follow C# source syntax, so a reader sees parameter defaults (`route = null`),
  per-type nullability (`Func<Wire, WireStyle?>?`), the `record` and `init` forms (`DiagramTheme`), generic method
  type parameters (`Get<T>(string name)`) and `where` constraints without the doc comments repeating them; type and
  member pages open with a breadcrumb line back up the tree.
- **Example rendering**: `<example><code>` blocks on namespaces, types and members are rendered as fenced code; the
  `lang` attribute selects the fence language (`<code lang="xml">` for the XAML examples), and a block without it
  is fenced as `csharp`.
- **Cross-package links** (`ApiMarkExternalLinks`): the Rendering, Avalonia and PropertyGrid projects map the
  `DemaConsulting.Plexus` assembly to the `api/` folder of the core package, so the core types they mention render as
  links instead of being listed as external types. The link target is a path relative to the package's own `api/`
  folder, `../../../demaconsulting.plexus/{version}/api`, which is where NuGet extracts the core package of the same
  version beside it in the package cache. ApiMark does not verify that a link target exists.
- **NuGet package inclusion** (`ApiMarkPackDocs=true`): the generated tree is packed into the `api/` folder of
  each package, so consumers and agents read it without visiting an external site.
- **Documentation-coverage enforcement** (`ApiMarkEnforceDocs=Public`, `ApiMarkEnforceDocsSeverity=Error`):
  enabled for all four shipped packages, so a public member without an XML doc summary — including the members of
  a public delegate such as `ConnectionValidator` — fails the build.

### Integration Pattern

ApiMark is referenced as a `PackageReference` with `PrivateAssets="All"` in each of the four shipped projects, so it
never becomes a dependency of a consuming project. Its target runs after `Build`. For the multi-targeted core project
(`net8.0;net9.0;net10.0`) it runs once,
for the first target framework only, which is correct because the public surface is identical across target
frameworks; the same single `api/` tree is added to the package. The `build.yaml` workflow builds and then packs the
solution, after which FileAssert asserts the contents of the produced `.nupkg` files (tag `api-docs` in
`.fileassert.yaml`). ApiMark contributes no runtime asset to any package; the only thing it can change is the content
of the `api/` documentation folder. Its version is captured by VersionMark as `apimark-msbuild`.

### Known Limitations

- ApiMark does not show the `virtual`, `override` or `abstract` modifier in a signature, so the protected extension
  points of `StandardNodeVisual` are described as virtual in the type's prose.
- Generated pages do not list inherited members, so the types derived from `DiagramElement` (`Node`, `Wire`, `Group`,
  `Annotation`) list the inherited members in their remarks.
- Cross-package links are not verified and resolve only where the core package of the same version sits beside the
  package, as in the NuGet package cache; in a build from source (version `0.0.0`) they point at a folder that does
  not exist.
- Protected members are documented, so each needs a real summary; an inherited or missing description renders as a
  placeholder.

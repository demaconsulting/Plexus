# OTS Integration Design

This document describes the overall Off-The-Shelf (OTS) integration strategy for the Plexus
repository.

## Overview

The Plexus system (`DemaConsulting.Plexus`) has no third-party runtime dependencies: it is implemented exclusively
against the .NET Base Class Library, including `System.Text.Json` for the clipboard and snapshot interchange format. The
other systems consume runtime OTS libraries:

- **PlexusRendering** uses CanvasNet (and CanvasNet.Svg) to draw scenes, measure text, rasterize SVG icons and encode
  PNG images. It is the only package that references CanvasNet.
- **PlexusAvalonia** uses Avalonia as the UI framework hosting the editor control, and Svg.Controls.Skia.Avalonia
  (SvgSkia) to draw SVG icons. It does not reference CanvasNet.
- **PlexusAvaloniaPropertyGrid** uses Avalonia for its controls. It does not reference CanvasNet or SvgSkia.

The remaining OTS items are build-time and quality-pipeline tools, not runtime dependencies. Each provides one stage of
the documentation, requirements-traceability, testing, and quality-reporting pipeline invoked by `build.ps1`,
`lint.ps1`, and the `.github/workflows/build.yaml` CI workflow. None of these tools are linked into, or shipped with,
the compiled NuGet packages (ApiMark contributes only the generated Markdown API reference in each package's `api/`
folder). The demo applications and agent tooling under `demo/` and `tools/` use further
third-party libraries, but they are not shipped and are outside the scope of this documentation.

## OTS Items

| OTS Item    | Kind    | Purpose                                                                         |
|-------------|---------|---------------------------------------------------------------------------------|
| ApiMark     | Tool    | Generates the Markdown API reference shipped in each NuGet package              |
| Avalonia    | Runtime | UI framework for the PlexusAvalonia and PlexusAvaloniaPropertyGrid controls     |
| BuildMark   | Tool    | Generates build-notes documentation from GitHub Actions metadata                |
| CanvasNet   | Runtime | Drawing, text, SVG rasterization and PNG encoding for PlexusRendering           |
| FileAssert  | Tool    | Validates generated documents (HTML/PDF) against acceptance criteria            |
| Pandoc      | Tool    | Converts Markdown documentation to HTML                                         |
| ReqStream   | Tool    | Enforces requirements-to-test traceability                                      |
| ReviewMark  | Tool    | Enforces file review coverage and currency                                      |
| SarifMark   | Tool    | Converts CodeQL SARIF results into a markdown report                            |
| SonarMark   | Tool    | Generates a SonarCloud quality report                                           |
| SvgSkia     | Runtime | Converts SVG icons to Avalonia images for PlexusAvalonia                        |
| SysML2Tools | Tool    | Validates the SysML2 architecture model and renders its views to SVG            |
| VersionMark | Tool    | Captures and publishes tool-version information                                 |
| WeasyPrint  | Tool    | Converts HTML documentation to PDF                                              |
| xUnit       | Tool    | Discovers and executes unit and integration tests                               |

Each item's individual design document (`docs/design/ots/{ots-name}.md`) records its Purpose,
Features Used, and Integration Pattern. Each item's requirements and verification evidence are
recorded in `docs/reqstream/ots/{ots-name}.yaml` and `docs/verification/ots/{ots-name}.md`
respectively.

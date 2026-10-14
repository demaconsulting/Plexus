# Introduction

This document provides the verification design for Plexus, a general-purpose .NET library for building and editing
node-based block diagrams.

## Purpose

The purpose of this document is to serve as the verification design entry point and document how
requirements will be tested across all software items in the Plexus systems. This
documentation enables formal review by mapping every requirement to named test scenarios, supports
compliance auditing by providing clear traceability from requirements through verification design
to tests, and ensures test completeness can be assessed without reading implementation code.

This document is intended for:

- Software developers implementing and maintaining tests
- Code reviewers validating test completeness against requirements
- Compliance auditors tracing requirements through verification design to tests
- Quality assurance teams validating test coverage and scenario adequacy

## Scope

This document covers the verification design for the four Plexus software systems and their
constituent software items, specifically:

- **Plexus (System)** — the UI-free core library, with the Model, History, Types, Properties, Routing, Scene and
  Serialization subsystems and the Geometry and ConnectionRules units
- **PlexusRendering (System)** — the headless PNG renderer
- **PlexusAvalonia (System)** — the Avalonia editor control, with the Editor subsystem
- **PlexusAvaloniaPropertyGrid (System)** — the generic annotation-driven property grid (UI-free layers and Avalonia controls)

The software item hierarchy is defined in the design introduction (`docs/design/introduction.md`) and in the SysML2
model under `docs/sysml2/`.

The following OTS items are also covered:

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

This verification documentation covers the same software items as the design documentation.

Version applicability: This verification design applies to all versions of Plexus.

The following topics are explicitly excluded from this verification documentation:

- Build pipeline and CI/CD process testing
- Infrastructure and hosting environment testing
- The demo applications under `demo/` and the agent tooling under `tools/` (and their test projects), which are not
  shipped and are not software items

## Companion Artifact Structure

Each software item covered by this document has corresponding artifacts in parallel directory
trees. In-house items have artifacts in these parallel locations:

- Requirements: `docs/reqstream/{system}[/{subsystem}...]/{item}.yaml` (kebab-case)
- Design docs: `docs/design/{system}[/{subsystem}...]/{item}.md` (kebab-case)
- Verification design: `docs/verification/{system}[/{subsystem}...]/{item}.md` (kebab-case)
- Source code: `src/DemaConsulting.{System}/...` (PascalCase for C#)
- Tests: `test/DemaConsulting.{System}.Tests/[{Subsystem}/]{Item}Tests.cs` (PascalCase for C#); system tests are
  `{SystemName}Tests.cs`

System names in documentation paths are kebab-case: `plexus`, `plexus-rendering`, `plexus-avalonia` and
`plexus-avalonia-property-grid`.

OTS items have parallel artifacts in:

- Requirements: `docs/reqstream/ots/{ots-name}.yaml` (kebab-case)
- Verification: `docs/verification/ots/{ots-name}.md` (kebab-case)

Review-sets: defined in `.reviewmark.yaml`

## References

- Plexus User Guide — the compiled User Guide document for this repository.
- Plexus Repository — the Plexus source repository hosted on
  GitHub.

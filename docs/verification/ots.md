# OTS Verification Evidence

This document describes the overall Off-The-Shelf (OTS) verification strategy for the Plexus
repository.

## Overview

The OTS items fall into two groups with different verification approaches.

**Runtime libraries** (CanvasNet, Avalonia and SvgSkia) have no dedicated OTS test project. Each is verified by the
tests of the Plexus system that consumes it: the PlexusRendering tests exercise CanvasNet drawing, text, SVG and PNG
encoding, and the PlexusAvalonia tests exercise Avalonia rendering, input, text, clipboard and headless hosting, and
SvgSkia icon loading. Because those tests inspect real pixels, measurements and images, a passing test run is direct
evidence that the third-party functionality Plexus depends on works.

**Build and compliance tools** used to build and verify this repository are verified through a combination of
self-validation CLI flags (where the tool provides a `--validate` or equivalent self-test mode)
and pipeline-evidence-based verification (where a passing CI pipeline run, having produced and
validated the expected output at each stage, constitutes proof that the tool executed correctly).

Each item's individual verification document (`docs/verification/ots/{ots-name}.md`) records the
detailed approach and named test scenarios.

## OTS Items

| OTS Item    | Verification Approach                                                             |
|-------------|-----------------------------------------------------------------------------------|
| ApiMark     | Pipeline evidence: FileAssert assertions on the API reference inside each package |
| Avalonia    | PlexusAvalonia tests running the editor control on the Avalonia headless platform |
| BuildMark   | Self-validation CLI suite plus pipeline evidence via build-notes document         |
| CanvasNet   | PlexusRendering tests asserting pixels, text measurements and PNG output          |
| FileAssert  | Self-validation CLI suite plus transitive evidence from document assertions       |
| Pandoc      | Pipeline evidence: FileAssert assertions on each generated HTML document          |
| ReqStream   | Self-validation CLI suite plus pipeline evidence via --enforce traceability       |
| ReviewMark  | Self-validation CLI suite plus pipeline evidence via review plan/report           |
| SarifMark   | Self-validation CLI suite plus pipeline evidence via SARIF markdown report        |
| SonarMark   | Self-validation CLI suite plus pipeline evidence via SonarCloud report            |
| SvgSkia     | PlexusAvalonia icon tests loading valid and malformed SVG                         |
| SysML2Tools | Self-validation CLI suite plus pipeline evidence via lint and rendered SVGs       |
| VersionMark | Self-validation CLI suite plus pipeline evidence via version data                 |
| WeasyPrint  | Pipeline evidence: FileAssert assertions on each generated PDF document           |
| xUnit       | Self-validation via discovery, execution, and TRX reporting of tests              |

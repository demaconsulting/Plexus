package OTS {
    private import SoftwareItemMetadata::*;

    doc
    /*
     * Off-the-shelf (third-party) software items used by Plexus: runtime libraries consumed by the
     * Plexus packages and the tools used by the build, test and compliance pipeline.
     */

    part def CanvasNet {
        doc /* DemaConsulting.CanvasNet and DemaConsulting.CanvasNet.Svg: 2D drawing, text, SVG rasterization and PNG encoding used by the headless renderer. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/canvasnet.md");
            verificationFiles = ("docs/verification/ots/canvasnet.md");
            requirementFiles = ("docs/reqstream/ots/canvasnet.yaml");
        }
    }

    part def Avalonia {
        doc /* Avalonia UI framework (including Avalonia.Headless for tests): controls, drawing context, input and clipboard used by the editor control. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/avalonia.md");
            verificationFiles = ("docs/verification/ots/avalonia.md");
            requirementFiles = ("docs/reqstream/ots/avalonia.yaml");
        }
    }

    part def SvgSkia {
        doc /* Svg.Controls.Skia.Avalonia: converts SVG text to Avalonia images for node icons in the editor control. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/svg-skia.md");
            verificationFiles = ("docs/verification/ots/svg-skia.md");
            requirementFiles = ("docs/reqstream/ots/svg-skia.yaml");
        }
    }

    part def XUnit {
        doc /* xUnit v3 unit-testing framework used to discover, execute and report automated tests. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/xunit.md");
            verificationFiles = ("docs/verification/ots/xunit.md");
            requirementFiles = ("docs/reqstream/ots/xunit.yaml");
        }
    }

    part def ReqStream {
        doc /* ReqStream tool enforcing requirements-to-test traceability and generating requirements reports. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/reqstream.md");
            verificationFiles = ("docs/verification/ots/reqstream.md");
            requirementFiles = ("docs/reqstream/ots/reqstream.yaml");
        }
    }

    part def BuildMark {
        doc /* BuildMark tool generating build-notes documentation from build metadata. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/buildmark.md");
            verificationFiles = ("docs/verification/ots/buildmark.md");
            requirementFiles = ("docs/reqstream/ots/buildmark.yaml");
        }
    }

    part def VersionMark {
        doc /* VersionMark tool capturing and publishing tool-version information. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/versionmark.md");
            verificationFiles = ("docs/verification/ots/versionmark.md");
            requirementFiles = ("docs/reqstream/ots/versionmark.yaml");
        }
    }

    part def SarifMark {
        doc /* SarifMark tool converting CodeQL SARIF results to a markdown report. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/sarifmark.md");
            verificationFiles = ("docs/verification/ots/sarifmark.md");
            requirementFiles = ("docs/reqstream/ots/sarifmark.yaml");
        }
    }

    part def SonarMark {
        doc /* SonarMark tool generating a SonarCloud quality report. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/sonarmark.md");
            verificationFiles = ("docs/verification/ots/sonarmark.md");
            requirementFiles = ("docs/reqstream/ots/sonarmark.yaml");
        }
    }

    part def SysML2Tools {
        doc /* SysML2Tools tool linting the SysML2 architecture model and rendering its views to SVG. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/sysml2tools.md");
            verificationFiles = ("docs/verification/ots/sysml2tools.md");
            requirementFiles = ("docs/reqstream/ots/sysml2tools.yaml");
        }
    }

    part def ReviewMark {
        doc /* ReviewMark tool enforcing file review coverage and currency. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/reviewmark.md");
            verificationFiles = ("docs/verification/ots/reviewmark.md");
            requirementFiles = ("docs/reqstream/ots/reviewmark.yaml");
        }
    }

    part def Pandoc {
        doc /* Pandoc tool converting markdown documentation to HTML. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/pandoc.md");
            verificationFiles = ("docs/verification/ots/pandoc.md");
            requirementFiles = ("docs/reqstream/ots/pandoc.yaml");
        }
    }

    part def WeasyPrint {
        doc /* WeasyPrint tool converting HTML documentation to PDF. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/weasyprint.md");
            verificationFiles = ("docs/verification/ots/weasyprint.md");
            requirementFiles = ("docs/reqstream/ots/weasyprint.yaml");
        }
    }

    part def FileAssert {
        doc /* FileAssert tool validating generated documents against acceptance criteria. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/fileassert.md");
            verificationFiles = ("docs/verification/ots/fileassert.md");
            requirementFiles = ("docs/reqstream/ots/fileassert.yaml");
        }
    }

    part def ApiMark {
        doc /* ApiMark MSBuild package generating the gradual-disclosure Markdown API reference shipped in each NuGet package. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/apimark.md");
            verificationFiles = ("docs/verification/ots/apimark.md");
            requirementFiles = ("docs/reqstream/ots/apimark.yaml");
        }
    }
}

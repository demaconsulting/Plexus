package OTS {
    doc
    /*
     * Off-the-shelf (third-party) software items used by Plexus: runtime libraries consumed by the
     * Plexus packages and the tools used by the build, test and compliance pipeline.
     */

    part def CanvasNet {
        doc /* DemaConsulting.CanvasNet and DemaConsulting.CanvasNet.Svg: 2D drawing, text, SVG rasterization and PNG encoding used by the headless renderer. */

        comment designRef /* Design: docs/design/ots/canvasnet.md */
        comment verificationRef /* Verification: docs/verification/ots/canvasnet.md */
        comment reqRef /* Requirements: docs/reqstream/ots/canvasnet.yaml */
    }

    part def Avalonia {
        doc /* Avalonia UI framework (including Avalonia.Headless for tests): controls, drawing context, input and clipboard used by the editor control. */

        comment designRef /* Design: docs/design/ots/avalonia.md */
        comment verificationRef /* Verification: docs/verification/ots/avalonia.md */
        comment reqRef /* Requirements: docs/reqstream/ots/avalonia.yaml */
    }

    part def SvgSkia {
        doc /* Svg.Controls.Skia.Avalonia: converts SVG text to Avalonia images for node icons in the editor control. */

        comment designRef /* Design: docs/design/ots/svg-skia.md */
        comment verificationRef /* Verification: docs/verification/ots/svg-skia.md */
        comment reqRef /* Requirements: docs/reqstream/ots/svg-skia.yaml */
    }

    part def XUnit {
        doc /* xUnit v3 unit-testing framework used to discover, execute and report automated tests. */

        comment designRef /* Design: docs/design/ots/xunit.md */
        comment verificationRef /* Verification: docs/verification/ots/xunit.md */
        comment reqRef /* Requirements: docs/reqstream/ots/xunit.yaml */
    }

    part def ReqStream {
        doc /* ReqStream tool enforcing requirements-to-test traceability and generating requirements reports. */

        comment designRef /* Design: docs/design/ots/reqstream.md */
        comment verificationRef /* Verification: docs/verification/ots/reqstream.md */
        comment reqRef /* Requirements: docs/reqstream/ots/reqstream.yaml */
    }

    part def BuildMark {
        doc /* BuildMark tool generating build-notes documentation from build metadata. */

        comment designRef /* Design: docs/design/ots/buildmark.md */
        comment verificationRef /* Verification: docs/verification/ots/buildmark.md */
        comment reqRef /* Requirements: docs/reqstream/ots/buildmark.yaml */
    }

    part def VersionMark {
        doc /* VersionMark tool capturing and publishing tool-version information. */

        comment designRef /* Design: docs/design/ots/versionmark.md */
        comment verificationRef /* Verification: docs/verification/ots/versionmark.md */
        comment reqRef /* Requirements: docs/reqstream/ots/versionmark.yaml */
    }

    part def SarifMark {
        doc /* SarifMark tool converting CodeQL SARIF results to a markdown report. */

        comment designRef /* Design: docs/design/ots/sarifmark.md */
        comment verificationRef /* Verification: docs/verification/ots/sarifmark.md */
        comment reqRef /* Requirements: docs/reqstream/ots/sarifmark.yaml */
    }

    part def SonarMark {
        doc /* SonarMark tool generating a SonarCloud quality report. */

        comment designRef /* Design: docs/design/ots/sonarmark.md */
        comment verificationRef /* Verification: docs/verification/ots/sonarmark.md */
        comment reqRef /* Requirements: docs/reqstream/ots/sonarmark.yaml */
    }

    part def SysML2Tools {
        doc /* SysML2Tools tool linting the SysML2 architecture model and rendering its views to SVG. */

        comment designRef /* Design: docs/design/ots/sysml2tools.md */
        comment verificationRef /* Verification: docs/verification/ots/sysml2tools.md */
        comment reqRef /* Requirements: docs/reqstream/ots/sysml2tools.yaml */
    }

    part def ReviewMark {
        doc /* ReviewMark tool enforcing file review coverage and currency. */

        comment designRef /* Design: docs/design/ots/reviewmark.md */
        comment verificationRef /* Verification: docs/verification/ots/reviewmark.md */
        comment reqRef /* Requirements: docs/reqstream/ots/reviewmark.yaml */
    }

    part def Pandoc {
        doc /* Pandoc tool converting markdown documentation to HTML. */

        comment designRef /* Design: docs/design/ots/pandoc.md */
        comment verificationRef /* Verification: docs/verification/ots/pandoc.md */
        comment reqRef /* Requirements: docs/reqstream/ots/pandoc.yaml */
    }

    part def WeasyPrint {
        doc /* WeasyPrint tool converting HTML documentation to PDF. */

        comment designRef /* Design: docs/design/ots/weasyprint.md */
        comment verificationRef /* Verification: docs/verification/ots/weasyprint.md */
        comment reqRef /* Requirements: docs/reqstream/ots/weasyprint.yaml */
    }

    part def FileAssert {
        doc /* FileAssert tool validating generated documents against acceptance criteria. */

        comment designRef /* Design: docs/design/ots/fileassert.md */
        comment verificationRef /* Verification: docs/verification/ots/fileassert.md */
        comment reqRef /* Requirements: docs/reqstream/ots/fileassert.yaml */
    }

    part def ApiMark {
        doc /* ApiMark MSBuild package generating the gradual-disclosure Markdown API reference shipped in each NuGet package. */

        comment designRef /* Design: docs/design/ots/apimark.md */
        comment verificationRef /* Verification: docs/verification/ots/apimark.md */
        comment reqRef /* Requirements: docs/reqstream/ots/apimark.yaml */
    }
}

package Plexus {
    private import SoftwareItemMetadata::*;

    part def TextMeasurement {
        doc /* Text measurement abstraction and the approximate default measurer. */

        metadata info : SoftwareInfo {
            description = "Text measurement abstraction and the approximate default measurer.";
            sourceFiles = ("src/DemaConsulting.Plexus/Scene/ITextMeasurer.cs", "src/DemaConsulting.Plexus/Scene/ApproximateTextMeasurer.cs");
            testFiles = ("test/DemaConsulting.Plexus.Tests/Scene/TextMeasurementTests.cs");
            designFiles = ("docs/design/plexus/scene/text-measurement.md");
            verificationFiles = ("docs/verification/plexus/scene/text-measurement.md");
            requirementFiles = ("docs/reqstream/plexus/scene/text-measurement.yaml");
        }
    }
}

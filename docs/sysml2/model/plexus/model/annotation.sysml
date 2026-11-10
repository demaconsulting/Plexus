package Plexus {
    private import SoftwareItemMetadata::*;

    part def Annotation {
        doc /* Sticky-note and text-box element with resizable bounds and optional fill. */

        metadata info : SoftwareInfo {
            description = "Sticky-note and text-box element with resizable bounds and optional fill.";
            sourceFiles = ("src/DemaConsulting.Plexus/Model/Annotation.cs");
            testFiles = ("test/DemaConsulting.Plexus.Tests/Model/AnnotationTests.cs", "test/DemaConsulting.Plexus.Tests/PageCollectionTests.cs", "test/DemaConsulting.Plexus.Tests/DiagramPageMoveTests.cs", "test/DemaConsulting.Plexus.Tests/DiagramPageCreationTests.cs", "test/DemaConsulting.Plexus.Tests/DiagramPageRemoveTests.cs");
            designFiles = ("docs/design/plexus/model/annotation.md");
            verificationFiles = ("docs/verification/plexus/model/annotation.md");
            requirementFiles = ("docs/reqstream/plexus/model/annotation.yaml");
        }
    }
}

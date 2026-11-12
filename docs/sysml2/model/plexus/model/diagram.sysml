package Plexus {
    private import SoftwareItemMetadata::*;

    part def Diagram {
        doc /* Diagram root: node-type registry, pages, history, theme, router, connection rules, change notification, rename pipeline and move-to-page. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Plexus/Model/Diagram.cs", "src/DemaConsulting.Plexus/Model/Diagram.Appearance.cs", "src/DemaConsulting.Plexus/Model/Diagram.MoveToPage.cs", "src/DemaConsulting.Plexus/Model/Diagram.Rename.cs", "src/DemaConsulting.Plexus/Model/DiagramChangeKind.cs", "src/DemaConsulting.Plexus/Model/DiagramChangedEventArgs.cs", "src/DemaConsulting.Plexus/Model/NodeRenamedEventArgs.cs", "src/DemaConsulting.Plexus/Model/NodeRenamingEventArgs.cs");
            testFiles = ("test/DemaConsulting.Plexus.Tests/DiagramChangedEventTests.cs", "test/DemaConsulting.Plexus.Tests/MoveToPageTests.cs", "test/DemaConsulting.Plexus.Tests/RenamePipelineTests.cs", "test/DemaConsulting.Plexus.Tests/Model/DiagramTests.cs", "test/DemaConsulting.Plexus.Tests/DiagramPageCreationTests.cs", "test/DemaConsulting.Plexus.Tests/PortPathTests.cs", "test/DemaConsulting.Plexus.Tests/ConnectionTests.cs");
            designFiles = ("docs/design/plexus/model/diagram.md");
            verificationFiles = ("docs/verification/plexus/model/diagram.md");
            requirementFiles = ("docs/reqstream/plexus/model/diagram.yaml");
        }
    }
}

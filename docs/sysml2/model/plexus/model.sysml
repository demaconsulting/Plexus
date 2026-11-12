package Plexus {
    private import SoftwareItemMetadata::*;

    part def Model {
        doc /* Diagram document model: diagram root, pages, nodes, ports, wires, annotations and groups with undoable mutation and change notification. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.Plexus.Tests/Model/ModelTests.cs");
            designFiles = ("docs/design/plexus/model.md");
            verificationFiles = ("docs/verification/plexus/model.md");
            requirementFiles = ("docs/reqstream/plexus/model.yaml");
        }

        part diagram : Diagram;
        part pageCollection : PageCollection;
        part page : DiagramPage;
        part node : Node;
        part wire : Wire;
        part annotation : Annotation;
        part group : Group;
    }
}

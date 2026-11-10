package Plexus {
    private import SoftwareItemMetadata::*;

    part def DiagramPage {
        doc /* One diagram page: containers for nodes, wires, annotations and groups, and the undoable operations that edit them (add, connect, group, move, remove, layout, and the port updates a node's port provider implies). */

        metadata info : SoftwareInfo {
            description = "One diagram page: containers for nodes, wires, annotations and groups, and the undoable operations that edit them (add, connect, group, move, remove, layout, and the port updates a node's port provider implies).";
            sourceFiles = ("src/DemaConsulting.Plexus/Model/DiagramPage.cs", "src/DemaConsulting.Plexus/Model/DiagramPage.Elements.cs", "src/DemaConsulting.Plexus/Model/DiagramPage.Groups.cs", "src/DemaConsulting.Plexus/Model/DiagramPage.Layout.cs", "src/DemaConsulting.Plexus/Model/DiagramPage.PortUpdates.cs", "src/DemaConsulting.Plexus/Model/DiagramPage.Ports.cs", "src/DemaConsulting.Plexus/Model/DiagramPage.Removal.cs", "src/DemaConsulting.Plexus/Model/ElementPresenceCommand.cs", "src/DemaConsulting.Plexus/Model/LayoutCommand.cs", "src/DemaConsulting.Plexus/Model/GroupMembershipCommand.cs", "src/DemaConsulting.Plexus/Model/GroupObstacles.cs");
            testFiles = ("test/DemaConsulting.Plexus.Tests/DiagramPageCreationTests.cs", "test/DemaConsulting.Plexus.Tests/DiagramPageMoveTests.cs", "test/DemaConsulting.Plexus.Tests/DiagramPageRemoveTests.cs", "test/DemaConsulting.Plexus.Tests/RerouteAllTests.cs", "test/DemaConsulting.Plexus.Tests/Routing/GroupObstacleTests.cs", "test/DemaConsulting.Plexus.Tests/PageCollectionTests.cs", "test/DemaConsulting.Plexus.Tests/PageCollectionBindingTests.cs", "test/DemaConsulting.Plexus.Tests/ConnectionTests.cs", "test/DemaConsulting.Plexus.Tests/PortPathTests.cs", "test/DemaConsulting.Plexus.Tests/GroupTests.cs");
            designFiles = ("docs/design/plexus/model/diagram-page.md");
            verificationFiles = ("docs/verification/plexus/model/diagram-page.md");
            requirementFiles = ("docs/reqstream/plexus/model/diagram-page.yaml");
        }
    }
}

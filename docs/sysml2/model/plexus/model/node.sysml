package Plexus {
    part def Node {
        doc /* Diagram element base class, nodes and their ports, including ports a node type's port provider computes per node and recomputes, in the same undo entry, when the node's properties or data change, resizing the node when its number of ports changes. */

        comment sourceRef /* Source: src/DemaConsulting.Plexus/Model/DiagramElement.cs, src/DemaConsulting.Plexus/Model/Node.cs, src/DemaConsulting.Plexus/Model/Node.*.cs, src/DemaConsulting.Plexus/Model/NodePortsCommand.cs, src/DemaConsulting.Plexus/Model/Port.cs */
        comment testRef /* Test: test/DemaConsulting.Plexus.Tests/Model/NodeTests.cs, test/DemaConsulting.Plexus.Tests/Model/NodePortsTests.cs, test/DemaConsulting.Plexus.Tests/PortPathTests.cs, test/DemaConsulting.Plexus.Tests/SharedPortNameTests.cs, test/DemaConsulting.Plexus.Tests/ElementConvenienceTests.cs, test/DemaConsulting.Plexus.Tests/DiagramPageCreationTests.cs, test/DemaConsulting.Plexus.Tests/DiagramPageMoveTests.cs, test/DemaConsulting.Plexus.Tests/PageCollectionTests.cs, test/DemaConsulting.Plexus.Tests/DiagramPageRemoveTests.cs, test/DemaConsulting.Plexus.Tests/RenamePipelineTests.cs */
        comment designRef /* Design: docs/design/plexus/model/node.md */
        comment verificationRef /* Verification: docs/verification/plexus/model/node.md */
        comment reqRef /* Requirements: docs/reqstream/plexus/model/node.yaml */
    }
}

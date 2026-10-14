package Plexus {
    part def Model {
        doc /* Diagram document model: diagram root, pages, nodes, ports, wires, annotations and groups with undoable mutation and change notification. */

        comment testRef /* Test: test/DemaConsulting.Plexus.Tests/Model/ModelTests.cs */
        comment designRef /* Design: docs/design/plexus/model.md */
        comment verificationRef /* Verification: docs/verification/plexus/model.md */
        comment reqRef /* Requirements: docs/reqstream/plexus/model.yaml */

        part diagram : Diagram;
        part pageCollection : PageCollection;
        part page : DiagramPage;
        part node : Node;
        part wire : Wire;
        part annotation : Annotation;
        part group : Group;
    }
}

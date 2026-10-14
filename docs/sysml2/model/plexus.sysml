package Plexus {
    doc
    /*
     * UI-free core library: document model, undo history, node types, properties, routing, scene graph and clipboard serialization for node-based diagrams.
     */

    part def PlexusSystem {
        doc /* UI-free core library: document model, undo history, node types, properties, routing, scene graph and clipboard serialization for node-based diagrams. */

        comment sourceRef /* Source: src/DemaConsulting.Plexus/NamespaceDoc.cs */
        comment testRef /* Test: test/DemaConsulting.Plexus.Tests/PlexusTests.cs, test/DemaConsulting.Plexus.Tests/PlexusWireStylingTests.cs, test/DemaConsulting.Plexus.Tests/PlexusNodeStylingTests.cs */
        comment designRef /* Design: docs/design/plexus.md */
        comment verificationRef /* Verification: docs/verification/plexus.md */
        comment reqRef /* Requirements: docs/reqstream/plexus.yaml */

        part geometry : Geometry;
        part connectionRules : ConnectionRules;
        part model : Model;
        part history : History;
        part types : Types;
        part properties : Properties;
        part routing : Routing;
        part scene : Scene;
        part serialization : Serialization;
    }
}

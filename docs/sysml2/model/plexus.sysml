package Plexus {
    private import SoftwareItemMetadata::*;

    doc
    /*
     * UI-free core library: document model, undo history, node types, properties, routing, scene graph and clipboard serialization for node-based diagrams.
     */

    part def PlexusSystem {
        doc /* UI-free core library: document model, undo history, node types, properties, routing, scene graph and clipboard serialization for node-based diagrams. */

        metadata info : SoftwareInfo {
            description = "UI-free core library: document model, undo history, node types, properties, routing, scene graph and clipboard serialization for node-based diagrams.";
            testFiles = ("test/DemaConsulting.Plexus.Tests/PlexusTests.cs", "test/DemaConsulting.Plexus.Tests/PlexusWireStylingTests.cs", "test/DemaConsulting.Plexus.Tests/PlexusNodeStylingTests.cs");
            designFiles = ("docs/design/plexus.md");
            verificationFiles = ("docs/verification/plexus.md");
            requirementFiles = ("docs/reqstream/plexus.yaml");
        }

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

package Plexus {
    public import Plexus_ConnectionRules::*;
    public import Plexus_Geometry::*;
    public import Plexus_History::*;
    public import Plexus_Model::*;
    public import Plexus_Properties::*;
    public import Plexus_Routing::*;
    public import Plexus_Scene::*;
    public import Plexus_Serialization::*;
    public import Plexus_Types::*;
    private import SoftwareItemMetadata::*;

    doc
    /*
     * UI-free core library: document model, undo history, node types, properties, routing, scene graph and clipboard serialization for node-based diagrams.
     */

    part def PlexusSystem {
        doc /* UI-free core library: document model, undo history, node types, properties, routing, scene graph and clipboard serialization for node-based diagrams. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Plexus/NamespaceDoc.cs");
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

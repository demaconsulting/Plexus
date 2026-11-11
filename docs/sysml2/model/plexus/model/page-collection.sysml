package Plexus {
    private import SoftwareItemMetadata::*;

    part def PageCollection {
        doc /* Ordered, observable collection of pages with undoable add, insert, remove and move. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Plexus/Model/PageCollection.cs");
            testFiles = ("test/DemaConsulting.Plexus.Tests/PageCollectionTests.cs", "test/DemaConsulting.Plexus.Tests/PageCollectionBindingTests.cs");
            designFiles = ("docs/design/plexus/model/page-collection.md");
            verificationFiles = ("docs/verification/plexus/model/page-collection.md");
            requirementFiles = ("docs/reqstream/plexus/model/page-collection.yaml");
        }
    }
}

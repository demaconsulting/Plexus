package Plexus {
    private import SoftwareItemMetadata::*;

    part def PropertyBag {
        doc /* Per-node property storage with typed values and automatic undo. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Plexus/Properties/PropertyBag.cs");
            testFiles = ("test/DemaConsulting.Plexus.Tests/PropertyBagTests.cs");
            designFiles = ("docs/design/plexus/properties/property-bag.md");
            verificationFiles = ("docs/verification/plexus/properties/property-bag.md");
            requirementFiles = ("docs/reqstream/plexus/properties/property-bag.yaml");
        }
    }
}

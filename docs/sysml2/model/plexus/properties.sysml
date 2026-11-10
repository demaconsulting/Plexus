package Plexus {
    private import SoftwareItemMetadata::*;

    part def Properties {
        doc /* Typed, undoable property definitions and per-element property bags. */

        metadata info : SoftwareInfo {
            description = "Typed, undoable property definitions and per-element property bags.";
            testFiles = ("test/DemaConsulting.Plexus.Tests/Properties/PropertiesTests.cs");
            designFiles = ("docs/design/plexus/properties.md");
            verificationFiles = ("docs/verification/plexus/properties.md");
            requirementFiles = ("docs/reqstream/plexus/properties.yaml");
        }

        part propertyDefinition : PropertyDefinition;
        part propertyBag : PropertyBag;
    }
}

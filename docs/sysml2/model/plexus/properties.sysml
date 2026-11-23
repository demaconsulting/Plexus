package Plexus_Properties {
    public import Plexus_PropertyBag::*;
    public import Plexus_PropertyDefinition::*;
    private import SoftwareItemMetadata::*;

    part def Properties {
        doc /* Typed, undoable property definitions and per-element property bags. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.Plexus.Tests/Properties/PropertiesTests.cs");
            designFiles = ("docs/design/plexus/properties.md");
            verificationFiles = ("docs/verification/plexus/properties.md");
            requirementFiles = ("docs/reqstream/plexus/properties.yaml");
        }

        part propertyDefinition : PropertyDefinition;
        part propertyBag : PropertyBag;
    }
}

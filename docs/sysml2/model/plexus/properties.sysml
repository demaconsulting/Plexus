package Plexus {
    part def Properties {
        doc /* Typed, undoable property definitions and per-element property bags. */

        comment testRef /* Test: test/DemaConsulting.Plexus.Tests/Properties/PropertiesTests.cs */
        comment designRef /* Design: docs/design/plexus/properties.md */
        comment verificationRef /* Verification: docs/verification/plexus/properties.md */
        comment reqRef /* Requirements: docs/reqstream/plexus/properties.yaml */

        part propertyDefinition : PropertyDefinition;
        part propertyBag : PropertyBag;
    }
}

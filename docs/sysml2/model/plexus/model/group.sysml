package Plexus {
    private import SoftwareItemMetadata::*;

    part def Group {
        doc /* Nestable, collapsible group of elements with computed bounds. */

        metadata info : SoftwareInfo {
            description = "Nestable, collapsible group of elements with computed bounds.";
            sourceFiles = ("src/DemaConsulting.Plexus/Model/Group.cs");
            testFiles = ("test/DemaConsulting.Plexus.Tests/GroupTests.cs");
            designFiles = ("docs/design/plexus/model/group.md");
            verificationFiles = ("docs/verification/plexus/model/group.md");
            requirementFiles = ("docs/reqstream/plexus/model/group.yaml");
        }
    }
}

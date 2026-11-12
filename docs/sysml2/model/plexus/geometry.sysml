package Plexus {
    private import SoftwareItemMetadata::*;

    part def Geometry {
        doc /* Immutable geometry value types (Point, Size, Rect) and the RGBA Color type shared by all Plexus items. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Plexus/Geometry/Color.cs", "src/DemaConsulting.Plexus/Geometry/NamespaceDoc.cs", "src/DemaConsulting.Plexus/Geometry/Point.cs", "src/DemaConsulting.Plexus/Geometry/Rect.cs", "src/DemaConsulting.Plexus/Geometry/Size.cs");
            testFiles = ("test/DemaConsulting.Plexus.Tests/GeometryTests.cs");
            designFiles = ("docs/design/plexus/geometry.md");
            verificationFiles = ("docs/verification/plexus/geometry.md");
            requirementFiles = ("docs/reqstream/plexus/geometry.yaml");
        }
    }
}

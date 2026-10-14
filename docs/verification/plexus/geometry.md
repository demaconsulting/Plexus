## Geometry Unit Verification Design

This document describes how the Geometry unit (the immutable `Point`, `Size`, `Rect` and `Color` value types) is
verified. Requirements are defined in the Geometry Unit Requirements document.

### Verification Approach

Geometry is pure computation with no collaborators, so nothing is mocked or stubbed. The unit tests in
`GeometryTests.cs` call the real types directly and compare results with expected values using the xUnit v3 framework.
Each test follows the Arrange, Act, Assert pattern. Invalid color text is verified with a theory that supplies one case
per malformed shape (empty, missing hash, too short, non-hex, sign character, too long).

### Test Environment

No setup beyond the standard xUnit v3 test runner on .NET. No external services, files or configuration are needed, and
the tests are independent of platform and culture.

### Acceptance Criteria

All Geometry unit tests pass, and every expected value matches exactly: arithmetic and edge values are compared for
equality, colors are compared channel by channel, and every invalid input raises the documented exception or returns
false.

### Test Scenarios

#### Point and Rectangle Behavior

- **Point_Operators_AddSubtractAndOffset** (`Plexus-Geometry-PointArithmetic`): operators, `Offset`, `Point.Zero` and
  `Size.Empty` produce the expected values.
- **Rect_Edges_DeriveFromPositionAndSize** (`Plexus-Geometry-RectMeasures`): edges, center, position, size and
  non-emptiness derive from a point and size.
- **Rect_IsEmpty_NonPositiveExtent_ReturnsTrue** (`Plexus-Geometry-RectMeasures`): zero and negative extents are
  empty.
- **Rect_FromPoints_ReversedCorners_ReturnsNormalizedRect** (`Plexus-Geometry-RectFromCorners`): corners given
  bottom-right first still give a positive-size rectangle at the top-left.
- **Rect_Contains_PointsAndRects_HonorsEdges** (`Plexus-Geometry-RectRelations`): edges are inclusive for points and
  rectangles; points just outside are rejected.
- **Rect_IntersectsUnionInflateOffset_ProduceExpectedRects** (`Plexus-Geometry-RectRelations`,
  `Plexus-Geometry-RectTransforms`): intersection, union, inflate, offset and reposition give the expected rectangles.
- **Geometry_ValueTypes_CompareByValueAndHashConsistently** (`Plexus-Geometry-ValueSemantics`): equal components give
  equal values and hash codes, and one differing component breaks equality.

#### Color Parsing and Formatting

- **Color_Parse_RgbHex_ReturnsOpaqueColor** (`Plexus-Geometry-ColorParsing`): `#FF8C00` parses with alpha 255.
- **Color_Parse_ArgbHex_ReturnsColorWithAlpha** (`Plexus-Geometry-ColorParsing`): `#80112233` carries alpha in the
  leading byte.
- **Color_Parse_LowercaseHex_ParsesAndFormatsUppercase** (`Plexus-Geometry-ColorParsing`,
  `Plexus-Geometry-ColorFormatting`): lowercase digits parse and format back in canonical uppercase.
- **Color_Parse_InvalidText_ThrowsFormatException** (`Plexus-Geometry-ColorParseErrors`): six malformed texts are
  rejected by `Parse` with a format error and by `TryParse` with false.
- **Color_Parse_Null_ThrowsArgumentNullException** (`Plexus-Geometry-ColorParseErrors`): null is an argument error.
- **Color_TryParse_Null_ReturnsFalse** (`Plexus-Geometry-ColorParseErrors`): null reports failure without throwing.
- **Color_ToString_RoundTripsThroughParse** (`Plexus-Geometry-ColorFormatting`): opaque and translucent colors format
  canonically and parse back unchanged.
- **Color_NamedStatics_HaveExpectedValues**: the named colors and factories have the documented values (regression
  test without a dedicated requirement).

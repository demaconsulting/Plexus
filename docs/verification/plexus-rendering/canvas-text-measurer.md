## CanvasTextMeasurer Unit Verification Design

Scope: Text measurer backed by CanvasNet fonts, with a bundled fallback font.

### Verification Approach

Unit tests in `CanvasTextMeasurerTests.cs` call the measurer directly with a fixed style and compare measurements with
each other (relative checks such as "longer text is wider" and "double size doubles width") rather than with absolute
widths, because absolute widths depend on which fonts the machine has installed. The real CanvasNet fonts and the real
font lookup are used; no mocks are needed.

### Test Environment

No setup beyond the standard test runner. The tests must pass whether or not the style's font family is installed, which
is why an unknown family is also used to exercise the bundled fallback.

### Acceptance Criteria

A test passes when the measured sizes satisfy the stated relation, the fallback produces a usable width, and null
arguments throw `ArgumentNullException`.

### Test Scenarios

- **CanvasTextMeasurer_Measure_LongerText_IsWider**: longer text is more than three times as wide, and the line height is
  between one and 1.25 times the font size and equal for both texts.
- **CanvasTextMeasurer_Measure_DoubleFontSize_DoublesWidth**: doubling the font size doubles the width.
- **CanvasTextMeasurer_Measure_Bold_IsAtLeastAsWide**: bold text is at least as wide as regular text.
- **CanvasTextMeasurer_Measure_EmptyTextOrZeroSize_HasNoWidth**: empty text has no width and a zero font size gives an
  empty size.
- **CanvasTextMeasurer_Measure_UnknownFamily_FallsBackToBundledFont**: an unknown font family still measures with a
  usable width.
- **CanvasTextMeasurer_Measure_SameInput_IsDeterministic**: repeated measurement is identical and font lookup ignores
  letter case and returns the same font instance.
- **CanvasTextMeasurer_Measure_NullArguments_Throw**: null text or null style throws.

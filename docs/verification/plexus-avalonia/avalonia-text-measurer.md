## AvaloniaTextMeasurer Unit Verification Design

Scope: Text measurer backed by Avalonia text APIs.

### Verification Approach

The measurer is tested directly with a real Avalonia text engine: measuring is a thin adaptation of Avalonia text
formatting, so mocking it would prove nothing. The assertions are relations between measurements (longer text is
wider, a larger font is larger, color changes nothing) rather than absolute pixel sizes, which differ between machines
and fonts. The tests are in `AvaloniaTextMeasurerTests.cs`.

### Test Environment

The standard test runner plus the Avalonia headless platform with Skia drawing, started by the shared fixtures, so that
real font metrics are used. No particular font needs to be installed.

### Acceptance Criteria

A test passes when the measured sizes stand in the specified relation to each other, and invalid arguments raise
`ArgumentNullException`.

### Test Scenarios

- **AvaloniaTextMeasurer_Measure_LongerText_IsWider**: a long text is more than three times as wide as a short one and
  both have the same, positive height.
- **AvaloniaTextMeasurer_Measure_FontSizeAndColor_AffectSizeCorrectly**: doubling the font size nearly doubles the width;
  changing only the color gives an equal size.
- **AvaloniaTextMeasurer_Measure_EmptyText_HasLineHeightOnly**: the empty string has zero width and a positive height
  (boundary: empty input).
- **AvaloniaTextMeasurer_Measure_NullArguments_Throw**: a null text and a null style are rejected (error path).

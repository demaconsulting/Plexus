### TextMeasurement Unit Verification Design

#### Verification Approach

Unit tests in `TextMeasurementTests` call `ApproximateTextMeasurer.Measure` directly, through the `ITextMeasurer`
interface where the contract matters. No collaborators are involved.

#### Test Environment

Standard xunit runner; no files, network or installed fonts. A font family name that cannot exist is used on purpose.

#### Acceptance Criteria

All scenarios pass on every target framework. Relations (wider, equal, proportional) hold within a numeric tolerance
of six decimal places.

#### Test Scenarios

- **Scaling**
  (`ApproximateTextMeasurer_Measure_ScalesWithLengthWeightAndSize`): longer, bolder and larger text is wider, double
  the size is double the width and empty text has zero width.
- **Width classes**
  (`ApproximateTextMeasurer_Measure_NarrowNormalWide_AreOrderedByWidth`): narrow glyphs are narrower than ordinary
  ones, ordinary narrower than wide, and capitals are as wide as wide glyphs.
- **Line height**
  (`ApproximateTextMeasurer_Measure_LineHeight_DependsOnlyOnFontSize`): the height is the same for empty and long
  text and doubles with the font size.
- **Determinism**
  (`ApproximateTextMeasurer_Measure_SameInput_IsDeterministic`): separate instances return equal sizes.
- **Headless**
  (`ApproximateTextMeasurer_AsTextMeasurer_MeasuresWithoutPainterOrFonts`): an unknown family still measures to a
  positive size.
- **Null arguments**
  (`ApproximateTextMeasurer_Measure_NullArguments_ThrowArgumentNullException`): null text and null style.

Boundary values: empty text, single character classes. Error paths: null arguments.

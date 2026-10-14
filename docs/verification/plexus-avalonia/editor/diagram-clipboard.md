### DiagramClipboard Unit Verification Design

Scope: Operating-system clipboard bridge and clipboard text formats.

#### Verification Approach

The bridge is verified against the real Avalonia clipboard of a headless window: the tests wrap the window's clipboard
in the bridge, write and read through it, and inspect the clipboard directly with Avalonia calls, so a passing test
proves what other applications would see. The in-memory test clipboard used by other tests is a stand-in for the
`IDiagramClipboard` abstraction and is not under test here. The text formatting helper is verified through the
properties of the formatted text it returns. The tests are in `DiagramClipboardTests.cs`.

#### Test Environment

The standard test runner plus the Avalonia headless platform with Skia drawing, started by the shared fixtures. The
headless platform provides an in-process clipboard; no operating-system clipboard is touched. Tests that read the
clipboard first clear it so earlier tests cannot leak content.

#### Acceptance Criteria

A test passes when the clipboard content or the formatted text is exactly as specified for the scenario, in both
representations where applicable.

#### Test Scenarios

- **SystemDiagramClipboard_SetAsync_Json_WritesTaggedFormatAndTextTogether**: one write puts the JSON in the Plexus
  format and as plain text.
- **SystemDiagramClipboard_GetAsync_TaggedFormatPresent_PrefersIt**: with differing tagged and text values, the tagged
  value is returned.
- **SystemDiagramClipboard_GetAsync_OnlyPlainText_FallsBackToText**: with only text on the clipboard, the text is
  returned.
- **SystemDiagramClipboard_GetAsync_EmptyClipboard_ReturnsNull**: an empty clipboard yields null (boundary: nothing
  there).
- **TextFormats_Create_EmptyText_FormatsASingleSpace**: empty text has a positive line height and a space width
  (boundary: empty input).
- **TextFormats_Create_LargerFontSize_IsTallerAndWider**: the font size reaches the formatted text.
- **TextFormats_Create_BoldStyle_IsAtLeastAsWideAsRegular**: the bold typeface is used, and repeated requests give the
  same result.

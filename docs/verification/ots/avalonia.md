## Avalonia Verification

This document provides the verification evidence for the Avalonia OTS software item. Requirements for this OTS item
are defined in the Avalonia OTS Software Requirements document.

### Required Functionality

Avalonia provides the controls, drawing context, input, text, clipboard and headless platform used by the
PlexusAvalonia system. The framework has its own extensive vendor test suite, so Plexus verifies only the
capabilities it relies on, using the PlexusAvalonia tests that run the editor control on the Avalonia headless
platform.

### Verification Approach

No dedicated OTS test project is used. The PlexusAvalonia tests create the editor control in a headless Avalonia
window, simulate input, render the control and assert on the results, so a passing test run proves the Avalonia
behavior required by the requirements below. Each scenario names a test method that ReqStream matches in the TRX
results. The headless platform itself is proven by every test running at all.

### Test Scenarios

#### Rendering (Plexus-OTS-Avalonia-Render)

- **DiagramEditor_Render_SmallDiagram_PaintsNodesWiresAndText**: a rendered editor contains node, wire and text
  pixels.
- **DiagramEditor_Render_GroupsAndConnectPreview_SavesPicture**: group frames and the connection preview are drawn
  and the result can be captured.

**Expected**: each test passes, proving custom drawing and headless capture work.

#### Input (Plexus-OTS-Avalonia-Input)

- **DiagramEditor_CtrlWheel_ZoomsAroundCursor**: Ctrl and the mouse wheel zoom around the pointer.
- **DiagramEditor_Wheel_WithoutModifiers_Scrolls**: the mouse wheel scrolls the view.
- **DiagramEditor_MiddleDrag_PansView**: dragging with the middle button pans the view.
- **DiagramEditor_CtrlZAndCtrlY_UndoAndRedo**: keyboard shortcuts reach the focused control.
- **DiagramEditor_F2_SelectedNode_StartsRename**: the F2 key starts an inline rename.

**Expected**: each test passes, proving pointer, wheel and keyboard input reach the control.

#### Text (Plexus-OTS-Avalonia-Text)

- **AvaloniaTextMeasurer_Measure_LongerText_IsWider**: measured width grows with text length.
- **AvaloniaTextMeasurer_Measure_FontSizeAndColor_AffectSizeCorrectly**: measured size follows the font size.
- **AvaloniaTextMeasurer_Measure_EmptyText_HasLineHeightOnly**: empty text measures as one line height.

**Expected**: each test passes, proving text measurement is consistent.

#### Clipboard (Plexus-OTS-Avalonia-Clipboard)

- **DiagramEditor_CopyPaste_ConnectedPair_RoundTrips**: copied content pastes back identically.
- **DiagramEditor_CopyPaste_SystemClipboard_RoundTripsWhenAvailable**: content round-trips through the clipboard
  service when one is available.

**Expected**: each test passes, proving text transfer through the clipboard.

#### Headless platform (Plexus-OTS-Avalonia-Headless)

- **DiagramEditor_Defaults_NewEditor_HasDocumentedValues**: a control can be created and inspected without a display.
- **DiagramEditor_ZoomToFit_FarApartNodes_BringsBothIntoView**: layout runs and produces sizes without a display.

**Expected**: each test passes, proving controls can be hosted without a display.

### Requirements Coverage

- **`Plexus-OTS-Avalonia-Render`**: DiagramEditor_Render_SmallDiagram_PaintsNodesWiresAndText,
  DiagramEditor_Render_GroupsAndConnectPreview_SavesPicture
- **`Plexus-OTS-Avalonia-Input`**: DiagramEditor_CtrlWheel_ZoomsAroundCursor, DiagramEditor_Wheel_WithoutModifiers_Scrolls,
  DiagramEditor_MiddleDrag_PansView, DiagramEditor_CtrlZAndCtrlY_UndoAndRedo, DiagramEditor_F2_SelectedNode_StartsRename
- **`Plexus-OTS-Avalonia-Text`**: AvaloniaTextMeasurer_Measure_LongerText_IsWider,
  AvaloniaTextMeasurer_Measure_FontSizeAndColor_AffectSizeCorrectly,
  AvaloniaTextMeasurer_Measure_EmptyText_HasLineHeightOnly
- **`Plexus-OTS-Avalonia-Clipboard`**: DiagramEditor_CopyPaste_ConnectedPair_RoundTrips,
  DiagramEditor_CopyPaste_SystemClipboard_RoundTripsWhenAvailable
- **`Plexus-OTS-Avalonia-Headless`**: DiagramEditor_Defaults_NewEditor_HasDocumentedValues,
  DiagramEditor_ZoomToFit_FarApartNodes_BringsBothIntoView

- **`Plexus-OTS-Avalonia-ToolTips`**: DiagramEditor_ToolTip_OpenThenMoveToAnotherElement_ShowsNewContent,
  DiagramEditor_ToolTip_PointerLeaves_ClearsTip

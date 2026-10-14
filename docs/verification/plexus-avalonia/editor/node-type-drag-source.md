### NodeTypeDragSource Unit Verification Design

Scope: Helper that lets hosts start drag-and-drop of node types onto the editor.

#### Verification Approach

The helper is created directly on a control (a bordered box in a headless window) and driven with simulated pointer
input. The headless platform has no operating-system drag support and offers no replaceable drag service, so a started
drag cannot be observed at the platform. The tests therefore observe the helper's own state: the pending press it holds
is consumed exactly when it asks the editor to start the drag (the private state is read by reflection in the test
project). That the drag payload and drop result are right is verified by the DiagramEditor drop tests and by the Editor
subsystem test. The tests are in `NodeTypeDragSourceTests.cs`.

#### Test Environment

The standard test runner plus the Avalonia headless platform started by the shared fixtures. The control is placed in
the middle of a 200 by 200 window, and pointer positions are inside it.

#### Acceptance Criteria

A test passes when the pending press is held, consumed or cleared as specified for the pointer sequence, and no
exception escapes the helper.

#### Test Scenarios

- **NodeTypeDragSource_Move_PastThreshold_ConsumesPressToStartDrag**: a press is pending, a two-pixel movement keeps it
  pending, and a forty-pixel movement consumes it (boundaries below and above the threshold).
- **NodeTypeDragSource_Release_AbandonsPendingPress**: after a release no movement starts anything.
- **NodeTypeDragSource_Press_RightButton_DoesNotArm**: only the left button arms.
- **NodeTypeDragSource_Dispose_StopsListening**: a disposed helper ignores presses.
- **NodeTypeDragSource_Move_PlatformCannotDrag_IsIgnoredAndStaysUsable**: on the headless platform, where starting a
  drag is not supported, a complete press-drag-release sequence raises nothing and the next press arms again.

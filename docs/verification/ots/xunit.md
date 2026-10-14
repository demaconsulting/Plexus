## xUnit Verification

This document provides the verification evidence for the xUnit OTS software item. Requirements
for this OTS item are defined in the xUnit OTS Software Requirements document.

### Required Functionality

xUnit v3 (xunit.v3 and xunit.runner.visualstudio) is the unit-testing framework used by the
project. It discovers and runs all test methods and writes TRX result files that feed into coverage
reporting and requirements traceability. Passing tests confirm the framework is functioning
correctly.

### Verification Approach

xUnit is verified by self-validation evidence from the CI pipeline. Each scenario names a specific
test method in the Plexus system tests that xUnit must discover, execute, and record in a TRX result
file. A passing pipeline run for all scenarios constitutes evidence that both requirements are
satisfied.

### Test Scenarios

#### Plexus_HelloDiagram_DefaultConfiguration_BuildsConnectedDiagram

**Scenario**: xUnit discovers and runs this test; the test verifies that a default-configured diagram can be built,
connected, edited and queried.

**Expected**: xUnit executes the test, the test passes, and the result appears in the TRX output.

**Requirement coverage**: `Plexus-OTS-xUnit-Execute`, `Plexus-OTS-xUnit-Report`.

#### Plexus_EditingSession_UndoAllThenRedoAll_RestoresExactStates

**Scenario**: xUnit discovers and runs this test; the test verifies that a long editing session can be fully undone
and redone.

**Expected**: xUnit executes the test, the test passes, and the result appears in the TRX output.

**Requirement coverage**: `Plexus-OTS-xUnit-Execute`, `Plexus-OTS-xUnit-Report`.

#### Plexus_Transaction_WholeGesture_UndoesAsOneStep

**Scenario**: xUnit discovers and runs this test; the test verifies that a transaction groups a whole gesture into
one undo step.

**Expected**: xUnit executes the test, the test passes, and the result appears in the TRX output.

**Requirement coverage**: `Plexus-OTS-xUnit-Execute`, `Plexus-OTS-xUnit-Report`.

#### Plexus_LoadingWithSuspendedHistory_LeavesCleanEmptyHistory

**Scenario**: xUnit discovers and runs this test; the test verifies that content loaded with history suspended
leaves a clean, empty history.

**Expected**: xUnit executes the test, the test passes, and the result appears in the TRX output.

**Requirement coverage**: `Plexus-OTS-xUnit-Execute`, `Plexus-OTS-xUnit-Report`.

### Requirements Coverage

- **`Plexus-OTS-xUnit-Execute`**: Plexus_HelloDiagram_DefaultConfiguration_BuildsConnectedDiagram,
  Plexus_EditingSession_UndoAllThenRedoAll_RestoresExactStates,
  Plexus_Transaction_WholeGesture_UndoesAsOneStep,
  Plexus_LoadingWithSuspendedHistory_LeavesCleanEmptyHistory
- **`Plexus-OTS-xUnit-Report`**: Plexus_HelloDiagram_DefaultConfiguration_BuildsConnectedDiagram,
  Plexus_EditingSession_UndoAllThenRedoAll_RestoresExactStates,
  Plexus_Transaction_WholeGesture_UndoesAsOneStep,
  Plexus_LoadingWithSuspendedHistory_LeavesCleanEmptyHistory

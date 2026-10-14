# PlexusAvaloniaPropertyGrid System Verification Design

Scope: Generic, annotation-driven property grid for Avalonia: the UI-free layers and the Avalonia controls that show and
edit any annotated .NET object through System.ComponentModel annotations, type converters, edit sessions and a pluggable
editor registry.

## Verification Approach

System verification is end to end through the public API: small original test objects (a mixer with levels and
channels, a person with every supported annotation) are shown through `ReflectionPropertySource`, ordered with
`PropertyOrdering`, and edited through `PropertyEditSession`, and the results are checked on the objects themselves.
There are no mocks; all ten units are real. The tests are xUnit tests in `PlexusAvaloniaPropertyGridTests.cs` in the
PlexusAvaloniaPropertyGrid test project, sharing the fixtures in `TestModels.cs` and `TestHelpers.cs`. Unit-level
behavior is verified separately (see the unit verification designs). The shared `TestApp.cs` and `UiTest.cs` start the
Avalonia headless platform (Fluent theme, Skia drawing) on which the control scenarios run; `GridHarness.cs` hosts
controls in a window, finds them by automation id, presses keys and captures screenshots, and `SampleModels.cs` holds the
original sample object. The control scenarios drive the real controls and check the objects they edit; the screenshot
tests (`PropertyGridScreenshotTests.cs`) render the grid, the dialog and the collection editor to PNG files in the
`plexus-pg` folder of the temp path, which are reviewed by eye for a clean, professional look.

## Test Environment

- .NET 10 test runner, run on Windows, Linux and macOS in the build pipeline; the platform requirements use source
  filters so each platform's own results are the evidence.
- No display or network is needed (the Avalonia headless platform renders to memory).
- Text assertions pass an explicit culture where the result depends on it, and the one test that changes the current
  culture restores it afterwards.

## Acceptance Criteria

A system test passes when the shown properties, categories, labels, descriptions and texts match the annotations of the
test object, when edits reach the object only at the documented moment (immediately in live mode, at commit in staged
mode), when failures are reported as results and not exceptions, when a Plexus node is edited as one undo entry per
commit, and when the generic layers use no Plexus type.
All system tests must pass on every supported platform for the platform requirements to be satisfied.

## Test Scenarios

- **PlexusAvaloniaPropertyGrid_ReflectionSource_ShowsAnnotatedObject**: a mixer object is shown in categories
  (Channels, Levels) with labels, a description, the text of a value, the changed state of a property with a default,
  without its hidden property, and a collection as `(Collection)`. Also the platform-evidence scenario for every platform
  requirement.
- **PlexusAvaloniaPropertyGrid_StagedSession_EditsAndCommitsAnObject**: in a staged session a text edit is buffered, an
  invalid text is reported as an error, a channel is added through a collection editor, the object is untouched until
  commit, and commit applies everything inside one commit scope.
- **PlexusAvaloniaPropertyGrid_LiveSessionOverComposite_WritesThroughBothSources**: a live session over a composite of a
  host-supplied item and a reflection source writes through both and lists the host item first.
- **PlexusAvaloniaPropertyGrid_EditorRegistry_ResolvesEditorsForAnnotatedProperties**: an annotated property resolves
  to its registered editor key, an enumeration to the registration for `Enum`, and other properties to the fallback.
- **PlexusAvaloniaPropertyGrid_PropertyGrid_ShowsAndEditsAnAnnotatedObject**: the grid shows the categories of the sample
  object, a typed caption is written and shown in bold, and an out-of-range timeout is refused with the red error border
  (error path).
- **PlexusAvaloniaPropertyGrid_PropertyGridDialog_CommitsOrDiscardsTheEdits**: the dialog helper commits on OK inside the
  host scope (one call) and returns true; a second dialog discards its edit on Cancel and returns false.
- **PlexusAvaloniaPropertyGrid_CollectionEditor_EditsCollectionThroughWorkingCopy**: in a staged session the collection
  editor adds, moves and renames a member on its working list; OK records a pending edit that the commit applies.
- **PlexusAvaloniaPropertyGrid_CustomEditor_IsUsedForAnnotatedProperty**: a slider registered under the key of an
  annotation edits the annotated property.
- **PlexusAvaloniaPropertyGrid_PlexusNode_IsShownAndEditedAsOneUndoEntry**: a node given to the factory is shown
  by the node source; a staged commit of a rename, a property and a data edit is one undo entry; two live edits are
  two entries; the previous data object is never mutated.
- **PlexusAvaloniaPropertyGrid_NodePropertiesDialog_CommitsOrDiscardsAsOneUndoEntry**: a node dialog cancelled changes
  nothing; a second one commits a rename and a data edit with OK as one undo entry.
- **PlexusAvaloniaPropertyGrid_NodePropertiesDialog_PreviewFollowsStagedEditsAndCommitsOnce**: a node dialog with a live
  preview shows a staged frequency in the preview before OK, Cancel leaves the node unchanged, and a second dialog commits
  a frequency and a rename with OK as one undo entry.
- **PlexusAvaloniaPropertyGrid_Dependencies_GenericLayers_UseNoPlexusTypes**: no type outside the Plexus adapter
  namespace has a field, property, method, constructor or base type from a Plexus namespace.
- **PlexusAvaloniaPropertyGrid_HeadlessHost_RunsOnTheUiThread**: the headless Avalonia host used by the control tests starts
  and runs a body on the UI thread (support check; no requirement depends on it).

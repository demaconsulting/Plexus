## PropertyGridControl Unit Verification Design

Scope: The `PropertyGrid` Avalonia control: the two-column layout, categories, sorting, search, expansion, selection,
description pane and keyboard handling over a property source and an edit session.

### Verification Approach

The control is tested on the headless Avalonia platform with the Fluent theme and Skia drawing, so layout, focus, input
and rendering are real. A test hosts a `PropertyGrid` in a window over a small original annotated object (`Widget`),
drives it through its public API, raw key events and routed events, and asserts on the visual tree (found through the
automation ids of the rows, editors and toolbar), on the shown rows and on the edited object. There are no mocks. The
tests are in `PropertyGridDisplayTests.cs` and `PropertyGridKeyboardTests.cs`; `GridHarness.cs` provides the hosting, the
lookup by automation id, key presses and the screenshot capture, and `PropertyGridScreenshotTests.cs` renders the grid
to PNG files for visual review.

### Test Environment

- .NET 10 test runner on Windows, Linux and macOS; the Avalonia headless platform needs no display.
- One headless session is shared by the whole test run (`UiTest`), and every test body runs on its UI thread.
- Screenshots are written to the `plexus-pg` folder of the temp path.

### Acceptance Criteria

A test passes when the visual tree matches the documented rows and states, selection and focus end where the keyboard
rules say, and the edited object holds what the edit mode promises. Screenshot tests pass when the frame is captured; their
content is reviewed by looking at the images (tight rows, subtle grid lines, category bands, indentation of nested rows,
light and dark themes).

### Test Scenarios

- **PropertyGrid_Rows_ShowCategoriesAndBrowsableProperties**: category bands and rows in the documented order, no hidden
  property.
- **PropertyGrid_Rows_AreCompact**: every row is 22 pixels high.
- **PropertyGrid_SortToggle_SwitchesBetweenCategorizedAndAlphabetical**: the toggles switch the order, show the mode and
  remove or restore the category bands.
- **PropertyGrid_Search_FiltersRowsByName** /
  **PropertyGrid_Search_FindsNestedPropertiesAndReportsNoMatch**: filtering by typed text, clearing it, matching
  nested properties (their parents expand) and the no-match message (error path).
- **PropertyGrid_Category_CollapsesAndRemembersState**: collapsing hides the rows and survives a change of object.
- **PropertyGrid_NestedObject_ExpandsToIndentedChildren**: child rows appear one level deeper and disappear on collapse.
- **PropertyGrid_ReadOnlyRow_IsDimmedAndNotEditable** / **PropertyGrid_ChangedValue_IsBold**: dimmed read-only
  rows with a text value; bold when the value differs from the default, regular again when it is restored.
- **PropertyGrid_Description_FollowsSelection**: name and description of the selected item.
- **PropertyGrid_Chrome_CanBeHidden** / **PropertyGrid_ColumnSplitter_ResizesNameColumn**: hiding the toolbar, search and
  description; dragging the splitter resizes the name column of every row.
- **PropertyGrid_CustomSource_ReplacesSelectedObject** / **PropertyGrid_ObservableObject_ShowsExternalChanges**: a custom
  source replaces the object and vice versa, clearing the object removes the session; change notifications refresh values.
- **PropertyGrid_Rows_HaveAutomationIdsAndNames**: ids and names of rows and editors, nested paths included.
- **PropertyGrid_SelectedObjectSource_IsDisposedByTheGridButHostSourceIsNot** and
  **PropertyGrid_SelectedObjectSource_IsReleasedWhileDetachedAndRecreatedOnAttach**
  (`PlexusAvaloniaPropertyGrid-PropertyGridControl-SourceOwnership`, `NodePropertiesDialogTests.cs`): the source the grid
  created stops listening when the content is replaced and while the grid is out of the visual tree, the grid shows the
  same node with the same expansion after it returns, and a host-assigned source keeps working after the grid let go
  of it.
- **PropertyGrid_Keyboard_...**: Up, Down, Home, End; Left and Right on categories and nested objects; Enter and F2
  into the editor, Escape out; Enter toggling categories and nested objects; Tab and Shift+Tab between name and value;
  keys typed in the search box leave the selection alone (error path: keys the grid must not take).
- **PropertyGrid_Screenshot_CategorizedDark / AlphabeticalDark / CategorizedLight**: render the grid in both theme
  variants with a nested object expanded, a selection and a rejected value.

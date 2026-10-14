### Group Unit Verification Design

Scope: Nestable, collapsible group of elements with computed bounds.

#### Verification Approach

The unit is verified with unit tests that use the real Model classes through the public API (no mocks): groups are
created with the page API around nodes and annotations of the shared test node types, and member nodes are measured
with the Scene node geometry to compute the expected frame. Undo and redo are checked with the shared round-trip
helper, which compares a full-state dump of the diagram after each step. All tests live in `GroupTests.cs`.

#### Test Environment

Standard xUnit v3 runner on .NET with no external services, files or configuration.

#### Acceptance Criteria

Every scenario below passes on all target frameworks: members, bounds and flags equal the expected values, one
undoable history entry exists per change, none for a no-op, and undo and redo restore the exact prior state.

#### Test Scenarios

**Members** (`Plexus-Model-Group-Members`)

- `Group_AddMemberAndRemoveMember_MoveElementsBetweenGroupsUndoably`: add, move and remove members undoably.
- `Group_Members_AddAndRemove_RaiseCollectionChangedAndKeepOrder`: the read-only list reports changes in order.
- `Group_AddMember_AlreadyMemberOfSameGroup_RecordsNothing`: adding an existing member records nothing.

**Member rules** (`Plexus-Model-Group-MemberRules`)

- `Group_AddMemberAndRemoveMember_InvalidOperations_Throw`: cycles, wires and non-members are rejected.

**Nesting** (`Plexus-Model-Group-Nesting`)

- `Group_AllMembers_IncludesNestedGroupsAndTheirMembers`: depth-first enumeration of all descendants.

**Bounds** (`Plexus-Model-Group-Bounds`)

- `Group_Bounds_ExpandedFitsMemberExtentsWithPaddingAndTitle`: union of member extents plus padding and title bar,
  following a member move.
- `Group_Bounds_EnclosesNameSummaryAndPortLabels`: node text and port labels lie inside the frame.
- `Group_Bounds_NestedGroup_OuterContainsInnerAndSiblings`: an outer frame contains nested frames and siblings.
- `Group_Bounds_MemberJoinsOrLeaves_BoundsFollowMembers`: the frame grows and shrinks with its members.
- `Group_Bounds_EmptyGroup_FixedBoxAtCollapsedPosition`: an empty group is a fixed-size box.

**Collapse** (`Plexus-Model-Group-Collapse`)

- `Group_IsCollapsed_SetTrueAndFalse_UndoRedoRoundTrip`: collapsing pins the box and expanding restores the frame.
- `Group_IsCollapsed_SameValue_RecordsNothing`: an unchanged state records nothing.

**Collapse hides members** (`Plexus-Model-Group-CollapseHidesMembers`)

- `Group_IsCollapsed_HidesMembers`: members are hidden but stay in the group.
- `Group_IsCollapsed_NestedMember_IsNotVisibleUntilExpanded`: the group stays visible; members return on expand.

**Title** (`Plexus-Model-Group-Title`)

- `Group_Title_Set_UndoRedoRoundTrip`: title edits are undoable; null throws.
- `Group_Title_SameValue_RecordsNothing`: an unchanged title records nothing.

**Detached** (`Plexus-Model-Group-Detached`)

- `Group_Setters_RemovedGroup_ThrowInvalidOperationExceptionUntilUndo`: a removed group rejects changes until undo.

**Change notification** (`Plexus-Model-Group-ChangeNotification`)

- `Group_PropertyChanged_TitleCollapseAndMembers_RaisedForSetAndUndo`: notifications for title, collapse, members and
  bounds, including undo.
- `Group_PropertyChanged_MemberJoinsNestedGroup_ReportsBoundsOnAncestors`: ancestors are told about bounds changes.

### PlexusClipboard Unit Verification Design

#### Verification Approach

Unit tests in `ClipboardTests`, `ClipboardSecurityTests` and `DuplicateTests` copy, paste and duplicate on real
diagrams built from `SerializationFixtures`, with state compared through the model dump, the undo count and the
returned `PasteResult`. Hostile content is literal JSON text or generated strings, assembled by small helpers around a
valid envelope. The security tests use one assertion helper that requires, for every rejection, a failed result with a
message, no created elements, an unchanged dump and an unchanged undo count. Nothing is mocked; connection-rule
rejection uses a real host rule delegate.

#### Test Environment

Standard xunit runner; no files, network or system clipboard. The size tests allocate strings of about 8 MiB.

#### Acceptance Criteria

All scenarios pass on every target framework. Successful pastes create exactly the expected elements and warnings;
every rejection leaves the page, diagram and history exactly as before and never throws for bad content.

#### Test Scenarios

Copy:

- **Format marker** (`PlexusClipboard_Copy_WritesFormatIdAndVersion`).
- **Selection expansion**: group members recursively (`PlexusClipboard_Copy_Group_CopiesMembersRecursively`), wire
  with one copied end excluded (`PlexusClipboard_Copy_WireWithOneEndCopied_IsExcluded`) and wire between copied nodes
  included (`PlexusClipboard_Copy_WireBetweenCopiedNodes_IsIncludedImplicitly`).
- **Opaque data omitted** (`PlexusClipboard_Copy_OpaqueData_IsOmitted`).

Paste behavior:

- **Copies, ids and names**: offset copies
  (`PlexusClipboard_Paste_WithinPage_CreatesOffsetCopiesWithNewIdsAndNames`), distinct ids
  (`PlexusClipboard_Paste_Twice_GeneratesDistinctIds`), increasing suffixes
  (`PlexusClipboard_Paste_RepeatedPaste_AppendsIncreasingNumericSuffix`), names kept when uniqueness is not required
  (`PlexusClipboard_Paste_UniqueNamesNotRequired_KeepsNames`) and a name used on another page
  (`PlexusClipboard_Paste_NameUsedOnOtherPage_IsMadeUnique`).
- **Position** (`PlexusClipboard_Paste_AtPosition_PlacesTopLeftOfContent`).
- **Undo and redo** (`PlexusClipboard_Paste_UndoAndRedo_RestoreExactState`) and **empty selection**
  (`PlexusClipboard_Paste_EmptySelection_SucceedsWithoutHistoryEntry`).
- **Across pages and diagrams** (`PlexusClipboard_Paste_AcrossPages_AddsToTargetPageOnly`,
  `PlexusClipboard_Paste_AcrossDiagrams_RestoresPropertiesDataAndGroups`).
- **Warnings**: property kind mismatch (`PlexusClipboard_Paste_PropertyKindMismatch_UsesDefaultAndWarns`), choice not
  offered (`PlexusClipboard_Paste_ChoiceNotInTargetOptions_UsesDefaultAndWarns`), compatible numeric kind without
  warning (`PlexusClipboard_Paste_CompatibleNumericKind_ConvertsWithoutWarning`), wire rejected by a host rule
  (`PlexusClipboard_Paste_WireDeniedByHostRule_IsSkippedWithWarning`) and unreadable payload
  (`PlexusClipboard_Paste_UnreadablePayload_WarnsAndPastesWithoutData`).
- **Argument checking** (`PlexusClipboard_InvalidArguments_Throw`).
- **Missing types** (`PlexusClipboard_Paste_MissingNodeTypes_FailsAtomicallyListingAllKeys`,
  `PlexusClipboard_Paste_UnknownTypeInHostileContent_ListsMissingKeys`).

Untrusted input (all rejections change nothing):

- **Foreign content** (`PlexusClipboard_Paste_NotPlexusData_IsRejected`, theory: null, empty, white space, plain text,
  array, null literal, truncated JSON, foreign format marker, unsupported version, missing marker, missing version).
- **Size** (`PlexusClipboard_Paste_Oversize_IsRejected`, `PlexusClipboard_Paste_OversizeInBytes_IsRejected`: few
  characters but more than 8 MiB of UTF-8).
- **Element count** (`PlexusClipboard_Paste_TooManyElements_IsRejected` at 10,001 and
  `PlexusClipboard_Paste_MaximumElements_IsAccepted` at 10,000).
- **Depth** (`PlexusClipboard_Paste_TooDeep_IsRejected`, `PlexusClipboard_Paste_DeeplyNestedGroups_AreRejected`).
- **Geometry** (`PlexusClipboard_Paste_BadGeometry_IsRejected`, theory: negative size, NaN, string, 1e308, 1e999,
  missing member; `PlexusClipboard_Paste_BadRoute_IsRejected`).
- **Consistency** (`PlexusClipboard_Paste_InconsistentContent_IsRejected`, theory: unknown node, port or direction,
  self connection, duplicate wires, duplicate and empty ids, empty names, null entries, groups with unknown or shared
  members, cycles, bad z-orders, invalid annotation fill and malformed property values).
- **Type-name members** (`PlexusClipboard_Paste_TypeNameMembers_AreIgnored`), **hostile payload**
  (`PlexusClipboard_Paste_HostilePayload_BecomesWarning`) and **late failure**
  (`PlexusClipboard_Paste_LateFailure_LeavesNothingBehind`: fifty valid nodes followed by one bad wire).

Duplicate:

- **Nodes and wire as one undo entry** (`DiagramPage_Duplicate_NodesAndWire_ClonesAtOffsetAsOneUndoEntry`), **groups**
  (`DiagramPage_Duplicate_Group_ClonesMembersAndStructure`), **payloads**
  (`DiagramPage_Duplicate_Data_DeepCopiedWhenSerializableOtherwiseShared`), **independent properties**
  (`DiagramPage_Duplicate_Properties_AreCopiedIndependently`), **empty selection**
  (`DiagramPage_Duplicate_EmptySelection_DoesNothing`), **argument checking**
  (`DiagramPage_Duplicate_InvalidArguments_Throw`). A payload of the wrong type can no longer reach a duplicate because
  `Node.Data` refuses it at assignment (`Node_Data_IncompatibleType_ThrowsArgumentException`).

Boundary values: exactly 10,000 elements accepted and 10,001 rejected, sizes at the byte limit, extreme coordinates,
nesting depth at and beyond the limit. Error paths: all rejection and argument scenarios.

Paste message (`Plexus-Serialization-PlexusClipboard-PasteMessage`, `PasteResultMessageTests`):

- **PasteResult_Message_Success_SummarizesElements**, **PasteResult_Message_SuccessWithWarnings_IncludesThem**,
  **PasteResult_Message_MissingNodeTypes_NamesThem** and **PasteResult_Message_OtherFailure_UsesTheError**: the sentence
  of a plain success, a success with a skipped wire, a refused paste with unknown types and a foreign or empty
  clipboard.

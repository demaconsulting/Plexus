## EditorRegistry Unit Verification Design

Scope: Names custom editors by annotation and maps editor keys and property types to the editors of the user interface
layer.

### Verification Approach

The registry is tested directly with strings as the editors it stores (the registry does not care what an editor is),
and with items from the reflection source of the annotated `Person` test object for the item resolution. No mocks are
used. The tests are in `EditorRegistryTests.cs`.

### Test Environment

The standard test runner.

### Acceptance Criteria

A test passes when each lookup returns the documented editor or null, the exact registration wins over base types,
interfaces and generic definitions, and invalid arguments are rejected.

### Test Scenarios

- **PropertyEditorAttribute_KeyOrType_SetsTheEditorKey**: key or type gives the editor key; an empty key and a missing
  type are rejected (error paths).
- **PropertyEditorRegistry_ResolveByKey_IsCaseInsensitiveAndRemovable**: keys match ignoring case, registrations can be
  removed, invalid arguments are rejected (error paths).
- **PropertyEditorRegistry_ResolveByType_WalksBasesInterfacesAndGenerics**: exact type, base type, open generic,
  nullable underlying type and interface, and removal; null rejected (error paths).
- **PropertyEditorRegistry_ResolveItem_KeyThenTypeThenFallback**: key first, then type, then fallback; null rejected
  (error path).

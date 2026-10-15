## PropertyModel Unit Verification Design

Scope: Describes one property row: metadata from annotations, value access, type-converter text conversion, standard
values, expandable children, collection elements and validation results.

### Verification Approach

The unit is tested through items created by the reflection source over small original test classes (`Person`,
`Address`, `Dimensions`, `Tag`, `Temperature`, and a few holders) that carry every supported annotation, so each
annotation is checked at the item that exposes it, together with the real `System.ComponentModel` type descriptors and
converters. A custom item with throwing delegates covers failure paths. The tests are in `PropertyAnnotationTests.cs`
(annotations and defaults) and `PropertyModelTests.cs` (values, text, standard values, children, collections and
validation). No mocks are used.

### Test Environment

The standard test runner. Tests that depend on the culture pass an explicit culture, or set the current culture and
restore it in a `finally` block.

### Acceptance Criteria

A test passes when the item reports the metadata given by the annotation, when text conversion gives the expected text
and value in the requested culture, when children and elements have the documented paths and write through to the target,
and when each failure is reported as a failed result with a message and leaves the target unchanged.

### Test Scenarios

Annotations and defaults (`PropertyAnnotationTests.cs`):

- **PropertyItem_Annotation_Category_DefaultsToMiscAndHonorsAnnotation**: an unannotated property is in "Misc"; a
  `Category` annotation is used.
- **PropertyItem_Annotation_DisplayNameAndDescription_AreHonored**: `DisplayName` and `Description` are exposed; the
  name is the default display name.
- **PropertyItem_Annotation_DisplayAttribute_SuppliesNameDescriptionAndGroup**: `Display` supplies name, description and
  group when the component model attributes are absent.
- **PropertyItem_Annotation_ReadOnly_AnnotationGetterOnlyAndEditable**: `ReadOnly`, a missing setter and `Editable(false)`
  make an item read-only (error path: a writable item is not).
- **PropertyItem_Annotation_PasswordAndParenthesize_AreHonored**: masking and the parenthesized label.
- **PropertyItem_Annotation_RefreshProperties_IsExposed**: the refresh mode is exposed.
- **PropertyItem_Annotation_PropertyEditorAnnotation_OnPropertyAndType**: the editor key comes from the property
  annotation, else from the type annotation; an editor type gives the type's name as key.
- **PropertyItem_Annotation_UIHint_SuppliesTheEditorKey**: a `UIHint` on a property supplies the editor key (also over a
  type-level editor annotation); `PropertyEditor` wins when both are present; among several hints the `Avalonia` layer
  wins over a layer-less one; a hint for another presentation layer is ignored (error path: the key stays `null`).
- **PropertyItem_Annotation_DefaultValue_IsChangedTracksTheCurrentValue**: an item is changed when it differs from its
  default and an item without a default is never changed.
- **PropertyItem_Annotation_ShouldSerializePattern_DrivesIsChanged**: the `ShouldSerialize<Name>` rule drives the changed
  state without a known default value.
- **PropertyItem_Annotation_Reset_RestoresTheDefaultValue**: reset restores the default value and the `Reset<Name>` rule,
  and fails for an item without a default.
- **PropertyItem_Annotation_TypeConverterAnnotation_IsUsed**: the converter named by annotation converts the value.

Values, text, standard values, children, collections and validation (`PropertyModelTests.cs`):

- **PropertyItem_GetValueSetValue_ReadAndWriteTheTarget**: values and the path round-trip through an item.
- **PropertyItem_TextConversion_UsesTheRequestedCultureAndDefaultsToCurrent**: German and invariant formatting and
  parsing, and the current culture as the default.
- **PropertyItem_CustomConverter_RoundTripsWithCulture**: a custom converter receives the culture and round-trips.
- **PropertyItem_TrySetText_InvalidTextFailsWithoutChangingTheValue**: invalid text is a failed result, leaves the value,
  and the raw conversion throws (error path).
- **PropertyItem_TextConversion_HandlesNullNullableAndObject**: null is empty text, an empty nullable is null, an
  object-typed property takes any text.
- **PropertyItem_TextConversion_UnsupportedTypeReportsAnError**: a type without text form reports the error (error path).
- **PropertyItem_Enum_ParsesMemberNames**: enumeration member names parse and unknown names fail.
- **PropertyItem_StandardValues_EnumFlagsAndBoolean**: enumeration, flags and boolean lists and exclusivity.
- **PropertyItem_StandardValues_CanDependOnTheInstance**: a converter's list depends on the owner instance.
- **PropertyItem_Children_ExpandableObjectExposesNestedProperties**: children, paths, write-through, defaults of children.
- **PropertyItem_Children_FollowTheCurrentValue**: children are rebuilt for a new value and empty for null.
- **PropertyItem_Children_ValueTypeParentReceivesTheModifiedStruct**: a struct child edit is written back.
- **PropertyItem_Children_ReadOnlyValueTypeHasReadOnlyChildren**: children of a read-only struct are read-only.
- **PropertyItem_Children_ObjectTypedPropertyUsesTheValueConverter**: an object-typed property expands by its value.
- **PropertyItem_IsCollection_RecognizesCollectionTypes**: list, array, set and read-only list are collections; string and
  dictionary are not.
- **PropertyItem_GetCollectionItems_ReadAndWriteElements**: element items read and write with indexed paths.
- **PropertyItem_GetCollectionItems_ReadOnlyCollectionElementsAreReadOnly**: read-only collection elements cannot be set.
- **PropertyItem_GetCollectionItems_ExpandableElementHasChildren**: an expandable element has children under its path.
- **PropertyItem_ConvertToString_CollectionShowsCollectionText**: a collection is shown as `(Collection)`.
- **PropertyItem_TrySetValue_ReportsValidationAndSetterFailures**: null, wrong type, range annotation and a throwing
  setter are failed results (error paths).
- **PropertyItem_TrySetValue_ReadOnlyItemFails**: a read-only item cannot be written (error path).
- **PropertyItem_SupportsTextEditing_ConverterThrows_ReportsNotSupported**
  (`PlexusAvaloniaPropertyGrid-PropertyModel-ConverterProbeFailure`): a property whose converter throws from
  `CanConvertFrom` reports `SupportsTextEditing` false without throwing, while an ordinary string property still reports
  true (error path).
- **PropertyItem_TryGetValue_GetterFailureIsCaptured**: a throwing getter is a failed result and empty text (error path).
- **PropertyResult_SuccessAndFailure_CarryTheOutcome**: the result type carries the success flag and message.

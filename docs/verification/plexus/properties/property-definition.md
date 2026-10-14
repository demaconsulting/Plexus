### PropertyDefinition Unit Verification Design

Scope: Property metadata: name, value kind, default, constraints and editor hints.

#### Verification Approach

The unit is verified with isolated unit tests that construct definitions directly; no diagram is needed. Tests assert
the inferred kind and canonical type of defaults, the presentation members, the formatted text and the errors raised for
invalid declarations. The kind, choice and summary tests were written together with the PropertyBag tests and live in
the PropertyBag test class; the remaining tests are in the PropertyDefinition test class.

#### Test Environment

- Standard xUnit test runner on the supported .NET runtimes, executed through the repository test project.
- No external services, files or UI; the formatting test switches the current culture temporarily and restores it.
- Test files: the PropertyDefinition test class in the Properties folder and the PropertyBag test class in the root of
  the test project.

#### Acceptance Criteria

- All PropertyDefinition tests pass.
- Every PropertyDefinition requirement is linked to at least one passing test.

#### Test Scenarios

##### Kind inference (`Plexus-Properties-PropertyDefinition-KindInference`)

- **PropertyDefinition_Constructor_InfersKindFromDefault**: boolean, int, long, double and string defaults infer their
  kind.
- **PropertyDefinition_Constructor_ColorAndSmallNumbers_NormalizeToCanonicalKinds**: color defaults infer the color
  kind; short and single-precision defaults widen to int and double.
- **PropertyDefinition_Constructor_WideNumericTypes_NormalizeToCanonicalKinds**: byte, unsigned and decimal defaults
  normalize to int, long and double.

##### Default validation (`Plexus-Properties-PropertyDefinition-DefaultValidation`)

- **PropertyDefinition_Constructor_UnsupportedDefault_ThrowsArgumentException**: unsupported types, NaN, null and a
  blank name are rejected.
- **PropertyDefinition_Constructor_OutOfRangeOrNonFiniteNumbers_ThrowArgumentException**: an oversized unsigned value,
  infinity, single-precision NaN and a character are rejected.

##### Choices (`Plexus-Properties-PropertyDefinition-Choices`)

- **PropertyDefinition_Choices_Initializer_MakesChoiceProperty**: a choice list creates a choice property with default
  and hints.
- **PropertyDefinition_Choices_NotSet_IsNull**: a plain string property reports no choices.
- **PropertyDefinition_Choices_SourceListChangedLater_DefinitionKeepsCopy**: later changes to the caller's list do not
  affect the definition.

##### Choice validation (`Plexus-Properties-PropertyDefinition-ChoiceValidation`)

- **PropertyDefinition_Choices_Invalid_Throws**: a default outside the list, an empty list, duplicates and a non-string
  default are rejected.

##### Presentation (`Plexus-Properties-PropertyDefinition-Presentation`)

- **PropertyDefinition_Presentation_Defaults_AreEmpty**: the display name defaults to the name; all other hints are
  empty.
- **PropertyDefinition_Presentation_ExplicitHints_AreStored**: explicit hints are stored unchanged.

##### Formatting (`Plexus-Properties-PropertyDefinition-Formatting`)

- **PropertyDefinition_FormatSummary_AppliesDisplayNameUnitAndFormat**: display name, unit, custom formatter and color
  text in a summary.
- **PropertyDefinition_FormatSummary_WithAndWithoutUnit_AppendsUnitOnlyWhenSet**: the unit is appended only when set.
- **PropertyDefinition_FormatValue_UsesInvariantCulture**: values format with the invariant culture even under a
  culture with a decimal comma; null formats as empty text.

##### Text editing (`Plexus-Properties-PropertyDefinition-TextEditing`, `PropertyEditingTests.cs`)

- **PropertyDefinition_TryParse_ValidText_ProducesTypedValues** and
  **PropertyDefinition_TryParse_TolerantForms_AreAccepted**: the exact CLR type of every kind, alternative boolean
  spellings, a color without hash, a trailing unit and a case-insensitive choice.
- **PropertyDefinition_TryParse_InvalidText_IsRefused**: a theory over syntax and range errors, checking the message.
- **PropertyDefinition_TryParse_Choice_ExactWinsAndAmbiguousIsRefused** and
  **PropertyDefinition_TryParse_IsCultureInvariant** (under a culture with a decimal comma).
- **PropertyDefinition_FormatForEdit_RoundTripsThroughTryParse**: every kind including awkward doubles and translucent
  colors.

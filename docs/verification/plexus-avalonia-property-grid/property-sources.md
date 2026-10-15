## PropertySources Unit Verification Design

Scope: Supplies property items: reflection over any object through the component model, host-supplied items, merging of
sources, source selection by factory and category and item ordering.

### Verification Approach

The sources are tested directly with small original test objects: an annotated `Person`, an `ICustomTypeDescriptor`
bag with run-time properties, an object extended by a `TypeDescriptionProvider`, change-notifying objects, and host
items built from delegates. The real component model is used; no mocks. The tests are in `PropertySourceTests.cs`.
The factory test registers its adapter for a type that only that test uses and removes the registration at the end,
because the registry is process-wide and tests run in parallel.

### Test Environment

The standard test runner. The type description provider test adds and removes a provider for a type only that test uses.

### Acceptance Criteria

A test passes when the source lists the expected items in the expected order, hides what is marked hidden, forwards or
raises change notifications exactly as documented, and resolves sources and orders items as specified, with null
arguments rejected.

### Test Scenarios

- **ReflectionPropertySource_GetItems_HidesNonBrowsableByDefault**: a hidden property is omitted by default and included,
  flagged, on request.
- **ReflectionPropertySource_CustomTypeDescriptor_ExposesDynamicProperties**: dynamic properties are listed with their
  category and description, are editable, and a refresh picks up a new one.
- **ReflectionPropertySource_TypeDescriptionProvider_AddsProperties**: a provider-added read-only property is listed.
- **ReflectionPropertySource_DefaultProperty_IsExposed**: the default property name is exposed, or null.
- **ReflectionPropertySource_EmptyOrNullTarget_IsHandled**: no properties gives no items; a null target is rejected
  (error path).
- **ReflectionPropertySource_NotifyPropertyChanged_RaisesChanged**: change notifications are forwarded with the property
  name or null, and stop after dispose.
- **ReflectionPropertySource_RefreshAllProperty_RebuildsItems**: a change of an observable property annotated
  `RefreshProperties.All` replaces the items and raises an all-items notification, and an explicit refresh of the source
  does the same.
- **ReflectionPropertySource_ShownRefreshAllPropertyChanged_RebuildsItems**
  (`PlexusAvaloniaPropertyGrid-PropertySources-HiddenRefreshAll`): the change notification of a shown refresh-all
  property replaces the items and reports a change of everything, while an ordinary property keeps the items and
  reports its own name.
- **ReflectionPropertySource_HiddenRefreshAllPropertyChanged_RebuildsItems**
  (`PlexusAvaloniaPropertyGrid-PropertySources-HiddenRefreshAll`): the change notification of a hidden (non-browsable)
  refresh-all property, which has no item, also replaces the items of a source that omits it and reports a change of
  everything.
- **CustomPropertySource_AddRemoveNotify_RaiseChanges**: add, remove, refresh and value notifications raise changes and
  hidden items are not listed.
- **CustomPropertyItem_Options_AreExposed**: metadata, default and reset, read-only state and argument validation
  (error paths).
- **CompositePropertySource_GetItems_MergesInOrderAndForwardsChanges**: order and forwarded changes.
- **CompositePropertySource_GetItems_DuplicateNameKeepsTheFirst**: the first item of a duplicate name is kept.
- **CompositePropertySource_AddRemoveDispose_UpdateItemsAndSubscriptions**: adding, removing and disposing, and a null
  source rejected (error path).
- **CompositePropertySource_ReflectionAndCustom_AreEditedThroughTheirOwnItems**: items of both kinds are edited through
  their own setters.
- **PropertySourceFactory_Create_ChoosesRegisteredProviderOrReflection**: reflection by default, the object's own
  source or provider, a registered adapter first, removal of a registration, and null arguments rejected (error
  paths).
- **PropertyOrdering_Order_CategorizedSortsByCategoryThenLabel**: category, then label, parenthesized names first.
- **PropertyOrdering_Order_AlphabeticalIgnoresCategories**: label order with parenthesized names first; null rejected
  (error path).
- **PropertyOrdering_Group_ProducesCategoryGroupsOrOneFlatGroup**: groups ignore case, a flat group has no category, empty
  input gives no groups.
- **PropertyOrdering_Group_OrdersCategoriesByRankThenAlphabetically**
  (`PlexusAvaloniaPropertyGrid-PropertySources-CategoryOrder`): a category with a lower
  `CategoryOrder` is first, a category ranks as its lowest item, equal ranks are alphabetical.
- **PropertyOrdering_Order_CategorizedSortsByItemOrderThenLabel** (`PlexusAvaloniaPropertyGrid-PropertySources-ItemOrder`):
  within a category a parenthesized item comes first whatever its rank, then the items by ascending `Order` (the
  default 0 before 1), equal ranks alphabetical.
- **PropertyOrdering_Order_AlphabeticalIgnoresItemOrder**
  (`PlexusAvaloniaPropertyGrid-PropertySources-ItemOrderAlphabetical`): the
  alphabetical view sorts by label even when the ranks say otherwise.
- **CompositePropertySource_RunCommit_ChainsTheSourceScopes** (`PlexusAvaloniaPropertyGrid-PropertySources-CommitScope`):
  the scopes of the sources that
  have one are chained, the first outermost, and the action runs once; a null action is rejected (error path). The test
  uses its own recording source in `PropertySourceTests.cs`.

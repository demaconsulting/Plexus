## PropertySources Unit Design

![PlexusAvaloniaPropertyGrid Structure](PlexusAvaloniaPropertyGridView.svg)

Scope: Supplies property items: reflection over any object through the component model, host-supplied items, merging of
sources, source selection by factory and category and item ordering.

### Purpose

An `IPropertySource` is the grid's only input. This unit provides the sources: `ReflectionPropertySource` for any
annotated object (the default), `CustomPropertySource` with `CustomPropertyItem` for hosts that supply items themselves,
and `CompositePropertySource` that merges several sources (for example a node name, a property bag and host
data). `PropertySourceFactory` chooses the source for an object and lets adapter layers register themselves.
`PropertyOrdering` orders and groups items for display.

### Data Model

- **IPropertySource**: `GetItems()` returning stable item instances until the source changes, and a `Changed` event with
  the changed property name or null for "everything".
- **ReflectionPropertySource**: the target, a flag for including non-browsable properties, the current item list and
  a subscription to the target's `INotifyPropertyChanged` when it has one.
- **CustomPropertySource**: a list of items supplied by the host. **CustomPropertyItem** holds the name, type, getter,
  optional setter and a `CustomPropertyOptions` record of metadata (display name, category, description, default,
  converter, editor key, flags).
- **CompositePropertySource**: the ordered list of sources and a subscription to each.
- **PropertySourceFactory**: an immutable array of registered adapter factories replaced under a lock, so lookups are
  lock-free.
- **PropertyOrdering**: stateless; **PropertySortMode** and **PropertyGroup** are the inputs and outputs.

### Key Methods

- **`ReflectionPropertySource.GetItems`** — items built from `TypeDescriptor.GetProperties(target)`, so
  `ICustomTypeDescriptor` and `TypeDescriptionProvider` add dynamic properties; non-browsable properties are omitted
  unless requested. `Refresh()` discovers the properties again and raises `Changed` for everything. A property change
  notification raises `Changed` for that property, and rebuilds the items when the property is annotated
  `RefreshProperties.All` or the notification names no property.
- **`CompositePropertySource.GetItems`** — concatenates the items of the sources in order; when a name occurs again the
  first item is kept (the name is the path, which must be unique). Forwards the `Changed` events of its sources.
- **`CustomPropertySource.Add` / `Remove` / `Refresh` / `NotifyValueChanged`** — change the list or report a value
  change.
- **`PropertySourceFactory.Create`** — the newest registered factory that returns a source wins, then the object itself
  when it is an `IPropertySource`, then `IPropertySourceProvider`, otherwise a `ReflectionPropertySource`.
- **`CompositePropertySource.RunCommit`** — chains the `IPropertyCommitScope.RunCommit` of the sources that have one (the
  first source outermost) around the writes and runs them once; **`IPropertyCommitScope`** is the interface a source
  implements to bracket the commits of its session (for example in an undo transaction).
- **`PropertyOrdering.Order`** — orders by category rank (the lowest `PropertyItem.CategoryOrder` of a category), then by
  category (categorized mode) and then by parenthesized names first, the item rank (`PropertyItem.Order`, lower first;
  categorized mode only, so the alphabetical view ignores it) and display
  name, comparing with the invariant culture and ignoring case; the sort is stable. Every item ranks 0 unless its source
  sets a rank (`CustomPropertyOptions.Order`, or `PropertyDefinition.Order` through the node source), so sources that set
  none keep the alphabetical order. **`Group`** splits the ordered items
  into category groups (a single group without a category in alphabetical mode).

### Error Handling

Null arguments raise `ArgumentNullException`. Reading values is the item's business (see the PropertyModel unit).
Change notification handlers of the host are called on the thread that raised the change.

### Dependencies

- The PropertyModel unit (`PropertyItem`, descriptor-backed items).
- .NET `System.ComponentModel`.

### Callers

The EditSession unit (items come from a source), host applications and the grid control.

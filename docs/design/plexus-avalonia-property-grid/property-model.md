## PropertyModel Unit Design

![PlexusAvaloniaPropertyGrid Structure](PlexusAvaloniaPropertyGridView.svg)

Scope: Describes one property row: metadata from annotations, value access, type-converter text conversion, standard
values, expandable children, collection elements and validation results.

### Purpose

`PropertyItem` is the view-model of one grid row, independent of any UI framework and of Plexus types. It turns the
annotations of a property into metadata, reads and writes the value, converts it to and from text with the property's
`TypeConverter`, lists standard values, exposes nested properties of expandable objects and the elements of
collections, and validates candidate values. It is abstract so that sources can supply items without reflection
(`CustomPropertyItem` is in the PropertySources unit); the descriptor-backed implementation `ReflectionPropertyItem`
belongs to this unit because nested items are built from it.

### Data Model

- **Item state**: an item holds its parent (for nested items), and caches its child items together with the value
  instance (reference types) or runtime type (value types) they were built for. Everything else is derived on demand
  from the descriptor or from the subclass, so an item is always consistent with the target.
- **Path**: the name for a top-level item, `Parent.Name` for a nested property and `Parent[index]` for a collection
  element. The path is the key of edits and errors in the edit session.
- **PropertyResult**: a success flag with an error message; all `Try...` members return it.
- **StandardValue**: a value with its display text.
- **Descriptor-backed item**: caches the default value, display, editor annotations and whether the owner declares a
  `ShouldSerialize<Name>` method. The object a descriptor is applied to comes from a delegate (the target for top-level
  items, the parent's current value for nested items), and a custom type descriptor may name another property owner.
- **Collection element item**: refers to the collection item and an index; it reads and writes the element through the
  collection's `IList` implementation.

### Key Methods

- **Metadata members** (`DisplayName`, `Category`, `CategoryOrder` (the rank of the category when sorting by
  category, default 0), `Description`, `IsReadOnly`, `IsBrowsable`, `IsPasswordText`,
  `IsParenthesized`, `RefreshMode`, `EditorKey`, `EditorType`, `Label`) — read the `System.ComponentModel`
  annotations (`DisplayName`, `Category` with the default "Misc", `Description`, `ReadOnly`, `Browsable`,
  `PasswordPropertyText`, `ParenthesizePropertyName`, `RefreshProperties`, `PropertyEditor`) and, when those are
  absent, the data annotations `Display` and `Editable`. The editor key comes from a `PropertyEditor` annotation of the
  property; when the property has none, from a `UIHint` data annotation (the hint for the `Avalonia` presentation layer
  preferred over a layer-less one, hints for other layers ignored), so that a model can name a custom editor without
  referencing the Avalonia assembly; and only then from a `PropertyEditor` annotation of the property type. A property
  without a setter is read-only; the children of a
  read-only value-type property are read-only.
- **`HasDefault`, `IsDefaultValueKnown`, `DefaultValue`, `IsChanged`, `CanReset`, `Reset`** — `HasDefault` is true for a
  `DefaultValue` annotation or a `ShouldSerialize<Name>` method; `IsChanged` asks the descriptor
  (`ShouldSerializeValue`) and so follows the WinForms rule; reset uses the descriptor (`ResetValue`), which honors the
  default value and the `Reset<Name>` method. An item without a default is never changed.
- **`GetValue`, `SetValue`, `TryGetValue`, `TrySetValue`, `TryReset`** — raw access may throw what the accessor throws;
  the `Try` forms check read-only state, validate, and capture exceptions. Writing a child of a value-type owner writes
  the modified copy back through the parent.
- **`Validate`** — a `null` is invalid for a non-nullable value type, the value must be assignable to the property type,
  and the `ValidationAttribute` annotations must accept it.
- **`ConvertToString`, `TryConvertToString`, `GetText`** — format with the converter and the culture (current culture
  when none is given); `null` is empty text; a collection whose converter has no text form is `(Collection)`.
- **`ConvertFromString`, `TryParse`, `TrySetText`, `SupportsTextEditing`** — parse with the converter and the culture;
  an object-typed property takes the text as is; a type whose converter cannot convert from text (and collections)
  report a `NotSupportedException` or a failed result.
- **`HasStandardValues`, `StandardValuesExclusive`, `GetStandardValues`** — expose the converter's standard values (enums,
  `bool`, custom lists that may depend on the owner instance) with their text.
- **`IsExpandable`, `Children`** — a converter that supports properties (such as `ExpandableObjectConverter`) makes the
  item expandable; the children are the browsable properties of the current value, using the converter of the value's
  runtime type for object-typed properties.
- **`IsCollection`, `CollectionItemType`, `GetCollectionValues`, `GetCollectionItems`** — arrays, `IList`,
  `ICollection<T>` and `IReadOnlyCollection<T>` types are collections; strings and dictionaries are not.

The converter receives an `ITypeDescriptorContext` whose `Instance` is the owner object and whose `PropertyDescriptor` is
the item's descriptor, so context-sensitive converters work as in the WinForms grid.

### Error Handling

User code is called only through guarded members: exceptions from getters, setters, converters, validation attributes
and reset methods become `PropertyResult` failures with the innermost (reflection-unwrapped) message. Out-of-memory
and similar process-level failures are not caught. The raw members (`GetValue`, `SetValue`, `ConvertFromString`) document
that they can throw.

### Dependencies

- .NET `System.ComponentModel` (`TypeDescriptor`, `TypeConverter`, annotations) and
  `System.ComponentModel.DataAnnotations`.
- `PropertyEditorAttribute` from the EditorRegistry unit (editor key and type).

### Callers

The PropertySources unit (sources create items), the EditSession unit (reads, validates and writes items) and the grid
control and its editors.

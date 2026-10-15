### PropertyDefinition Unit Design

![Properties Structure](PropertiesView.svg)

Scope: Property metadata: name, value kind, default, constraints and editor and display-order hints.

#### Purpose

PropertyDefinition describes one editable property of a node type and defines the portable value rules shared by the
whole Properties subsystem. The unit comprises three source files: the definition class, the value-kind enumeration and
an internal helper that normalizes, coerces and formats values.

#### Data Model

- **Name**: non-blank text, unique within a node type (uniqueness is enforced when the node type is registered).
- **ValueKind**: one of boolean, 32-bit integer, 64-bit integer, double, string, choice or color; inferred from the
  default value and changed to choice when a choice list is set.
- **DefaultValue**: the default in canonical form (bool, int, long, double, string or color).
- **Choices**: null, or an immutable copy of a non-empty list of distinct, non-null strings that contains the default
  value.
- **Presentation hints**: display name (defaults to the name), description, category, unit, summary flag, read-only
  flag, editor hint and an optional formatting function. All are set at construction and never restrict values.
- **Display order** (`Order`): an `int` hint, default 0; any value including negative ones and the extremes is accepted
  without validation. Contract for editors: in a categorized view a category lists its properties by ascending `Order`,
  equal values by display name (case-insensitive, culture-invariant); an alphabetical view ignores it. It never changes
  the order of the node type's property list, the property bag's enumeration, node summaries or saved documents. The
  definition only stores it; the Plexus property grid's node source maps it to the grid's item rank.
- **Canonical types**: small integral types widen to int, larger signed and unsigned types up to the long range widen
  to long, floating-point and decimal types become double. Other types, non-finite numbers and unsigned values above the
  long range are not allowed.
- **Invariant**: after construction the kind and default are consistent: a choice property is a string whose default is
  one of the choices.

#### Key Methods

- **Constructor(name, defaultValue)**: validates the name, normalizes the default to its canonical type and infers the
  kind. Preconditions: non-blank name, non-null supported default. Postcondition: kind and default are set.
- **Choices (initializer)**: requires a string property and a list without null entries (checked first), non-empty,
  without duplicates and containing the default (checked by coercing the default to the choice kind); copies the list
  and sets the kind to choice.
- **Normalize (helper)**: maps a value to its canonical type and kind; used by definitions and by ad-hoc assignment.
- **Coerce (helper)**: converts a value to the representation required by a declared kind: integers to int only when in
  range, integers to long, doubles and 32-bit integers to double, 64-bit integers to double only when the double
  converts back to exactly the same integer (below 2^63 and equal after the round trip), choices only when listed, every
  other kind only on an exact match. Anything else fails with a descriptive message.
- **FormatValue(value)**: applies the custom formatting function when present; otherwise formats with the invariant
  culture (empty text for null, the hexadecimal text for colors).
- **FormatSummary(value)**: returns the display name, a colon, the formatted value and, when a unit is set, a space and
  the unit.

`TryParse(text, out value, out error)`: culture-invariant conversion by `ValueKind`. Strings are returned as typed;
every other kind trims, strips a trailing `Unit` from numbers, and parses: booleans from `true/false/yes/no/on/off/1/0`
in any case, `int` and `long` with range detection (digits that do not fit are a range problem, not a syntax problem),
finite doubles with exponent notation, colors as `#RRGGBB` or `#AARRGGBB` (the hash optional) and choices by exact
match first and then by a unique case-insensitive match that returns the declared spelling. `FormatForEdit(value)`
(static; named so because `Format` is the display callback) produces the text that `TryParse` reads back to an equal
value: invariant culture, `true/false`, shortest round-trip doubles, `Color.ToString()`.

#### Error Handling

- A blank name, an unsupported default type, a non-finite number or an unsigned value above the long range raises an
  argument error; a null default raises an argument-null error.
- A choice list on a non-string property, a list with a null entry (message naming the property), an empty list,
  duplicates or a default not in the list raise an argument error from the initializer, so the declaration fails at
  the point where it is written.
- Coercion failures raise an argument error whose message names the value, the kind or the allowed choices; the caller
  (the property bag) lets it propagate unchanged.

#### Dependencies

- The Geometry subsystem's color type is one of the allowed value kinds and is formatted through its text form.
- The enumeration, the helper and the definition are internal to the Properties subsystem; there are no other
  dependencies. The summary-line API refers to the node type only in documentation.

#### Callers

- The Types subsystem (node type and its registry) stores definitions, looks them up by name, builds default summary
  lines and checks for duplicates.
- The PropertyBag unit reads the kind, default and choices of the declaration and uses the shared value rules.
- The Serialization subsystem uses the value kinds and rules when it writes and reads property values.
- Host applications create definitions when they declare node types, and editors read the presentation hints.

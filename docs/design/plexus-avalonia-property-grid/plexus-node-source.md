## PlexusNodeSource Unit Design

![PlexusAvaloniaPropertyGrid Structure](PlexusAvaloniaPropertyGridView.svg)

Scope: The Plexus adapter sources that show and edit a node, an annotation or a group in the property grid, with every
commit as one undo entry of the diagram.

### Purpose

A diagram editor shows the selected element in a properties dialog or a docked panel. `NodePropertySource` lists a node's
name, its declared properties (from their `PropertyDefinition`) and its host data; `AnnotationPropertySource` and
`GroupPropertySource` list the few properties of those elements. The package registers the sources with
`PropertySourceFactory` when it loads, so `grid.SelectedObject = element` needs no setup, and every commit is one undo
entry no matter which mode the grid is in or which host shows it.

### Data Model

- **Rows (`NodePropertySource`)**: the name item (category General with rank -1, parenthesized so it is first), one item
  per declared property and one item per browsable property of the host data. Row paths are collision-free: `Node.Name`
  for the name row, `Property.<name>` for declared properties and `Data.<path>` for host data (nested objects and
  collection elements extend the data path), built with `NamePath`, `PropertyPath` and `DataPath`; items keep their bare
  `Name`, and the path prefix is applied through the internal `PropertyItem.RootPath`. A data property can therefore not
  hide a declared one. Declared properties use the category of the
  definition (General when it has none), the definition's `Order` as the item rank (so a category lists them in
  declared display order, ties alphabetical), the display name with the unit in parentheses, the description,
  read-only, a
  default (so a changed value is bold), a text converter built on `PropertyDefinition.TryParse` and
  `FormatForEdit` for numbers and choices (choices are an exclusive list), booleans as `bool`, colors as
  `Avalonia.Media.Color` and the editor hint as the editor key. Data rows are descriptor-backed items over a working copy.
- **Working state**: a `WorkingValues` dictionary of the values the grid wrote during the running commit (keyed by row
  path), and a
  working copy of the host data made on demand with `Node.CloneData` and thrown away after every commit and every
  outside change. An `ElementPropertyItem` reads the element until a value was written, then the written value.
- **`ElementEditing`**: the commit plumbing shared by the three sources: the element, its diagram, the publish and discard
  callbacks, the names of the element properties it watches and a running flag.
- **Annotation and group items**: text and fill (an optional color, bold when set) of an annotation; title and collapsed
  state of a group.

### Key Methods

- **`RunCommit(apply)`** (the `IPropertyCommitScope` of the three sources) — clears the working state, opens
  `UndoHistory.Transact`, lets the session write (`apply`, which writes into the working state), then publishes: the name
  through `Diagram.TryRename` (a refusal throws `PropertyCommitRefusedException` for the name row), the changed properties
  with `PropertyBag.TrySetAll`, and the working copy of the data as `Node.Data` when its serialized form differs from the
  live data. A refusal rolls the transaction back (nothing changes: the commit is atomic) and the refusal names the row
  (`Node.Name` or `Property.<name>`), which the session lists in `CommitResult.Failed`. Afterwards the working state is
  cleared and `Changed`
  is raised so the grid reads again.
- **Name validation** — while the name is typed: not empty, and unique across the diagram when
  `Diagram.RequireUniqueNodeNames` is set (the same messages as the rename pipeline); a host veto is only known on commit.
- **Outside changes** — the source listens to `PropertyChanged` of the element (name, properties, data; text and fill;
  title and collapsed state); a change that is not its own commit clears the working state, rebuilds the data rows
  when the type of the data changed and raises `Changed`.
- **`PlexusSourceRegistration.Create`** — maps a `Node`, `Annotation` or `Group` to its source; registered by a module
  initializer.

### Error Handling

A node that is not attached to a diagram raises `ArgumentException` on construction; null raises
`ArgumentNullException`. Invalid values are refused as results of the session; a refused commit is reported as the commit
error and on the refused row. Data that cannot be copied (the node type declares no data type or serializer)
simply has no rows.

### Dependencies

- The PropertySources unit (`IPropertySource`, `IPropertyCommitScope`, `PropertySourceFactory`, `CustomPropertyOptions`),
  the PropertyModel unit (items, descriptor-backed items) and the EditSession unit (commit scope).
- The Plexus core (`Node`, `PropertyBag`, `PropertyDefinition`, `Diagram.TryRename`, `UndoHistory.Transact`,
  `Node.CloneData`, `Annotation`, `Group`).

### Callers

The NodePropertiesDialog unit, `PropertyGrid` (through `SelectedObject`) and host applications.

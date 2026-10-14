### Annotation Unit Design

![Model Structure](ModelView.svg)

Scope: Sticky-note and text-box element with resizable bounds and optional fill.

#### Purpose

The Annotation unit models a free-floating text note on a page: a resizable box with a text and an optional fill
color. It is not connected to anything, can be a member of a group, and is created through the page API (see DiagramPage
Design).

#### Data Model

- `Text`: the note text, never null (an empty string is allowed).
- `Bounds`: the bounding box; `GetBounds()` returns the same box for the element base class contract.
- `Fill`: an optional color; null selects the theme's annotation fill at draw time.
- Inherited from `DiagramElement` (see Node Design): `Id`, `Page`, `Parent`, `IsVisible`, `PropertyChanged`.

#### Key Methods

- `Text` setter: precondition: the value is not null and the annotation is attached. An equal text is ignored;
  otherwise an undoable value command "Edit annotation text" stores the text and raises the change notification.
- `Fill` setter: same pattern with the command "Change annotation fill"; null clears the override.
- `Bounds` setter: delegates to the page layout pipeline, which validates the box, ignores an unchanged box, names the
  history entry "Move" when only the position changes and "Resize" otherwise, and writes the box through
  `SetBoundsCore`, which raises the notification. Moving annotations together with other elements uses the same
  pipeline.

#### Error Handling

- A null text throws `ArgumentNullException`; non-finite or negative bounds throw `ArgumentException` from the page.
- Any setter on a removed annotation throws `InvalidOperationException`.
- Exceptions are propagated; a rejected call leaves the annotation unchanged.

#### Dependencies

- DiagramPage and the undo history (through the diagram) for every change; Geometry (`Rect`, `Color`); the base class
  from the Node unit.

#### Callers

DiagramPage creates, moves and removes annotations; Group measures them as members; the Scene builder draws them; hit testing,
serialization and the Avalonia editor read and edit them through the public members.

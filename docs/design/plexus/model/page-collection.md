### PageCollection Unit Design

![Model Structure](ModelView.svg)

Scope: Ordered, observable collection of pages with undoable add, insert, remove and move.

#### Purpose

PageCollection is the ordered list of the pages of a diagram. It creates, inserts, removes and reorders pages as undoable
operations, publishes collection-changed notifications that UI item controls can bind to directly, and attaches and
detaches page content so that the diagram's element index only contains elements of pages that are in the list. It also
presents a read-only non-generic list view so that frameworks which bind to the non-generic list interface cannot bypass
the undoable operations. The unit is the single source file `PageCollection.cs`.

#### Data Model

- **Pages**: an observable list of pages in display order; its collection-changed event is re-raised with the page
  collection as the sender, so consumers see a stable event source.
- **Owner**: a reference to the diagram, used to reach the undo history and to register elements and raise `Changed`.
- **Page state**: each page has an attached flag, set while the page is in the list. A removed page keeps its content
  and its identity so that undo can restore the very same object.
- **Interfaces**: the generic read-only list of pages, the non-generic list (read-only and fixed size; every mutator
  throws), and the collection-changed notification interface.

#### Key Methods

- **Add(name) / Insert(index, name)**: validate the name (not blank) and the index (0 to Count), create an empty page and
  execute an undoable entry "Add page" whose do action inserts the page and whose undo action removes it. Return the new
  page. Redo re-inserts the same page object.
- **Remove(page)**: returns false when the page is not in the list; otherwise executes an undoable entry "Remove page"
  that remembers the old index; undo re-inserts the same page at that index.
- **Move(page, newIndex)**: validates that the page is in the list and the index is within the list, does nothing when the
  position is unchanged, and otherwise executes an undoable entry "Move page" with the old index as its inverse.
- **IndexOf / Contains / enumeration / indexer**: read-only access.
- **InsertCore (private)**: marks the page attached, registers its elements in the element index, inserts it and raises
  the `PageAdded` change. **RemoveCore (private)**: unregisters the elements, marks the page detached, removes it and
  raises `PageRemoved`. **MoveCore (private)**: moves the page and raises `PageMoved`. Detaching and attaching complete
  before any notification, so observers never see a half-attached page.
- **Non-generic list mutators**: Add, Insert, Remove, RemoveAt, Clear and the indexer setter throw a not-supported
  exception that points to the undoable API.

#### Error Handling

A blank name throws an argument exception, an out-of-range index throws an argument-out-of-range exception, a page that
is not in the collection throws an argument exception from `Move` (and yields false from `Remove`), and a null page
throws an argument-null exception. The commands cannot fail when run by the history because they only manipulate the
in-memory list, so notifications are always raised for both the original operation and undo and redo. The list change
itself (which raises `CollectionChanged`) and the diagram's `Changed` run through the history's notification gate: a
handler that throws does not interrupt the change, the page is attached, detached or moved and the change recorded
first, and the first handler exception is then rethrown to the caller (see Diagram Design). Not thread-safe.

#### Dependencies

- Diagram: owner; provides the undo history, the element index registration and the `Changed` event.
- DiagramPage: the element type of the list; its attached flag and element lists are read and written here.
- History: executes the undoable entries.

#### Callers

The diagram exposes the collection as its `Pages` property; host applications and the editor call its operations, and UI
controls bind to its notifications and list view. The Snapshot unit and the scene builder enumerate it.

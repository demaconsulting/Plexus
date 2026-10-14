## History Subsystem Design

![History Structure](HistoryView.svg)

Scope: Command-based undo/redo history in which every operation is undoable.

### Overview

The History subsystem makes every change to a diagram reversible. A change is expressed as a command with an apply
action and an exact inverse; the history applies the command, remembers it on an undo stack, and later replays or
reverts it on request. One history instance belongs to one diagram, and all mutating operations of the model, such as
adding or moving elements and editing properties, run through it, so hosts get undo and redo without writing any
reversal logic. Hosts can also record their own changes with delegate-based commands.

The subsystem contains one software unit, UndoHistory, which also comprises the command types used with it (see
UndoHistory Design). It is single-threaded and has no dependencies on other Plexus subsystems; the diagram owns an
instance and the other subsystems are its callers.

### Interfaces

- **UndoHistory**: the public entry point. It exposes execution of commands, undo and redo, the availability and
  descriptions of the next undo and redo entries, transactions, suspension, the dirty flag with its clean marker, the
  capacity limit and a change event.
- **IUndoableCommand**: the public contract of one reversible change (a description, an apply action and an inverse).
- **IMergeableCommand**: an optional extension that lets a command absorb the next command inside a transaction.
- **HistoryTransaction**: the handle returned when a transaction begins; disposing commits it, and it can be rolled back.

### Design

- **Two stacks and a save point.** Entries live on an undo stack and a redo stack. The dirty flag is not a boolean
  that is toggled; it compares the current undo depth with the depth at which the document was last marked clean.
  This makes "undo back to the saved state is clean" come out naturally, and the save point becomes unreachable (and
  the history permanently dirty) when new edits replace the redo branch or the capacity limit drops the entry.
- **One entry per gesture.** Transactions collect commands in a list and push a single composite entry when the
  outermost transaction ends. Nested transactions flatten, so operations can be composed freely without deciding who
  owns the undo step. Rollback reverts only the commands recorded since the transaction began, which lets an inner step
  fail without destroying the outer work.
- **Apply first, record second.** A command is applied before it is recorded, so a command that fails is never
  recorded; an undo that fails leaves its entry on the undo stack. Re-entrant recording during an undo or redo is
  refused so that command code cannot corrupt the stacks.
- **Suspension for loading.** Loading a document changes the model without commands, which makes recorded entries
  stale. Suspending applies changes without recording; when the outermost scope ends the stacks are dropped and the
  dirty state of the moment the suspension began is restored, so loading into a clean diagram leaves it clean.
- **Bounded memory.** The undo stack is trimmed from the oldest end to the capacity limit (1000 by default), and the
  save point shifts with it.
- **Single notification.** One change event covers every change of the stacks or the dirty flag, which is all that
  undo and redo buttons and a modified indicator need.

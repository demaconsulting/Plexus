### UndoHistory Unit Design

![History Structure](HistoryView.svg)

Scope: Per-diagram undo and redo stacks, transactions, mergeable commands and dirty tracking.

#### Purpose

UndoHistory is the single place where changes to a diagram are applied, recorded, reverted and re-applied. It owns the
undo and redo stacks, groups commands into transactions, suspends recording while documents load, tracks whether the
document differs from its saved state, bounds the number of remembered entries and announces every change of this
state. The unit comprises the sources in the History folder: the history itself, the transaction handle, the command
contract and its mergeable extension, and the internal command types (delegate, composite, set-value and the command
plan used by multi-step model operations).

#### Data Model

- **Undo and redo stacks**: lists of commands; the last element is the newest undo entry or the next redo entry.
- **Open transaction state**: the list of commands collected by the outermost open transaction, the start index of
  each open (nested) transaction within it, and the description of the outermost transaction.
- **Suspension state**: a counter of active suspension scopes and the dirty state captured when the outermost scope
  began.
- **Save point**: the undo depth at which the document was last marked clean, or null when that state is no longer
  reachable. The history is dirty exactly when the save point differs from the current undo depth.
- **Capacity**: maximum number of undo entries, at least one, 1000 by default.
- **Applying flag**: true while an undo or redo runs, used to refuse re-entrant recording.
- **Invariants**: the redo stack is empty after any newly pushed entry; commands collected by an open transaction are
  not on the undo stack until the outermost transaction completes; a save point deeper than the undo depth refers to
  entries on the redo stack and becomes null as soon as a new entry replaces them.

Command types: a command has a description, an apply action and an inverse that restores the exact prior state. A
mergeable command may absorb a following, already applied command. The composite command replays its children in order
and reverts them in reverse order; the delegate command wraps two actions; the set-value command stores a target, a key,
the old and the new value, and merges with a following set of the same target and key, keeping the first old value and
the last new value. The command plan applies the steps of a multi-step model operation one by one, can revert them all
if a later step fails, and builds one composite entry; its `Count` (the number of applied steps) lets a caller record a
single step as itself instead of a composite, so a mergeable command stays mergeable (used when a property change
recomputes no ports).

#### Key Methods

- **Execute(command)**: applies the command, then records it. Precondition: no undo or redo is running. Postcondition:
  the command is on the undo stack (or joined to the open transaction, or dropped when suspended) and, outside a
  transaction, the redo stack is empty. A command that throws while being applied is not recorded.
- **Execute(description, doAction, undoAction)**: wraps the delegates in a delegate command and executes it.
- **Record(command)** (internal): records an already applied command with the same rules; used by the model for
  operations that apply each step while planning the next.
- **Undo / Redo**: revert the newest undo entry or re-apply the newest redo entry and move it to the other stack.
  Preconditions: no transaction open, no undo or redo running. They return false when the stack is empty. The entry is
  moved only after the action succeeded.
- **BeginTransaction(description)**: opens a transaction; the outermost one creates the collection list and fixes the
  description. The returned handle commits on dispose or reverts the commands recorded since it began on rollback.
  Completion must happen innermost first.
- **EndTransaction** (internal): on rollback reverts the transaction's commands newest first and discards them; on
  finishing the outermost transaction pushes one composite entry, or nothing when no command was recorded.
- **Record inside a transaction**: the new command is offered to the previous command of the innermost transaction
  (never to commands before the transaction's start index) and absorbed when that command accepts it; otherwise it is
  appended. Outside a transaction commands are pushed individually and never merged.
- **Push (private)**: clears the redo stack, invalidates a save point beyond the new depth, appends the entry, trims to
  the capacity and raises the change event.
- **Suspend**: returns a scope; while any scope is active, recorded commands are applied but dropped and the save point
  becomes unreachable. When the outermost scope is disposed the stacks are cleared, the save point becomes zero (clean)
  or unreachable according to the dirty state captured at the start, and the change event is raised. Disposing a scope
  twice has no further effect.
- **MarkClean / Clear**: set the save point to the current depth, respectively empty both stacks and set the save point
  to zero; Clear is refused while a transaction is open. Both raise the change event.
- **Capacity (set)**: validates the value (at least one), then trims the oldest entries and shifts the save point by the
  number of dropped entries, losing it when the dropped entry was at the base; raises the change event only when
  something was dropped.

`Transact(description, action)` and `Transact<T>(description, function)`: open a transaction, run the delegate, and let
disposal commit; when the delegate throws, call `Rollback` on the transaction (which reverts the commands recorded since
it began, newest first) and rethrow the original exception unchanged. The action overload delegates to the function
overload. Nested calls flatten like nested transactions.

#### Error Handling

- Null commands, descriptions and delegates raise an argument-null error before any state change.
- A capacity below one raises an argument-out-of-range error.
- Undo, redo and clear while a transaction is open, recording while an undo or redo is running, a rollback of an
  already completed transaction and completion of transactions out of order raise invalid-operation errors. A rejected
  out-of-order completion leaves the transaction open so it can be completed correctly.
- Exceptions thrown by command code propagate to the caller. Because the application happens before recording, a
  failing execute records nothing; a failing undo leaves its entry on the undo stack and clears the applying flag.
- Thread safety is not provided; all calls must come from the thread that owns the diagram.

#### Dependencies

None on other software units. The unit uses only the .NET base class library. Its public command contract is
implemented by the model's own command classes in the Model subsystem and by host code.

#### Callers

- The Model subsystem creates one history per diagram (exposed as its history member) and executes or records every
  mutation through it, including page, element, group, wire and rename operations; the Properties subsystem records
  property changes through it.
- The Serialization subsystem suspends the history while a snapshot is loaded and then clears it.
- Host applications use undo, redo, transactions, dirty tracking, capacity and the change event, and may record their
  own commands.

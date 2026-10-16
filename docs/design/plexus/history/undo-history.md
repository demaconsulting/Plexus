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
- **Applying flag**: true while an undo, redo or rollback runs, used to refuse re-entrant changes of the history.
- **Operation state**: the nesting depth of running change operations (execute, model operation, undo, redo,
  rollback) and the first change-handler exception captured during the outermost one, if any.
- **Pending suspension end**: set when the outermost suspension scope ends while the applying flag is set; its effect
  runs right after the replay.
- **Invariants**: the redo stack is empty after any newly pushed entry; commands collected by an open transaction are
  not on the undo stack until the outermost transaction completes; a save point deeper than the undo depth refers to
  entries on the redo stack and becomes null as soon as a new entry replaces them.

Command types: a command has a description, an apply action and an inverse that restores the exact prior state; both
must be atomic (complete, or throw without changing anything). A mergeable command may absorb a following, already
applied command. The composite command replays its children in order and reverts them in reverse order; when a child
fails it compensates the children it already replayed in that call (a failing apply reverts them newest first, a
failing revert re-applies them in their original order) and rethrows the child's exception. The delegate command wraps
two actions; the set-value command stores a target, a key, the old and the new value, and merges with a following set
of the same target and key, keeping the first old value and the last new value. The command plan applies the steps of
a multi-step model operation one by one, can revert them all if a later step fails (`Abort(cause)`), and builds one
composite entry; its `Count` (the number of applied steps) lets a caller record a single step as itself instead of a
composite, so a mergeable command stays mergeable (used when a property change recomputes no ports). `AddFirst` records
a marker step whose apply action does nothing at the start of the plan without running it (used by the model for the
marker that reports a port recomputation on undo).

#### Key Methods

- **Execute(command)**: applies the command as one change operation, then records it. Precondition: no undo or redo is
  running. Postcondition: the command is on the undo stack (or joined to the open transaction, or dropped when
  suspended) and, outside a transaction, the redo stack is empty. A command that throws while being applied is not
  recorded (handler exceptions captured meanwhile are dropped). When it succeeded, the first captured handler exception
  is rethrown after recording.
- **Execute(description, doAction, undoAction)**: wraps the delegates in a delegate command and executes it.
- **Record(command)** (internal): records an already applied command with the same rules; used by the model for
  operations that apply each step while planning the next.
- **Undo / Redo**: revert the newest undo entry or re-apply the newest redo entry, as one change operation, and move it
  to the other stack. Preconditions: no transaction open, no undo or redo running. They return false when the stack is
  empty. The entry is moved only after the action succeeded; then a deferred suspension end is applied, the change
  event is raised and the first captured handler exception is rethrown. When the action fails, captured handler
  exceptions are dropped, a deferred suspension end is applied and the failure propagates with the entry still on its
  stack.
- **Notify(state, raise)** (internal): raises one change notification of the model. Outside an operation it simply
  raises it; inside one it catches any exception except running out of memory, keeps the first one, and returns so the
  change continues.
- **BeginOperation / EndOperation / AbortOperation** (internal): bracket a change operation. `BeginOperation` increases
  the depth and returns the failure captured so far; `EndOperation` decreases it and, for the outermost operation,
  returns the first captured handler exception for the caller to rethrow after recording; `AbortOperation` decreases it
  and drops the handler exceptions captured during the failed operation. The model uses them around multi-step
  operations (`Diagram.Perform`, a port-recomputing property change).
- **BeginTransaction(description)**: opens a transaction; the outermost one creates the collection list and fixes the
  description. The returned handle commits on dispose or reverts the commands recorded since it began on rollback.
  Completion must happen innermost first.
- **EndTransaction** (internal): on rollback reverts the transaction's commands newest first, as one change operation
  run under the replay guard (`RunGuarded`, which restores the previous guard state in a `finally`), attempting every
  inverse even when one throws, and discards all of them; on finishing the outermost transaction pushes one composite
  entry, or nothing when no command was recorded. It returns the exception the handle rethrows after it marked itself
  complete: the inverse failures when there are any (a lone failure of an explicit rollback as itself, otherwise an
  `AggregateException`, with the causing failure of a `Transact` first), else the first handler exception of a
  rollback; a rollback caused by a failure (`Transact`) drops handler exceptions instead. The transaction is complete
  in every case, so a later disposal commits nothing.
- **Record inside a transaction**: the new command is offered to the previous command of the innermost transaction
  (never to commands before the transaction's start index) and absorbed when that command accepts it; otherwise it is
  appended. Outside a transaction commands are pushed individually and never merged.
- **Push (private)**: clears the redo stack, invalidates a save point beyond the new depth, appends the entry, trims to
  the capacity and raises the change event.
- **Suspend**: returns a scope; while any scope is active, recorded commands are applied but dropped and the save point
  becomes unreachable. A transaction begun while recording is suspended still keeps its commands, only so that a
  failure or an explicit rollback can revert them; its commit discards them instead of pushing an entry. When the
  outermost scope is disposed the stacks are cleared, the save point becomes zero (clean)
  or unreachable according to the dirty state captured at the start, and the change event is raised; when that happens
  during an undo, redo or rollback, this effect is deferred until the replay has moved its entry. Disposing a scope
  twice has no further effect. Starting a suspension during an undo, redo or rollback, or while a transaction is open
  (the transaction's commit would record entries after the stacks were cleared), is refused.
- **MarkClean / Clear**: set the save point to the current depth, respectively empty both stacks and set the save point
  to zero; Clear is refused while a transaction is open, and both are refused during an undo, redo or rollback. Both
  raise the change event.
- **Capacity (set)**: validates the value (at least one), is refused during an undo, redo or rollback, then trims the
  oldest entries and shifts the save point by the number of dropped entries, losing it when the dropped entry was at
  the base; raises the change event only when something was dropped.

`Transact(description, action)` and `Transact<T>(description, function)`: open a transaction, run the delegate, and let
disposal commit; when the delegate throws, roll the transaction back (reverting the commands recorded since it began,
newest first, and dropping change-handler exceptions raised while reverting) and rethrow the original exception
unchanged. A change-handler exception rethrown by one of the delegate's edits is such a failure, so it rolls the whole
transaction back. The action overload delegates to the function overload. Nested calls flatten like nested
transactions.

#### Error Handling

- Null commands, descriptions and delegates raise an argument-null error before any state change.
- A capacity below one raises an argument-out-of-range error.
- Undo, redo and clear while a transaction is open; recording, beginning a transaction, clearing, marking clean,
  setting the capacity or suspending while an undo, redo or rollback is running; suspending while a transaction is
  open; a rollback of an already completed
  transaction; and completion of transactions out of order raise invalid-operation errors and change nothing. A
  rejected out-of-order completion leaves the transaction open so it can be completed correctly.
- Exceptions thrown by command code propagate to the caller. Because the application happens before recording, a
  failing execute records nothing; a failing undo or redo leaves its entry on its stack and clears the applying flag.
  A failing child of a grouped entry is compensated first; a failing step of a multi-step operation makes the plan
  revert every applied step. When a compensating or reverting step also fails, every remaining one is still attempted
  and an `AggregateException` is thrown whose inner exceptions are the original failure followed by every compensation
  failure (the same shape for `CommandPlan.Abort` and grouped replay). `CommandPlan.Abort` takes the history and runs
  its reverts under the same replay guard as an undo, so they cannot change the history. A transaction rollback also
  attempts every inverse; it reports the inverse failures (with the original failure first when `Transact` caused it).
- Exceptions thrown by change handlers during an operation (raised through `Notify`) never interrupt it: the change is
  completed and recorded (or moved), then the first one is rethrown with its original stack trace; later ones are
  dropped, as are all of them when the operation itself fails or when a rollback is caused by a failure. Handlers of the
  history's own change event are not covered: the stacks are consistent when it is raised.
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

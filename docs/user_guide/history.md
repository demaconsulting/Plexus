# Undo, Redo and Dirty Tracking

Every mutation made through the model is recorded in `diagram.History`, one entry per call. There is one history per
diagram, shared by all its pages, and it is not stored in clipboard or snapshot data.

## Undo and Redo

```csharp
diagram.History.Undo();                     // returns false when there is nothing to undo
diagram.History.Redo();
bool canUndo = diagram.History.CanUndo;     // enable menu items from CanUndo and CanRedo
string? text = diagram.History.UndoDescription;    // "Move", "Add node": text for an "Undo ..." menu item
```

The history raises `Changed` whenever the stacks or the dirty flag change, which is the single event a host needs to
keep its Edit menu, toolbar and window title current:

```csharp
diagram.History.Changed += (_, _) =>
    ShowMessage($"Undo {diagram.History.UndoDescription}, redo {diagram.History.RedoDescription}");
```

The demo's Edit menu shows *Undo Move* or *Redo Delete* this way. In the Avalonia editor, Ctrl+Z and Ctrl+Y use the
same history; `editor.Undo()` and `editor.Redo()` additionally cancel a gesture or an inline edit that is in progress
before touching the history (see *The Avalonia Editor*).

## Transactions

Group several changes into one undo step by wrapping them in a transaction. Disposing the transaction commits it.

```csharp
using (diagram.History.BeginTransaction("Build adder"))
{
    var x = page.AddNode("math.Constant", 0, 450);
    var y = page.AddNode("math.Add", 200, 450);
    x.Output("value").ConnectTo(y.Input("a"));
}
```

`Transact` is the safe shape of this pattern. It runs the delegate as one undo entry, and when the delegate throws it
reverts everything the delegate did and rethrows, so a refused edit never leaves half of its changes in the history. This
is the pattern for a dialog that applies several edits at once and must leave the model untouched when one of them is
refused:

```csharp
diagram.History.Transact("Edit properties", () =>
{
    sum.Name = "Total";                      // refused (an ArgumentException)? the next line is not applied
    a.Properties["Value"] = 4.0;             // and the rename above is undone before the exception reaches you
});

int added = diagram.History.Transact("Add pair", () =>
{
    page.AddNode("math.Constant", 0, 0);
    page.AddNode("math.Add", 200, 0);
    return 2;
});
```

`Transact<T>` returns what the delegate returns. For property edits you do not need to write the transaction yourself:
`node.Properties.TrySetAll(...)` validates, applies all or nothing and records one entry (see *The Avalonia Editor*).
The long form, `BeginTransaction` with `transaction.Rollback()`, remains for code that must decide to abandon the edit
without an exception.

Merging of consecutive small commands (a drag produces many moves but one entry) is done by commands implementing
`IMergeableCommand`; hosts rarely need it.

## Loading Content Without History

Content that the user did not create does not belong in the undo stack: building a sample, importing a file, adding
the first page of a new document. Suspend recording while populating:

```csharp
using (diagram.History.Suspend())
{
    diagram.Pages.Add("Imported");      // applied but not recorded
}
```

A diagram that was only populated inside a suspension starts clean: ending the suspension restores the clean state
that the new diagram had. The demo builds every sample and every new document this way.

## Dirty Tracking

`IsDirty` is true when the document changed since `MarkClean()` (or since creation). Call `MarkClean()` after the host
saved the document; undoing back to the saved state makes the document clean again.

```csharp
bool unsaved = diagram.History.IsDirty;
diagram.History.MarkClean();              // after saving
```

The demo shows `Modified *` or `Saved` in its status bar and an asterisk in the window title from `IsDirty`, and asks
Save, Discard or Cancel on New, Open and closing the window while the document is dirty.

## Host-Defined Undoable Operations

State that lives in the host (a document setting, a simulation parameter) can take part in the same history. Give the
operation as a description and a pair of delegates; Plexus records it like any other entry:

```csharp
int gridSetting = 15;
diagram.History.Execute("Change grid", () => gridSetting = 20, () => gridSetting = 15);
```

The two delegates must be exact inverses of each other, and repeatable. For richer commands implement
`IUndoableCommand` (`Description`, `Do` and `Undo`) and pass it to `Execute`. Each of `Do` and `Undo` must either
complete or throw without having changed anything: when one command of a transaction fails during undo or redo, the
history restores the other commands of that entry, which stays on its stack so the user can try again. If restoring
fails as well, the exception is an `AggregateException` holding the original failure first and then every restore
failure.

## Limits

The history has a bounded capacity (`History.Capacity`; the oldest entries are dropped first), `Clear()` empties it and
marks the document clean, and `UndoCount` and `RedoCount` report the depth. Changing the theme, the router or the
connection rules is configuration and is not undoable; assigning a styler is not undoable either.

Code that runs while an entry is undone or redone (a command's `Do` or `Undo`, and the change handlers it triggers)
cannot change the history: `Execute`, `BeginTransaction`, `Transact`, `Clear`, `MarkClean`, setting `Capacity` and
`Suspend` throw `InvalidOperationException` there and change nothing. Disposing a `Suspend` scope there is allowed; it
takes effect right after the undo or redo has finished.

# Plexus Puppet

`plexus-puppet` is a stateless driver for the Plexus demo application (`plexus-demo`). It lets an AI coding agent or a
test script drive the **real GUI** one action at a time: UI Automation (FlaUI UIA3) for menus, dialogs, keyboard and
screenshots; the demo's named-pipe automation endpoint for model queries and geometry; and **real operating-system
mouse input** for canvas gestures (drag, connect, rubber band, wheel, palette drag and drop).

It exists to test the Plexus editor control end to end: hit testing, gestures, inline editing, undo/redo and the OS
clipboard are exercised exactly as a user would, which the headless unit tests cannot do. It is **not shipped**.

## Warning

The Puppet drives the real desktop. It moves the real mouse cursor and sends real key presses. Do not use the keyboard
or mouse while a Puppet run is active, and never run it on a locked or remote-disconnected session (see
[Safety checks](#safety-checks)).

Each command is a fresh process: it finds the `plexus-demo` instance by its alias, does one thing, prints plain text and
exits with a documented code. All state lives in the demo process.

## Build

```powershell
dotnet build Plexus.slnx -c Release
```

The Puppet is `tools\DemaConsulting.Plexus.Puppet\bin\Release\net10.0-windows\plexus-puppet.exe`. `launch` finds the newest
`plexus-demo.exe` under `demo\DemaConsulting.Plexus.Demo.Gui\bin\{Release,Debug}\net10.0` of the repository (or beside the
Puppet); pass `--exe <path>` for any other build.

## Instances

Every command takes `--instance <alias>` (default `main`). An alias names the demo's automation pipe
`\\.\pipe\plexus-demo-<alias>`, so any number of instances can run side by side. Aliases use letters, digits, `-` and `_`.

```text
plexus-puppet launch [--instance <alias>] [--sample <name> | --open <file>] [--types full|basic] [--exe <path>] [--allow-existing]
plexus-puppet instances
plexus-puppet quit [--instance <alias>] [--kill]
```

- `launch` starts `plexus-demo --automation <alias>` (passing `--sample`/`--open`/`--types` through), waits until the pipe
  answers `ping` and the window is visible, then prints `OK instance=<alias> pid=<pid> ...`. It refuses (exit 4) when the
  alias already answers, unless `--allow-existing`. A launch that never becomes ready is killed.
- `instances` lists the live aliases by probing the pipes: `INSTANCE alias=a pid=... title=... file=... dirty=... types=...`.
- `quit` asks the instance to exit through the pipe (no unsaved-changes prompt) and waits for the process to end; `--kill`
  ends it if it does not.

## UI Automation commands

```text
plexus-puppet window
plexus-puppet tree [--depth <N>] [--under <automationId>]
plexus-puppet find <automationId>
plexus-puppet click <automationId>
plexus-puppet type <automationId> "<text>"
plexus-puppet choose <comboAutomationId> "<item text>"
plexus-puppet key <chord> [--to <automationId>]
plexus-puppet read <automationId>
plexus-puppet menu <Menu>/<Item>[/<Sub>]
plexus-puppet dialogs
plexus-puppet shot <path.png> [--screen]
plexus-puppet place [--instance <alias>] --x <px> --y <px> --width <px> --height <px>
plexus-puppet place [--instance <alias>] --work-area [--left-gap <px>] [--margin <px>]
```

- Elements are found by AutomationId (see the demo README for the scheme) in the main window, dialogs and pop-ups. A miss
  prints the closest ids ("Did you mean ...").
- `click` invokes through UI Automation patterns (invoke, select, toggle), so it works on menus, buttons and check boxes
  without moving the cursor. `type` sets the value through the Value pattern (falling back to typing);
  `choose` expands a combo box and selects an item by its text.
- `key` sends **real** key presses to the focused element of the instance (or to `--to <id>` after focusing it).
  A chord is modifiers joined with `+`: `Ctrl+C`, `Ctrl+Shift+Z`, `Delete`, `F2`, `Escape`, `Enter`, `Ctrl+Enter`,
  letters, digits, `F1`-`F12`, `Tab`, `Space`, `Backspace`, arrows, `Home`/`End`/`PageUp`/`PageDown`, `Plus`, `Minus`.
  Use `--to Editor` to put focus on the canvas first.
- `menu` walks the real menu bar by header text or by AutomationId parts, for example `menu Edit/Undo`,
  `menu "Page/Move Selection To/1"` (the last segment of a dynamic entry is its page index) or `menu View/ZoomToFit`.
  Access-key underscores, spaces, case and a trailing `...` are ignored.
- `dialogs` lists open dialogs and their buttons, edits, check boxes and combo boxes; the exit code is 10 while one is open.
- `shot` renders the main window **and any open dialogs and pop-ups** with `PrintWindow`
  (so a window in front does not end up in the picture) and saves a PNG. It works on a locked desktop as well.
  `--screen` copies the screen pixels of the window's frame instead, which also shows what only the compositor draws
  (a context menu, a tooltip or a drag ghost) but needs the window on screen and uncovered (it is brought to the front
  first; when two instances overlap and the other keeps the foreground, place them apart or quit it). `PrintWindow`
  can lag one frame behind a change: wait a second after a pipe command before a plain `shot`, or use `--screen`.
- `place` restores the main window from the maximized state and puts its **visible** frame (the invisible resize borders
  are compensated for) at the given physical pixels, without activating it. `--work-area` computes the rectangle from the
  primary screen's work area (the screen minus the taskbar): it keeps `--left-gap` pixels free on the left (default 0, for
  example a column of desktop icons) and `--margin` pixels (default 8) on the other three sides. It prints the resulting
  bounds and the work area. Use it before `shot` for screenshots with a predictable size and position.

## Pipe commands

```text
plexus-puppet pipe <cmd> [<json-args>]
plexus-puppet info | pages | nodes [--page <p>] [--ports] | wires [--page <p>] [--ports]
plexus-puppet select <name>... [--add]
plexus-puppet add-node <type> <x> <y> [name]
plexus-puppet connect <Node.port> <Node.port>
plexus-puppet undo | redo
plexus-puppet render <path.png> [--page <p>] [--scale <s>]
```

`pipe` prints the raw JSON `result` of any command of the demo's automation protocol (see
`demo/DemaConsulting.Plexus.Demo.Gui/README.md`), for example `pipe list-annotations`, `pipe copy`,
`pipe paste '{"x":50,"y":600}'` or `pipe view '{"fit":true}'`. The other commands are wrappers that print one line per item;
`add-node` accepts `Mixer` for `demo.audio.Mixer` (a short name must be unique among the registered types). Pipe
commands change the model **directly**, bypassing the editor's input
handling: use them to set up and to verify, and use the mouse and key commands to test the editor.

## Real-mouse gestures

All gestures work in **diagram units** (so they do not depend on zoom, window position or monitor scaling): the demo's
`geometry` pipe command converts to physical screen pixels, the window is brought to the foreground, and the real cursor
then moves in small steps with short pauses so Avalonia sees genuine pointer moves.

```text
plexus-puppet drag-node <name> <dx> <dy>
plexus-puppet ctrl-drag-node <name> <dx> <dy>        # Ctrl held: duplicate
plexus-puppet double-click-node <name>               # opens Properties (a Switch toggles instead)
plexus-puppet double-click-name <name>               # inline rename
plexus-puppet right-click-node <name>                # context menu
plexus-puppet click-node <name> [--shift|--ctrl]
plexus-puppet click-toggle <group title or id>       # collapse / expand
plexus-puppet drag-connect <Node.port> <Node.port>   # port names may carry @in / @out
plexus-puppet drag-palette <type> <diagram-x> <diagram-y> [--hold-shot <path.png>]   # <type>: Mixer or demo.audio.Mixer
plexus-puppet click-at <x> <y> [--shift|--ctrl] [--double]
plexus-puppet drag-at <x1> <y1> <x2> <y2> [--shift|--ctrl]   # rubber band on empty canvas
plexus-puppet drag-element <automationId> <dx> <dy>          # drag a control by screen pixels, e.g. Main_PaletteSplitter
plexus-puppet wheel <notches> [--ctrl] [--at <x>,<y>]        # +up / -down; Ctrl zooms around the cursor
```

Presses land on a point of the node body that is neither its name label nor a port; `drag-connect` presses inside the
source port's hit area and releases inside the target's. A source tries its `out` side first and a target its `in` side
(the flow meter has both sides under one name); add `@in` or `@out` to choose explicitly.

`drag-palette --hold-shot <path.png>` takes a screen copy (as `shot --screen`) at the drop point while the button is
still down, which shows the drop ghost and the drop feedback; the drop itself still happens afterwards.

`drag-palette` finds the entry by its AutomationId, which is `NodePalette.GetAutomationId(key)` of the library (the
Puppet restates that rule in `PaletteIds`; a unit test asserts that both agree): `NodePalette_` plus the node type key
with every character other than a letter, digit or underscore replaced by an underscore. A short name such as `Mixer`
is resolved against the entries that exist. When the entry is scrolled out of the palette the Puppet asks it to scroll
itself into view with the UI Automation ScrollItem pattern before reading its position, so it needs no filtering.

### Safety checks

Before any real input the Puppet checks that the topmost window at the target pixel belongs to the instance. If not (another
window covers it, it is off screen or scrolled out of the editor, the session is locked) **nothing is sent** and the command
exits with code 11. `key` likewise exits with 7 unless an element of the instance holds keyboard focus. This also protects
the user's other windows.

## Exit codes

| Code | Meaning |
| ---: | --- |
| 0 | Success |
| 1 | Element, node or menu item not found |
| 2 | Timeout (an instance did not exit after `quit`) |
| 3 | Instance not running (no answer on its pipe) |
| 4 | Launch failed or refused |
| 5 | Usage error |
| 6 | Element disabled |
| 7 | No usable keyboard focus |
| 8 | Input had no effect |
| 9 | Element is read-only |
| 10 | A dialog is open (`dialogs`) |
| 11 | Target not on the instance's window; real input refused |
| 12 | The application rejected a pipe command (the reason is printed) |
| 99 | Unexpected error (add `--trace` for the stack) |

## Worked example: two-instance clipboard test

```powershell
$puppet = '.\tools\DemaConsulting.Plexus.Puppet\bin\Release\net10.0-windows\plexus-puppet.exe'

& $puppet launch --instance a --sample audio
& $puppet launch --instance b                      # showcase, full type set
& $puppet launch --instance c --types basic        # only the math types

# Copy two connected nodes in a: the real Ctrl+C, after selecting them with the real mouse
& $puppet click-node Mix --instance a
& $puppet click-node Tone --shift --instance a
& $puppet key Ctrl+C --instance a

# Paste into b with the pipe (the instance does not need to be in front) and verify
& $puppet pipe paste '{"x":50,"y":600}' --instance b
& $puppet nodes --instance b
& $puppet wires --instance b

# The basic instance does not know the Mixer or Filter types: the status bar says so
& $puppet pipe paste --instance c
& $puppet read Status_Text --instance c            # Paste failed: this application does not know the node type(s) ...
& $puppet shot C:\temp\c.png --instance c

foreach ($alias in 'a', 'b', 'c') { & $puppet quit --instance $alias }
```

Another sequence: drag a node, check the wires and undo it.

```powershell
& $puppet launch --sample audio
& $puppet drag-node Tone 0 120               # real mouse drag
& $puppet info                                      # undoText = "Move"
& $puppet shot C:\temp\after-drag.png
& $puppet key Ctrl+Z --to Editor                    # one undo entry reverts the drag
& $puppet quit
```

## Notes

- The Puppet needs an **unlocked interactive desktop** for mouse and key input, and the OS clipboard needs one as well.
  UI Automation (`tree`, `click`, `type`, `menu`, `dialogs`) and `shot` keep working on a locked session; mouse and key
  commands then refuse (exit 11 / 7) instead of guessing.
- `shot` draws what `PrintWindow` returns for each window, so rounded or shadowed window chrome may differ slightly
  from the screen.
- A chord with `Alt` (for example `Alt+Enter`) keeps Alt down for a moment after the key is released, as a person
  does: releasing it at once reaches the window before a dialog the chord opens and moves the focus to the menu bar.
  Before a chord the Puppet releases only the modifier keys that really are down (a key-up of a key that is not down
  is not something a keyboard sends, and a lone Alt key-up opens the menu bar of an Avalonia window).
- The Puppet is not a Plexus feature: it is built and unit-tested (argument, chord, geometry and format parsing) in
  `test/DemaConsulting.Plexus.Puppet.Tests`, but no UI runs in those tests.

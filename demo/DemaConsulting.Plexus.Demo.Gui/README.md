# Plexus Demo (GUI)

`plexus-demo` is the reference host application for the Plexus diagram editor: an Avalonia 12 desktop app that
shows how little a host has to write to get a complete diagram editor. It is **not shipped**; it exists to
demonstrate the library, to be driven by UI automation (the Puppet) and to find usability problems in the library.

What the host supplies (see `demo/DemaConsulting.Plexus.Demo.Model`): node types, port types, icons and a file
format. Everything else - drawing, hit testing, selection, drag/connect/resize gestures, undo/redo, clipboard
interchange, rendering to PNG, persistence - comes from the library.

## Demo content

The demo ships five small, original node domains (namespaced keys such as `demo.math.Add`; each type has a
`NodeType.Category`), each with its own port type and color:

| Domain   | Node types                                                  | Port type (color)  |
| -------- | ----------------------------------------------------------- | ------------------ |
| Math     | Constant, Add, Multiply, Sine, Clamp, Scope                 | `number` (orange)  |
| Plumbing | Pump, Valve, Tank, Junction, Flow Meter, Drain              | `fluid` (blue)     |
| Logic    | Switch, AND, OR, NOT, XOR, Lamp                             | `bool` (green)     |
| Audio    | Oscillator, Filter, Envelope, Amplifier, Mixer, Speaker     | `audio` (violet)   |
| Workflow | Start, Task, Decision, Approval, Notify, End                | `flow` (yellow)    |

Features each domain shows off:

- Plumbing: pumps and tanks have a `Fluid` choice property (Water, Oil, Steam); the flow meter has the port `flow`
  on both sides and a numeric `reading` output. Pipes are colored by what flows through them (see Wire appearance).
- Logic: the gates use a custom `INodeVisual` (`GateVisual`) that draws classic gate symbols while name, ports and
  hit testing keep working; `LogicEvaluator.Evaluate(page)` computes the live true/false value of every logic port,
  which colors the logic wires by state (see Wire appearance). Double-click a switch to flip it.
- Ports that vary per node: AND, OR and XOR have an `Inputs` property (2 to 8, edited with a number box; other
  values are clamped). A `NodeType.PortProvider` (`DemoNodeTypes.GatePorts`) gives the gate that many inputs named
  `a`, `b`, `c`, ...: raising the count adds inputs and makes the gate taller, lowering it removes the last inputs and
  their wires and shrinks the gate back (never below its default size), all in one undo entry, and wires on the inputs
  that remain stay connected. Two-input gates are exactly the gates of earlier versions, so older files load unchanged.
  AND is on when every input is on, OR when any is, and XOR when an odd number are (parity); an unconnected input counts
  as off. The `logic` sample has a three-input AND gate (`All`) with a note explaining the `Inputs` property.
- Tooltips: rest the pointer on a node, a port or a wire. Ports show their label, type (`Boolean`, `Number`, ...) and
  description (every demo port has one). Every node type has a `NodeType.Description`, and most add a live summary of
  their current values from their `NodeType.ToolTip` (a pump `Water · 12 L/min · 20 m`, a filter `Low-pass, 900 Hz`,
  an envelope `A 10 ms · D 120 ms · S 70 % · R 300 ms`; logic nodes their live state: a switch on or off, a gate's
  output, a lamp lit or dark). Wires show `source → target`, and the window's `DiagramEditor.ToolTipRequested` handler
  adds `= on` or `= off` to boolean wires and gives an oscillator, an envelope and a filter a control as their tooltip:
  a small plot thumbnail of their current settings (waveform, ADSR curve or magnitude response) above their name and
  the short summary, such as `Sine, 440 Hz`. View > Show Tooltips turns them off.
- Audio inspectors: the oscillator (under Oscillator: Waveform, Frequency, Amplitude 0 to 1, Phase in degrees and DC
  Offset), the envelope (under Envelope: Attack, Decay, Sustain and Release, in that order, through
  `PropertyDefinition.Order`) and the filter (under Filter: Type, then Cutoff) open a custom inspector: the library's
  properties dialog with a live preview next
  to the grid. The oscillator shows two cycles of its signal against time (ms) with amplitude grid lines, the envelope its
  ADSR curve with the A, D, S and R phases (an illustrative 200 ms sustain hold), and the filter its magnitude response
  (a second-order model) from 20 Hz to 20 kHz on a logarithmic axis with the cutoff marked and a dB axis. The previews
  redraw on every committed edit, reset and cancel, before OK. The math is in the model (`AudioPreview`,
  `OscillatorSettings`, `EnvelopeSettings`, `FilterSettings`) and the plots are drawn by a small theme-aware control
  (`Inspectors/SignalPlot.cs`) that follows light and dark mode; its phase labels stay inside the plot area, clear of
  the axis labels and of each other (a label with no room near its phase is left out), and the vertical axis title
  starts at the plot's left edge so it never covers the top tick label. Files saved before the new oscillator properties
  existed load with their defaults.
- Workflow: `Task` nodes carry a `TaskDetails` host data object whose annotations show off the property grid (see
  Host data and the property grid below), edited in the properties dialog and in the properties panel.
- Samples: `math`, `plumbing` (a water circuit plus an oil and a steam circuit), `logic`, `audio`, `workflow`,
  `groups` (nested and collapsed groups), `wiring` (the wire junction and crossing audit page) and `showcase` (all of
  them as pages).
- Wiring: the `wiring` sample is a one-page audit of how wires are drawn. It shows fan-outs to 2, 3 and 4 inputs
  (a junction dot with a square "T" corner where a branch leaves the trunk), a split where both branches turn at one
  point, two nets merging into an input that allows fan-in (one net, so a merge dot where they join), a horizontal wire
  hopping over the vertical wires of other nets (close crossings share one wider hop), different nets that touch (no
  dot, no hop), a dashed steam fan-out, and a collapsed group fanning out through display routes. Use it to check any
  change to wire drawing: `plexus-demo-render --sample wiring --out <dir> --scale 2`.
- Connections: `DemoAppearance.Options` carries `ConnectionRules.All(ConnectionRules.Default, ConnectionRules.SameType)`,
  so a wire joins ports of one type only (a number output cannot drive an audio input) and an input accepts a single
  wire unless its port declaration opts into fan-in (`PortDefinition.AllowsMultipleWires`); such a connection is
  refused, and the editor shows the reason (for example "Input 'Add1.a' already has a wire; it does not accept more
  than one.") while the wire is dragged. The workflow step inputs and the Scope input opt into fan-in, because paths of
  a workflow merge naturally and a scope overlays several traces; merging wires are drawn with a dot where they join.

## Wire appearance

`DemoAppearance.Options` (in the Model project, so the GUI and the `plexus-demo-render` command-line tool
draw the same wires) is the one `DiagramOptions` of the demo; it installs one `Diagram.WireStyler`, composed with `WireStylers.Combine`:

1. **By port type** (`WireStylers.ByPortType()`): every wire takes the color of its source port's type.
2. **Fluid**: a fluid wire is colored by the fluid that flows in it - Water blue, Oil brown, Steam light gray and
   dashed. The fluid is the `Fluid` property of the pump or tank found by walking upstream from the wire's source
   through valves, junctions and flow meters, so changing the `Fluid` of a pump recolors every pipe after it.
3. **Logic state**: a bool wire is bright green and thick while it carries true and dark gray while it carries
   false, as computed by `LogicEvaluator`. The values are computed once per page and diagram version by
   `WireStylers.PerPage` (any change, including a switch's `On`, undo and redo, advances `Diagram.Version`), not once
   per wire, with no cache or subscription in the demo.

The styler is configuration, not content, so it is not saved in the file: `DemoSamples`, `DemoDocument.Load`,
`DemoSession.New` and `DemoSession.OpenSample` each pass `DemoAppearance.Options` to the diagram they create.

## Running

```text
plexus-demo [--open <file.json> | --sample <name>] [--types full|basic] [--automation <name>]
```

- `--open <file.json>` opens a Plexus diagram JSON file at startup.
- `--sample <name>` opens a built-in sample: `math`, `plumbing`, `logic`, `audio`, `workflow`, `groups`, `wiring` or
  `showcase` (every page in one diagram). The default (no file and no sample) is `showcase`.
- `--types full|basic` registers every demo node type (default) or only the math types (Constant, Add, Multiply,
  Sine, Clamp, Scope).
  With `basic` and no explicit document the app starts with an empty diagram, because the showcase needs every type.
- `--automation <name>` (**demo only**) serves the automation pipe `plexus-demo-<name>`, described below. Off by
  default.
- `-h`, `--help` prints the usage text (the app is a Windows GUI executable, so run it from an attached console).

### Demonstrating the clipboard failure path

Start two instances, one `--types full` and one `--types basic`. Copy a Mixer node in the full instance and
paste in the basic one: nothing is pasted and the status bar reports
`Paste failed: this application does not know the node type(s) demo.audio.Mixer.` Copying the other way round works.

## Features

- **Menus** - File (New, Open, Save, Save As, Open Sample, Export Page PNG, Exit), Edit (Undo and Redo showing the
  action's description, Cut, Copy, Paste, Duplicate, Delete, Select All, Find Node..., Rename, Properties...), Insert
  (Node per type, Annotation, Group Selection, Ungroup, Toggle Collapse), Page (Add, Rename, Delete, Move Left/Right,
  Move Selection To), View (Zoom In/Out/To Fit, Zoom To Selection, Show Grid, Snap To Grid, Show Tooltips, Properties
  Panel, Show Toolbar).
- **Navigation**: Edit > Find Node... (Ctrl+F) asks for a node name and shows that node with
  `DiagramEditor.BringIntoView`, switching to its page and showing the collapsed group that hides it, then selects it;
  View > Zoom To Selection fits the selection with `DiagramEditor.ZoomToFit(Editor.Selection)`.
- **Toolbar** under the menu bar: compact flat icon buttons (28 pixels) in groups with separators: New, Open, Save |
  Undo, Redo | Cut, Copy, Paste, Delete | Group, Ungroup, Toggle Collapse, Add Annotation | Zoom In, Zoom Out, Zoom To
  Fit, Show Grid, Snap To Grid | Properties, Properties Panel | Export PNG. A button owns no behavior: it raises the
  click of the menu item of the same command (a toggle flips the item's check mark), so the menu item's handler is the
  one implementation, and the button copies the item's enabled and checked state (Undo is dimmed with nothing to undo,
  Group needs a selection, the grid and snap toggles show their state). The tooltip is the item's caption and shortcut,
  for example `Undo Move (Ctrl+Z)` or `Properties (Alt+Enter)`. The buttons do not take the keyboard focus, so Cut,
  Copy, Paste and Delete still reach the canvas. The icons come from the `Material.Icons.Avalonia` NuGet package,
  which only this demo references (the app registers `MaterialIconStyles` next to the Fluent theme); View > Show
  Toolbar hides and shows the bar.
- **Palette** on the left is the library's `NodePalette` control, and the whole integration is one property: the
  window creates `new NodePalette { Editor = Editor, Zoom = 0.75 }`. The palette lists the node types of the editor's
  current document (so File > New and Open need no handling), has its own search box (`NodePalette_Filter`, prompt
  "Search nodes") that matches the display name, title, key or category without regard to case (type `pump` or
  `logic`), and draws every node type exactly as the canvas draws the node (icon, ports, summary, custom gate
  visual), grouped under their `NodeType.Category` headers (Math, Plumbing, Logic, Audio, Workflow). Drag an entry
  onto the canvas to create that node (a ghost shows where it will land; the library's `DiagramEditor` handles the
  drop), or double-click it to add the node in the middle of the view (the palette's default activation,
  `DiagramEditor.AddNodeAtCenter`). The palette draws at a fixed zoom of 0.75 instead of following the editor's
  zoom, which keeps it one comfortable size while the canvas zooms and avoids rebuilding every item on each wheel step.
  The palette starts 205 pixels wide, one column of node types. A draggable splitter (`Main_PaletteSplitter`, a
  `GridSplitter`) between the palette and the canvas resizes it, from its 205-pixel starting width (one full column,
  never clipped) up to what leaves the canvas 300 pixels;
  the palette lays each category out in as many columns as fit, so a wider palette shows two, three or more node types
  per row (about 420 pixels for two columns and 650 for three), with the boxes lined up in a grid, the headers across
  the full width and Left/Right/Up/Down moving through the grid. The width is not remembered between runs.
  The same display names (for example "Flow meter" for a node titled FLOW METER) are used by the Insert > Node
  menu, the context menu and the UI Automation names.
- **Switches**: double-click a Switch (or use its context menu > Toggle, or the `toggle-switch` automation command)
  to flip `On` and recolor its wires; the properties dialog stays available from the context menu and Edit >
  Properties....
- **Page tabs** above the canvas are bound straight to `diagram.Pages`; select a tab to show that page, double-click
  to rename.
- **Annotations** are edited in place: double-click one (or press F2 with it selected, or use the context menu's Edit
  Text) to open a text box over it; Ctrl+Enter or clicking elsewhere commits, Escape cancels.
- **Properties dialog** (double-click a node other than a switch, context menu > Properties, or Edit > Properties... /
  Alt+Enter with a single node selected; the menu item is disabled otherwise; Alt+Enter opens the dialog once Alt is
  released, so the window's access key handling sees the whole chord and the first key press after the dialog reaches
  the editor): the library's `NodePropertiesDialog.ShowAsync(window, node)`, which is a staged `PropertyGrid`. It
  lists the name (first, as `(Name)`), the declared properties (check box, drop-down for choices, text with validation
  that accepts a unit, color editor, the slider for properties with the editor hint `slider`) and, for Task nodes, the
  annotated `TaskDetails` host data. OK applies everything as one undo entry; Cancel changes nothing; a rejected or
  vetoed rename keeps the dialog open with the reason. The whole integration is one line:
  `Editor.NodeDoubleClicked += async (_, e) => await NodePropertiesDialog.ShowAsync(this, e.Node);` (the demo also
  flips switches on double-click, hence `ActivateNodeAsync`). For oscillators, envelopes and filters `ShowPropertiesAsync`
  passes `new NodePropertiesDialogOptions { Preview = ... }` from `Inspectors/AudioInspectors.cs`, which shows the live
  preview to the right of the grid in a wider dialog; the preview reads the staged values through `NodePreviewContext`
  and never writes, so OK and Cancel behave exactly as in the plain dialog.
- **Properties panel** (View > Properties Panel, F4; hidden at start): a second, docked way to show the same grid. The
  panel is a live `PropertyGrid` (`EditMode = Live`) on the right of the window, between a `GridSplitter` and the
  window edge, and follows the selection: one node, annotation or group is shown (the whole integration is
  `PropertiesGrid.SelectedObject = element`), several elements or none show nothing. Each edit is applied at once as its
  own undo entry, and undo, redo and moving or renaming from elsewhere are reflected in the panel.
- **Host data and the property grid**: `TaskDetails` (in the model project) is a plain mutable class that
  `System.Text.Json` saves with the diagram. Its properties demonstrate the annotations the grid understands:
  `[Category]`, `[Description]`, `[DisplayName]` (Notes is shown as "Summary"), `[ReadOnly]` (Reference),
  `[Browsable(false)]` (Revision is saved but never shown), `[DefaultValue]` (Priority, Labels, Progress and Team are
  bold when they differ from the default), an enumeration (`Priority`, a drop-down), a `[Flags]` enumeration (`Labels`,
  a check list), type converters with standard values (`Team` is a closed list, `Reviewer` an editable one), an
  expandable nested object (`Contact` with Name, E-mail and Phone), collections edited in the collection editor
  (`Checklist` of steps, each with a nested `Remarks` list; the "..." button opens it), `[PasswordPropertyText]`
  (`AccessCode`) and a custom editor named by `[UIHint("slider")]` (`Progress`; the grid honors the data annotation as
  the editor key, so the model project does not reference the grid). The slider editor is
  registered once in `PropertyGrid.DefaultEditors` by `DemoEditors`; it is also used by node properties with the
  editor hint `slider` (the speaker's `Volume`, the mixer's `Master` and the valve's `Open`). The grid edits a working
  copy of the data (`Node.CloneData`), so Cancel changes nothing and the live data object is never mutated.
- **Context menu** (right-click): on a node Toggle (switches only), Properties, Rename, Duplicate, Delete, Move To
  Page; on a wire Delete,
  Reroute; on an annotation Edit Text, Delete; on a group Rename, Toggle Collapse, Ungroup, Delete; on the canvas
  Add Node Here, Paste, Add Annotation Here.
- **Status bar**: last message, selection count, zoom, `Modified *` or `Saved`. A clipboard that cannot be used
  (another process holds it, or the desktop is locked) is reported here and in the `copy`/`cut`/`paste` automation
  reply as `The clipboard is not available (error 0x...)`, also when Ctrl+C/X/V started the command
  (`DiagramEditor.ClipboardFailed`).
- **Title bar**: `Plexus Demo - <file name or Untitled>`, followed by `*` when there are unsaved changes.
- **Unsaved changes** prompt (Save / Discard / Cancel) on New, Open, Open Sample and closing the window.
- **Windows stay on the screen**: the main window (minimum size 640x420) and every dialog (properties, prompt,
  message, unsaved changes) are moved back inside the screen's working area (the screen minus the taskbar) when they
  open, and again when the screens or their scaling change. Moving comes first; a window is shrunk only when it is
  still too big once moved, and never below its minimum size. A maximized or minimized window is left alone.
  (`WindowPlacement`, adapted from another DEMA Consulting application; its arithmetic is unit tested.)

## AutomationIds

Every interactive control has an `AutomationProperties.AutomationId`. Scheme: `Area_Item[_Detail]`, PascalCase
words joined by underscores. `<Type>` is the node type key after the last dot (`Constant`, `Pump`, `FlowMeter`,
`Switch`, `And`, `Mixer`, `Task`, `Decision`, ...; the short names are unique
across the five demo domains) in the menu ids; `<index>` is zero-based.

- Window and bars: `Main_Window`, `Menu_Bar`, `Status_Bar`.
- Canvas: `Editor` (the `DiagramEditor`). Its inline rename box is `DiagramEditor.RenameEditor`.
- File menu: `Menu_File`, `Menu_File_New`, `Menu_File_Open`, `Menu_File_Save`, `Menu_File_SaveAs`,
  `Menu_File_OpenSample`, `Menu_File_OpenSample_<name>` (math, plumbing, logic, audio, workflow, groups, wiring,
  showcase), `Menu_File_ExportPng`,
  `Menu_File_Exit`.
- Edit menu: `Menu_Edit`, `Menu_Edit_Undo`, `Menu_Edit_Redo`, `Menu_Edit_Cut`, `Menu_Edit_Copy`, `Menu_Edit_Paste`,
  `Menu_Edit_Duplicate`, `Menu_Edit_Delete`, `Menu_Edit_SelectAll`, `Menu_Edit_FindNode`, `Menu_Edit_Rename`,
  `Menu_Edit_Properties`.
- Insert menu: `Menu_Insert`, `Menu_Insert_Node`, `Menu_Insert_Node_<Type>`, `Menu_Insert_Annotation`,
  `Menu_Insert_Group`, `Menu_Insert_Ungroup`, `Menu_Insert_ToggleCollapse`.
- Page menu: `Menu_Page`, `Menu_Page_Add`, `Menu_Page_Rename`, `Menu_Page_Delete`, `Menu_Page_MoveLeft`,
  `Menu_Page_MoveRight`, `Menu_Page_MoveSelectionTo`, `Menu_Page_MoveSelectionTo_<index>` (target page index).
- View menu: `Menu_View`, `Menu_View_ZoomIn`, `Menu_View_ZoomOut`, `Menu_View_ZoomToFit`, `Menu_View_ZoomToSelection`,
  `Menu_View_ShowGrid`, `Menu_View_SnapToGrid`, `Menu_View_ShowToolTips`, `Menu_View_PropertiesPanel`,
  `Menu_View_ShowToolbar`.
- Toolbar: `Toolbar` (the bar) and one button per command, `Toolbar_<Command>`, where `<Command>` is the name of the
  menu item it mirrors without its menu: `Toolbar_New`, `Toolbar_Open`, `Toolbar_Save`, `Toolbar_Undo`,
  `Toolbar_Redo`, `Toolbar_Cut`, `Toolbar_Copy`, `Toolbar_Paste`, `Toolbar_Delete`, `Toolbar_Group`,
  `Toolbar_Ungroup`, `Toolbar_ToggleCollapse`, `Toolbar_Annotation`, `Toolbar_ZoomIn`, `Toolbar_ZoomOut`,
  `Toolbar_ZoomToFit`, `Toolbar_ShowGrid` (toggle), `Toolbar_SnapToGrid` (toggle), `Toolbar_Properties`,
  `Toolbar_PropertiesPanel` (toggle) and `Toolbar_ExportPng`. The toggles are `ToggleButton`s, the rest `Button`s.
- Properties panel: `Properties_Panel` (the pane, hidden until View > Properties Panel), `Main_PropertiesPanelSplitter`
  (its `GridSplitter`) and, inside it, the library grid ids below (`PropertyGrid`, `PropertyGrid_Row_<path>`, ...).
- Palette splitter: `Main_PaletteSplitter` (the `GridSplitter` that resizes the palette; drag it sideways).
- Palette: `NodePalette` (the control), `NodePalette_Filter` (its built-in search box) and `NodePalette_<key>` for
  each entry, the library's scheme (`NodePalette.GetAutomationId`): the whole node type key with every character other
  than a letter, digit or underscore replaced by an underscore (`NodePalette_demo_audio_Mixer`,
  `NodePalette_demo_plumbing_FlowMeter`). The entries replace the former `Palette_List`, `Palette_Item_<Type>` and
  `Palette_Filter` ids. An entry outside the scrolled view still exists; UI Automation can scroll it into view (the
  ScrollItem pattern, which `plexus-puppet drag-palette` uses) or activate it (the Invoke pattern).
- Page tabs: `Pages_Tabs`, `Page_Tab_<index>` (current order; renumbered when pages move).
- Status bar: `Status_Text`, `Status_Selection`, `Status_Zoom`, `Status_Dirty`.
- Properties dialog (the library's `PropertyGridDialog`): `PropertyGridDialog` (the window), `PropertyGridDialog_OK`,
  `PropertyGridDialog_Cancel`, `PropertyGridDialog_Status` (why OK was refused), `PropertyGridDialog_Preview` (the live
  preview panel of an oscillator, envelope or filter dialog) and `Inspector_Plot` (the plot inside it). It replaces
  the former `Properties_Dialog`, `Properties_Name`, `Properties_Prop_<name>`, `Properties_Details_*`, `Properties_Ok`
  and `Properties_Cancel` ids.
- Property grid rows (in the dialog and in the panel; the library assigns them): `PropertyGrid` (the grid),
  `PropertyGrid_Row_<path>` (a row; node rows are `Node.Name`, `Property.<name>` for a declared property such as
  `Property.Capacity` and `Data.<path>` for host data such as `Data.Priority`, `Data.Contact.Email` or
  `Data.Checklist[0].Text`; annotation rows are `Text` and `Fill`, group rows `Title` and `IsCollapsed`),
  `PropertyGrid_Editor_<path>` (the editor control of that
  row: a text box, combo box, check box, slider, ...), `PropertyGrid_Category_<category>` (a category band, for example
  `PropertyGrid_Category_General`), `PropertyGrid_Search`, `PropertyGrid_SortCategorized` and
  `PropertyGrid_SortAlphabetical`. A collection row opens `CollectionEditorDialog` (`CollectionEditorDialog_OK`,
  `CollectionEditorDialog_Cancel`). When the dialog and the panel are both open the same row ids exist in both windows;
  search the dialog window for the dialog's rows.
- Prompt dialog: `Dialog_Prompt`, `Dialog_Prompt_Text`, `Dialog_Prompt_Ok`, `Dialog_Prompt_Cancel`.
- Message dialog: `Dialog_Message`, `Dialog_Message_Text`, `Dialog_Message_Yes` (Save), `Dialog_Message_No`
  (Discard), `Dialog_Message_Cancel`, `Dialog_Message_Ok`.
- Context menu: `Context_Menu`; node `Context_Node_Toggle` (switches only), `Context_Node_Properties`,
  `Context_Node_Rename`, `Context_Node_Duplicate`,
  `Context_Node_Delete`, `Context_Node_MoveToPage`, `Context_Node_MoveToPage_<index>`; wire `Context_Wire_Delete`,
  `Context_Wire_Reroute`; annotation `Context_Annotation_EditText`, `Context_Annotation_Delete`; group
  `Context_Group_Rename`, `Context_Group_ToggleCollapse`, `Context_Group_Ungroup`, `Context_Group_Delete`; canvas
  `Context_Canvas_AddNode`, `Context_Canvas_AddNode_<Type>`, `Context_Canvas_Paste`, `Context_Canvas_AddAnnotation`.

## Automation endpoint (demo only)

Enabled only by `--automation <name>`. The app then serves the named pipe `plexus-demo-<name>` (Windows:
`\\.\pipe\plexus-demo-<name>`): local machine only and restricted to the current user
(`PipeOptions.CurrentUserOnly`). Never ship this in a real product.

**Protocol**: UTF-8, newline-delimited JSON. One request per line, `{"cmd":"...", ...arguments}`; exactly one reply
line per request: `{"ok":true,"result":...}` or `{"ok":false,"error":"..."}`. Clients may connect one after
another (one at a time). Every request runs on the UI thread. Errors never crash the app.

Conventions:

- Elements are referenced by **id** or by **node name** (groups also by title). `ids` / `names` take a list or a
  single string; `id` and `name` single values are accepted as well.
- Pages are referenced by zero-based **index or name**.
- Ports are `"Node.port"` (split at the last dot). `connect` uses the output for `from` and the input for `to` when
  both directions share the port name (the flow meter has `flow` on both sides); `fromDirection` / `toDirection`
  (`in` or `out`) override that.
- Commands that name elements on another page switch the editor to that page first.

Commands (arguments in parentheses, `?` = optional; result keys after the arrow):

- `ping` -> `{pong:true}`.
- `info` -> `pid`, `title`, `file`, `dirty`, `canUndo`, `canRedo`, `undoText`, `redoText`, `pages` (names),
  `currentPage` (index), `currentPageName`, `zoom`, `offsetX`, `offsetY`, `selection` (ids), `status`, `typeSet`.
- `list-pages` -> `[{index,name,nodes,wires,annotations,groups}]`.
- `select-page` (`page`), `add-page` (`name?`; the new page is shown), `rename-page` (`page`, `name`),
  `delete-page` (`page`; the last page cannot be deleted) -> the page.
- `list-nodes` (`page?`, default the shown page) -> `[{id,name,typeKey,bounds,properties,data,visible,page,ports}]`;
  `data` is the host data as a JSON object (or null); `ports` are the node's current ports
  (`[{name,direction,type}]`), so `set-property` of a gate's `Inputs` followed by `list-nodes` shows the new inputs.
- `list-wires` (`page?`) -> `[{id,source:"Node.port",target:"Node.port",route:[{x,y}],visible}]`.
- `list-annotations` (`page?`) -> `[{id,text,bounds,visible}]`.
- `list-groups` (`page?`) -> `[{id,title,collapsed,bounds,members,visible}]`.
- `add-node` (`type` = a full key such as `demo.audio.Mixer` or its short name `Mixer`, `x`, `y`, `name?`) ->
  `{id,name}`; the node becomes the selection.
- `move-node` (`node`, `x`, `y` = top-left) -> the new bounds.
- `set-property` (`node`, `name`, `value`) -> the stored value, parsed against the property definition.
- `toggle-switch` (`node`) -> `{name,on}`; flips a Switch's `On` like a double-click (undoable); other nodes are refused.
- `rename` (`node`, `name`) -> `{name}`; when refused, the error is the rename pipeline's reason.
- `connect` (`from`, `to`, `fromDirection?`, `toDirection?`) -> the wire.
- `delete` (`ids` or `names`) -> `{deleted}`; deletes the elements directly (`DiagramEditor.Delete`) and leaves the
  selection alone.
- `select` (`ids` or `names`, `add?`), `clear-selection`, `select-all` -> `{selection:[{id,kind,name}]}`.
- `copy`, `cut` (current selection, OS clipboard) -> `{copied}` / `{cut}`.
- `paste` (`x?`, `y?`: both or neither) -> `{success,ids,missingNodeTypes,warnings,error}`. A refused paste is
  reported as data (`success:false`), not as an error, and also appears in the status bar.
- `duplicate` -> `{ids}`.
- `undo`, `redo` -> `{performed,canUndo}` / `{performed,canRedo}`; like Ctrl+Z / Ctrl+Y, either one while an inline edit
  or a running gesture is open ONLY cancels it (`performed:true`, history untouched) and the next one acts on the
  history (`DiagramEditor.Undo`/`Redo`).
- `group` (`ids` or `names`, `title?`) -> the group; `ungroup` (`group` = id or title) -> `{ungrouped}`;
  `collapse` (`group`, `collapsed?` default true) -> the group.
- `move-to-page` (`ids` or `names`, `page`) -> `{moved,page}`.
- `geometry` (one of `node`, `port` + optional `direction`, `element`, `point:{x,y}` or `editor:true`) -> see below.
- `view` (`zoom?`, `offsetX?`, `offsetY?`, `fit?`) -> `{zoom,offsetX,offsetY}`.
- `bring-into-view` (`element`, `node` or `name`) -> `{page,zoom,offsetX,offsetY}` (`page` is the shown page's index);
  `DiagramEditor.BringIntoView`: switches to the element's page and centers it without zooming in.
- `zoom-to` (`ids` or `names`, `margin?` default 30) -> `{page,zoom,offsetX,offsetY}`; `DiagramEditor.ZoomToFit` of
  the elements (the first element's page is shown).
- `tooltip` (`element` or `node`, or `port` + optional `direction`) -> `{text,preview}`: the tooltip the editor would
  show (`DiagramEditor.GetToolTipContent`), or a null `text` when there is none. When the tooltip is a control (the
  plot thumbnail of an oscillator, envelope or filter), `text` is the texts of its text blocks, one per line (for example
  `Osc1\nSaw, 220 Hz` or `Tone\nLow-pass, 900 Hz`), and `preview` is true; for text tooltips `preview` is false.
- `save` (`path?`, default the current file) -> `{path}`; `open` (`path`) -> like `info`, without an unsaved-changes
  prompt; `new` -> like `info`.
- `render` (`path`, `page?`, `scale?`) -> `{path,page,bytes}`; writes a PNG through `PlexusRenderer`.
- `quit` -> `{quitting:true}`, then the app exits without prompting.

### Geometry

`geometry` is built so an external driver can use **real operating-system mouse input**. Every point and rectangle
is returned in both coordinate systems:

```json
{"diagram": {"x": 450, "y": 75, "width": 90, "height": 105},
 "screen":  {"x": 788, "y": 326, "width": 95.9, "height": 111.9, "centerX": 836, "centerY": 382},
 "visible": true}
```

`screen` values are **physical screen pixels** (what `SetCursorPos` and `SendInput` expect): they include the window
position, the editor's zoom and offset and the monitor scaling (`scale` in the reply). `visible` is false when the
element is scrolled out of the editor; `view` with `fit` or an offset brings it back.

- `node`: `bounds` (the box), `center`, `nameRect` (double-click it to rename), `title`, and `ports[]` with `name`,
  `direction`, `anchor` (where wires attach), `press` (a point inside the port's hit area: press there and drag to
  connect) and `hit` (the whole hit rectangle).
- `port`: the same description for one port (`"port":"Node.port"`; add `direction` when both directions share the
  name).
- `element` (id or name): `bounds`. Wires add `points` and `segmentMidpoints` (grab a midpoint to drag a segment),
  groups add `header`, `toggle` (the collapse toggle) and `collapsed` (all published by the scene, so `bounds` is the
  drawn frame, including the extra height of a collapsed box with proxy ports; a group inside a collapsed group
  reports `hidden:true`), annotations add `resizeHandle`.
- `editor`: the editor control's own screen rectangle.
- `point` (`{"x":..,"y":..}` in diagram units): `point` with the same `diagram`/`screen`/`visible` description, for
  aiming at empty canvas (rubber band, drop target, wheel anchor).

## Tests

`test/DemaConsulting.Plexus.Demo.Tests` covers the command line (`GuiOptions`), the properties dialog and panel (the
library grid in the demo, the host data annotations, the collection editor, the slider editor and the audio inspectors
with their live previews, the oscillator, envelope and filter tooltips, the plot control and its label layout
(`SignalPlotLayoutTests`)), the audio preview math (`AudioPreviewTests`),
the automation dispatcher (headless Avalonia, no pipe), the pipe transport (with a fake executor) and the real main
window (menus, AutomationIds, status bar, context menu, dialogs) on the headless platform.

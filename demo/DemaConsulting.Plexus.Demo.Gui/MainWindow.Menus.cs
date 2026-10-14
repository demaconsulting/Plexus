using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using DemaConsulting.Plexus.Demo.Model;

namespace DemaConsulting.Plexus.Demo.Gui;

/// <content>
///     The menu bar and the logic that keeps its items enabled and captioned.
/// </content>
internal sealed partial class MainWindow
{
    /// <summary>The Undo menu item (its caption names the action).</summary>
    private readonly MenuItem _undo = new();

    /// <summary>The Redo menu item (its caption names the action).</summary>
    private readonly MenuItem _redo = new();

    /// <summary>The Cut menu item.</summary>
    private MenuItem _cut = new();

    /// <summary>The Copy menu item.</summary>
    private MenuItem _copy = new();

    /// <summary>The Duplicate menu item.</summary>
    private MenuItem _duplicate = new();

    /// <summary>The Delete menu item.</summary>
    private MenuItem _delete = new();

    /// <summary>The Rename menu item.</summary>
    private MenuItem _rename = new();

    /// <summary>The Properties menu item.</summary>
    private MenuItem _properties = new();

    /// <summary>
    ///     The menu bar.
    /// </summary>
    private Menu _menuBar = new();

    /// <summary>The Group Selection menu item.</summary>
    private MenuItem _group = new();

    /// <summary>The Ungroup menu item.</summary>
    private MenuItem _ungroup = new();

    /// <summary>The Toggle Collapse menu item.</summary>
    private MenuItem _toggleCollapse = new();

    /// <summary>The Delete Page menu item.</summary>
    private MenuItem _deletePage = new();

    /// <summary>The Move Page Left menu item.</summary>
    private MenuItem _moveLeft = new();

    /// <summary>The Move Page Right menu item.</summary>
    private MenuItem _moveRight = new();

    /// <summary>The Move Selection To submenu.</summary>
    private MenuItem _moveSelectionTo = new();

    /// <summary>The Show Grid menu item.</summary>
    private MenuItem _showGrid = new();

    /// <summary>The Snap To Grid menu item.</summary>
    private MenuItem _snapToGrid = new();

    /// <summary>The Properties Panel menu item.</summary>
    private MenuItem _showPropertiesPanel = new();

    /// <summary>The Zoom To Selection menu item.</summary>
    private MenuItem _zoomToSelection = new();

    /// <summary>The Show Tooltips menu item.</summary>
    private MenuItem _showToolTips = new();

    /// <summary>
    ///     Builds the menu bar.
    /// </summary>
    /// <returns>The menu.</returns>
    private Menu BuildMenuBar()
    {
        var menu = _menuBar = new Menu().WithId(AutomationIds.MenuBar);
        menu.Items.Add(BuildFileMenu());
        menu.Items.Add(BuildEditMenu());
        menu.Items.Add(BuildInsertMenu());
        menu.Items.Add(BuildPageMenu());
        menu.Items.Add(BuildViewMenu());
        return menu;
    }

    /// <summary>
    ///     Builds the File menu.
    /// </summary>
    /// <returns>The menu item.</returns>
    private MenuItem BuildFileMenu()
    {
        var samples = Ui.Item("Open _Sample", AutomationIds.Menu("File_OpenSample"));
        foreach (var name in DemoSamples.Names)
        {
            samples.Items.Add(Ui.Item(name, AutomationIds.Menu($"File_OpenSample_{name}"), () => Fire(() => OpenSampleAsync(name))));
        }

        var file = Ui.Item("_File", AutomationIds.Menu("File"));
        file.Items.Add(Ui.Item("_New", AutomationIds.Menu("File_New"), () => Fire(NewAsync), Ui.Keys(Key.N), hotKey: true));
        file.Items.Add(Ui.Item("_Open...", AutomationIds.Menu("File_Open"), () => Fire(OpenAsync), Ui.Keys(Key.O), hotKey: true));
        file.Items.Add(Ui.Item("_Save", AutomationIds.Menu("File_Save"), () => Fire(() => SaveAsync(saveAs: false)), Ui.Keys(Key.S), hotKey: true));
        file.Items.Add(Ui.Item("Save _As...", AutomationIds.Menu("File_SaveAs"), () => Fire(() => SaveAsync(saveAs: true)), Ui.Keys(Key.S, KeyModifiers.Control | KeyModifiers.Shift), hotKey: true));
        file.Items.Add(new Separator());
        file.Items.Add(samples);
        file.Items.Add(Ui.Item("Export Page _PNG...", AutomationIds.Menu("File_ExportPng"), () => Fire(ExportPngAsync)));
        file.Items.Add(new Separator());
        file.Items.Add(Ui.Item("E_xit", AutomationIds.Menu("File_Exit"), Close));
        return file;
    }

    /// <summary>
    ///     Builds the Edit menu. Its shortcuts are shown but handled by the editor itself, so they work when the canvas has focus.
    /// </summary>
    /// <returns>The menu item.</returns>
    private MenuItem BuildEditMenu()
    {
        Configure(_undo, "_Undo", "Edit_Undo", () => Editor.Undo(), Ui.Keys(Key.Z));
        Configure(_redo, "_Redo", "Edit_Redo", () => Editor.Redo(), Ui.Keys(Key.Y));
        _cut = Ui.Item("Cu_t", AutomationIds.Menu("Edit_Cut"), () => Fire(Editor.CutAsync), Ui.Keys(Key.X));
        _copy = Ui.Item("_Copy", AutomationIds.Menu("Edit_Copy"), () => Fire(Editor.CopyAsync), Ui.Keys(Key.C));
        var paste = Ui.Item("_Paste", AutomationIds.Menu("Edit_Paste"), () => Fire(() => Editor.PasteAsync()), Ui.Keys(Key.V));
        _duplicate = Ui.Item("Duplicate", AutomationIds.Menu("Edit_Duplicate"), () => Editor.DuplicateSelection(), Ui.Keys(Key.D));
        _delete = Ui.Item("_Delete", AutomationIds.Menu("Edit_Delete"), Editor.DeleteSelection, new KeyGesture(Key.Delete));
        var selectAll = Ui.Item("Select _All", AutomationIds.Menu("Edit_SelectAll"), Editor.SelectAll, Ui.Keys(Key.A));
        var findNode = Ui.Item("_Find Node...", AutomationIds.Menu("Edit_FindNode"), () => Fire(FindNodeAsync), Ui.Keys(Key.F), hotKey: true);
        _rename = Ui.Item("Re_name", AutomationIds.Menu("Edit_Rename"), RenameSelectedNode, new KeyGesture(Key.F2));
        // cspell:ignore roperties - "P_roperties" marks R as the access key (P is taken by Paste)
        _properties = Ui.Item("P_roperties...", AutomationIds.Menu("Edit_Properties"), () => Fire(ShowSelectedNodePropertiesAsync), PropertiesGesture);

        var edit = Ui.Item("_Edit", AutomationIds.Menu("Edit"));
        foreach (var item in new Control[] { _undo, _redo, new Separator(), _cut, _copy, paste, _duplicate, _delete, new Separator(), selectAll, findNode, _rename, _properties })
        {
            edit.Items.Add(item);
        }

        return edit;
    }

    /// <summary>
    ///     Builds the Insert menu.
    /// </summary>
    /// <returns>The menu item.</returns>
    private MenuItem BuildInsertMenu()
    {
        var nodes = Ui.Item("_Node", AutomationIds.Menu("Insert_Node"));
        foreach (var type in Session.Diagram.Types)
        {
            var key = type.Key;
            nodes.Items.Add(Ui.Item(type.DisplayName, AutomationIds.Menu($"Insert_Node_{AutomationIds.ShortName(key)}"), () => AddNodeFromMenu(key)));
        }

        _group = Ui.Item("_Group Selection...", AutomationIds.Menu("Insert_Group"), () => Fire(GroupSelectionAsync), Ui.Keys(Key.G), hotKey: true);
        _ungroup = Ui.Item("_Ungroup", AutomationIds.Menu("Insert_Ungroup"), () => Editor.UngroupSelection(), Ui.Keys(Key.G, KeyModifiers.Control | KeyModifiers.Shift), hotKey: true);
        _toggleCollapse = Ui.Item("Toggle _Collapse", AutomationIds.Menu("Insert_ToggleCollapse"), () => Editor.ToggleCollapseSelection());

        var insert = Ui.Item("_Insert", AutomationIds.Menu("Insert"));
        insert.Items.Add(nodes);
        insert.Items.Add(Ui.Item("_Annotation", AutomationIds.Menu("Insert_Annotation"), () => Editor.AddAnnotationAtCenter(DemoSession.DefaultAnnotationText)));
        insert.Items.Add(new Separator());
        insert.Items.Add(_group);
        insert.Items.Add(_ungroup);
        insert.Items.Add(_toggleCollapse);
        return insert;
    }

    /// <summary>
    ///     Builds the Page menu.
    /// </summary>
    /// <returns>The menu item.</returns>
    private MenuItem BuildPageMenu()
    {
        _deletePage = Ui.Item("_Delete", AutomationIds.Menu("Page_Delete"), () => Fire(DeletePageAsync));
        _moveLeft = Ui.Item("Move _Left", AutomationIds.Menu("Page_MoveLeft"), () => MoveCurrentPage(-1));
        _moveRight = Ui.Item("Move _Right", AutomationIds.Menu("Page_MoveRight"), () => MoveCurrentPage(1));
        _moveSelectionTo = Ui.Item("Move Selection _To", AutomationIds.Menu("Page_MoveSelectionTo"));

        var page = Ui.Item("_Page", AutomationIds.Menu("Page"));
        page.Items.Add(Ui.Item("_Add", AutomationIds.Menu("Page_Add"), () => Session.AddPage()));
        page.Items.Add(Ui.Item("Re_name...", AutomationIds.Menu("Page_Rename"), () => Fire(RenamePageAsync)));
        page.Items.Add(_deletePage);
        page.Items.Add(new Separator());
        page.Items.Add(_moveLeft);
        page.Items.Add(_moveRight);
        page.Items.Add(_moveSelectionTo);
        return page;
    }

    /// <summary>
    ///     Builds the View menu.
    /// </summary>
    /// <returns>The menu item.</returns>
    private MenuItem BuildViewMenu()
    {
        _showGrid = Ui.Item("Show _Grid", AutomationIds.Menu("View_ShowGrid"));
        _showGrid.ToggleType = MenuItemToggleType.CheckBox;
        _showGrid.IsChecked = Editor.ShowGrid;
        _showGrid.PropertyChanged += (_, e) =>
        {
            if (e.Property == MenuItem.IsCheckedProperty)
            {
                Editor.ShowGrid = _showGrid.IsChecked;
            }
        };

        _snapToGrid = Ui.Item("_Snap To Grid", AutomationIds.Menu("View_SnapToGrid"));
        _snapToGrid.ToggleType = MenuItemToggleType.CheckBox;
        _snapToGrid.IsChecked = Editor.SnapToGrid;
        _snapToGrid.PropertyChanged += (_, e) =>
        {
            if (e.Property == MenuItem.IsCheckedProperty)
            {
                Editor.SnapToGrid = _snapToGrid.IsChecked;
            }
        };

        _showPropertiesPanel = Ui.Item("_Properties Panel", AutomationIds.Menu("View_PropertiesPanel"), gesture: new KeyGesture(Key.F4), hotKey: true);
        _showPropertiesPanel.ToggleType = MenuItemToggleType.CheckBox;
        _showPropertiesPanel.IsChecked = false;
        _showPropertiesPanel.PropertyChanged += (_, e) =>
        {
            if (e.Property == MenuItem.IsCheckedProperty)
            {
                IsPropertiesPanelVisible = _showPropertiesPanel.IsChecked;
            }
        };

        // Tooltips on nodes, ports and wires are the editor's own; the demo adds live values to them (see OnToolTipRequested)
        _showToolTips = Ui.Item("Show _Tooltips", AutomationIds.Menu("View_ShowToolTips"));
        _showToolTips.ToggleType = MenuItemToggleType.CheckBox;
        _showToolTips.IsChecked = Editor.ShowToolTips;
        _showToolTips.PropertyChanged += (_, e) =>
        {
            if (e.Property == MenuItem.IsCheckedProperty)
            {
                Editor.ShowToolTips = _showToolTips.IsChecked;
            }
        };

        _zoomToSelection = Ui.Item("Zoom To _Selection", AutomationIds.Menu("View_ZoomToSelection"), () => Editor.ZoomToFit(Editor.Selection));

        var view = Ui.Item("_View", AutomationIds.Menu("View"));
        view.Items.Add(Ui.Item("Zoom _In", AutomationIds.Menu("View_ZoomIn"), Editor.ZoomIn, Ui.Keys(Key.OemPlus), hotKey: true));
        view.Items.Add(Ui.Item("Zoom _Out", AutomationIds.Menu("View_ZoomOut"), Editor.ZoomOut, Ui.Keys(Key.OemMinus), hotKey: true));
        view.Items.Add(Ui.Item("Zoom To _Fit", AutomationIds.Menu("View_ZoomToFit"), () => Editor.ZoomToFit(), Ui.Keys(Key.D0), hotKey: true));
        view.Items.Add(_zoomToSelection);
        view.Items.Add(new Separator());
        view.Items.Add(_showGrid);
        view.Items.Add(_snapToGrid);
        view.Items.Add(_showToolTips);
        view.Items.Add(new Separator());
        view.Items.Add(_showPropertiesPanel);
        view.Items.Add(BuildShowToolbarItem());
        return view;
    }

    /// <summary>
    ///     Gives a menu item its identity and action (its caption is set by <see cref="RefreshMenus"/>).
    /// </summary>
    /// <param name="item">The item to configure.</param>
    /// <param name="header">The initial caption.</param>
    /// <param name="idPath">The id path after <c>Menu_</c>.</param>
    /// <param name="action">What to do.</param>
    /// <param name="gesture">The shortcut to display.</param>
    private static void Configure(MenuItem item, string header, string idPath, Action action, KeyGesture gesture)
    {
        item.Header = header;
        item.InputGesture = gesture;
        AutomationProperties.SetAutomationId(item, AutomationIds.Menu(idPath));
        item.Click += (_, _) => action();
    }

    /// <summary>
    ///     Updates menu captions and enablement from the current state: undo/redo names, selection-dependent items and the page list.
    /// </summary>
    private void RefreshMenus()
    {
        var history = Session.Diagram.History;
        _undo.Header = history.UndoDescription is { } undoText ? $"_Undo {undoText}" : "_Undo";
        _undo.IsEnabled = history.CanUndo;
        _redo.Header = history.RedoDescription is { } redoText ? $"_Redo {redoText}" : "_Redo";
        _redo.IsEnabled = history.CanRedo;

        var selection = Editor.Selection;
        var any = selection.Count > 0;
        _cut.IsEnabled = any;
        _copy.IsEnabled = any;
        _duplicate.IsEnabled = any;
        _delete.IsEnabled = any;
        _rename.IsEnabled = selection.Count == 1 && selection[0] is Node;
        _properties.IsEnabled = selection.Count == 1 && selection[0] is Node;
        _group.IsEnabled = selection.Any(e => e is not Wire);
        _ungroup.IsEnabled = selection.Any(e => e is Group);
        _toggleCollapse.IsEnabled = selection.Any(e => e is Group || e.Parent is not null);
        _zoomToSelection.IsEnabled = any;

        var pages = Session.Diagram.Pages;
        var index = Session.CurrentPage is { } current ? pages.IndexOf(current) : -1;
        _deletePage.IsEnabled = pages.Count > 1;
        _moveLeft.IsEnabled = index > 0;
        _moveRight.IsEnabled = index >= 0 && index < pages.Count - 1;
        RebuildMoveToPageMenu(_moveSelectionTo, "Menu_Page_MoveSelectionTo", any);
        RefreshToolbarTips();
    }

    /// <summary>
    ///     Fills a "move to page" submenu with every page except the current one.
    /// </summary>
    /// <param name="parent">The submenu parent item.</param>
    /// <param name="idPrefix">The AutomationId prefix; the page index is appended.</param>
    /// <param name="enabled">Whether there is something to move.</param>
    private void RebuildMoveToPageMenu(MenuItem parent, string idPrefix, bool enabled)
    {
        parent.Items.Clear();
        var pages = Session.Diagram.Pages;
        for (var i = 0; i < pages.Count; i++)
        {
            if (ReferenceEquals(pages[i], Session.CurrentPage))
            {
                continue;
            }

            var target = pages[i];
            parent.Items.Add(Ui.Item(target.Name, $"{idPrefix}_{i}", () => MoveSelectionToPage(target)));
        }

        parent.IsEnabled = enabled && parent.Items.Count > 0;
    }
}

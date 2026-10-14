using Avalonia.Controls;
using DemaConsulting.Plexus.Avalonia;
using DemaConsulting.Plexus.Demo.Gui.Dialogs;
using DemaConsulting.Plexus.Demo.Model;
using PlexusPoint = DemaConsulting.Plexus.Geometry.Point;

namespace DemaConsulting.Plexus.Demo.Gui;

/// <content>
///     The editor's context menu: what a right-click offers on a node, a wire, an annotation, a group or the empty canvas.
/// </content>
internal sealed partial class MainWindow
{
    /// <summary>
    ///     Builds and opens the context menu for a right-click reported by the editor.
    /// </summary>
    /// <param name="sender">The editor.</param>
    /// <param name="e">Where and on what the user right-clicked.</param>
    private void OnContextRequested(object? sender, DiagramContextRequestedEventArgs e) =>
        BuildContextMenu(e.Element, e.Position).Open(Editor);

    /// <summary>
    ///     Builds the context menu for an element or the empty canvas.
    /// </summary>
    /// <param name="element">The right-clicked element, or null for the empty canvas.</param>
    /// <param name="position">The right-clicked diagram position.</param>
    /// <returns>The menu (not yet open).</returns>
    internal ContextMenu BuildContextMenu(DiagramElement? element, global::Avalonia.Point position)
    {
        var menu = new ContextMenu().WithId(AutomationIds.ContextMenu);
        switch (element)
        {
            case Node node:
                FillNodeMenu(menu, node);
                break;
            case Wire wire:
                menu.Items.Add(Ui.Item("Delete", AutomationIds.Context("Wire_Delete"), Editor.DeleteSelection));
                menu.Items.Add(Ui.Item("Reroute", AutomationIds.Context("Wire_Reroute"), wire.Reroute));
                break;
            case Annotation annotation:
                menu.Items.Add(Ui.Item("Edit Text", AutomationIds.Context("Annotation_EditText"), () => Editor.BeginEditAnnotation(annotation)));
                menu.Items.Add(Ui.Item("Delete", AutomationIds.Context("Annotation_Delete"), Editor.DeleteSelection));
                break;
            case Group group:
                menu.Items.Add(Ui.Item("Rename...", AutomationIds.Context("Group_Rename"), () => Fire(() => RenameGroupAsync(group))));
                menu.Items.Add(Ui.Item("Toggle Collapse", AutomationIds.Context("Group_ToggleCollapse"), () => Editor.ToggleCollapseSelection()));
                menu.Items.Add(Ui.Item("Ungroup", AutomationIds.Context("Group_Ungroup"), () => Editor.UngroupSelection()));
                menu.Items.Add(Ui.Item("Delete", AutomationIds.Context("Group_Delete"), Editor.DeleteSelection));
                break;
            default:
                FillCanvasMenu(menu, position);
                break;
        }

        return menu;
    }

    /// <summary>
    ///     Fills the context menu of a node: Toggle (switches only), Properties, Rename, Duplicate, Delete, Move To Page.
    /// </summary>
    /// <param name="menu">The menu.</param>
    /// <param name="node">The node that was right-clicked (already selected by the editor).</param>
    private void FillNodeMenu(ContextMenu menu, Node node)
    {
        // A switch gets the quick action first: flipping it recolors the logic wires it drives
        if (node.Type.Key == DemoNodeTypes.Switch)
        {
            menu.Items.Add(Ui.Item("Toggle", AutomationIds.Context("Node_Toggle"), () => ToggleSwitch(node)));
        }

        menu.Items.Add(Ui.Item("Properties...", AutomationIds.Context("Node_Properties"), () => Fire(() => ShowPropertiesAsync(node))));
        menu.Items.Add(Ui.Item("Rename", AutomationIds.Context("Node_Rename"), () => Editor.BeginRename(node)));
        menu.Items.Add(Ui.Item("Duplicate", AutomationIds.Context("Node_Duplicate"), () => Editor.DuplicateSelection()));
        menu.Items.Add(Ui.Item("Delete", AutomationIds.Context("Node_Delete"), Editor.DeleteSelection));

        var move = Ui.Item("Move To Page", AutomationIds.Context("Node_MoveToPage"));
        RebuildMoveToPageMenu(move, AutomationIds.Context("Node_MoveToPage"), enabled: true);
        menu.Items.Add(move);
    }

    /// <summary>
    ///     Fills the context menu of the empty canvas: Add Node Here, Paste, Add Annotation Here.
    /// </summary>
    /// <param name="menu">The menu.</param>
    /// <param name="at">The right-clicked diagram position.</param>
    private void FillCanvasMenu(ContextMenu menu, global::Avalonia.Point at)
    {
        // New elements land on the grid under the pointer
        var position = Editor.SnapPoint(at.ToPlexus());

        var add = Ui.Item("Add Node Here", AutomationIds.Context("Canvas_AddNode"));
        foreach (var type in Session.Diagram.Types)
        {
            var key = type.Key;
            add.Items.Add(Ui.Item(type.DisplayName, AutomationIds.Context($"Canvas_AddNode_{AutomationIds.ShortName(key)}"), () => Session.AddNode(key, position)));
        }

        menu.Items.Add(add);
        menu.Items.Add(Ui.Item("Paste", AutomationIds.Context("Canvas_Paste"), () => Fire(() => Editor.PasteAsync(position))));
        menu.Items.Add(Ui.Item("Add Annotation Here", AutomationIds.Context("Canvas_AddAnnotation"), () => Session.AddAnnotation(position)));
    }

    /// <summary>
    ///     Asks for a new group title and applies it.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <returns>A task that completes when the dialog is closed.</returns>
    private async Task RenameGroupAsync(Group group)
    {
        var title = await PromptDialog.AskAsync(this, "Rename group", "Group title", group.Title).ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(title))
        {
            group.Title = title.Trim();
        }
    }
}

using Avalonia.Controls;
using Avalonia.Platform.Storage;
using DemaConsulting.Plexus.Avalonia.PropertyGrid.Plexus;
using DemaConsulting.Plexus.Demo.Gui.Dialogs;
using DemaConsulting.Plexus.Demo.Model;

namespace DemaConsulting.Plexus.Demo.Gui;

/// <content>
///     Command handlers that need dialogs or file pickers; the commands themselves live in <see cref="DemoSession"/>.
/// </content>
internal sealed partial class MainWindow
{
    /// <summary>
    ///     The file type shown in the open and save pickers.
    /// </summary>
    private static readonly FilePickerFileType DiagramFiles = new("Plexus diagram (*.json)") { Patterns = ["*.json"] };

    /// <summary>
    ///     The file type shown in the export picker.
    /// </summary>
    private static readonly FilePickerFileType PngFiles = new("PNG image (*.png)") { Patterns = ["*.png"] };

    /// <summary>
    ///     Asks what to do with unsaved changes before they would be lost.
    /// </summary>
    /// <returns>True when the caller may go ahead (nothing to lose, the user saved or chose to discard).</returns>
    private async Task<bool> ConfirmDiscardAsync()
    {
        if (!Session.IsDirty)
        {
            return true;
        }

        return await MessageDialog.ConfirmDiscardAsync(this, Session.DisplayName).ConfigureAwait(true) switch
        {
            DialogChoice.Yes => await SaveAsync(saveAs: false).ConfigureAwait(true),
            DialogChoice.No => true,
            _ => false,
        };
    }

    /// <summary>
    ///     File &gt; New.
    /// </summary>
    /// <returns>A task that completes when the command is done.</returns>
    private async Task NewAsync()
    {
        if (await ConfirmDiscardAsync().ConfigureAwait(true))
        {
            Session.New();
            Session.SetMessage("New diagram.");
        }
    }

    /// <summary>
    ///     File &gt; Open...
    /// </summary>
    /// <returns>A task that completes when the command is done.</returns>
    private async Task OpenAsync()
    {
        if (!await ConfirmDiscardAsync().ConfigureAwait(true))
        {
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open diagram",
            AllowMultiple = false,
            FileTypeFilter = [DiagramFiles],
        }).ConfigureAwait(true);
        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
        {
            Session.Open(path);
            Session.SetMessage($"Opened {Session.DisplayName}");
        }
    }

    /// <summary>
    ///     File &gt; Open Sample.
    /// </summary>
    /// <param name="name">The sample name.</param>
    /// <returns>A task that completes when the command is done.</returns>
    private async Task OpenSampleAsync(string name)
    {
        if (await ConfirmDiscardAsync().ConfigureAwait(true))
        {
            Session.OpenSample(name);
            Session.SetMessage($"Opened sample '{name}'.");
        }
    }

    /// <summary>
    ///     File &gt; Save / Save As...
    /// </summary>
    /// <param name="saveAs">True to always ask for a file.</param>
    /// <returns>True when the diagram was saved.</returns>
    private async Task<bool> SaveAsync(bool saveAs)
    {
        var path = Session.FilePath;
        if (saveAs || path is null)
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save diagram",
                SuggestedFileName = path is null ? "diagram.json" : Path.GetFileName(path),
                DefaultExtension = "json",
                FileTypeChoices = [DiagramFiles],
            }).ConfigureAwait(true);
            if (file?.TryGetLocalPath() is not { } chosen)
            {
                return false;
            }

            path = chosen;
        }

        Session.Save(path);
        return true;
    }

    /// <summary>
    ///     File &gt; Export Page PNG...
    /// </summary>
    /// <returns>A task that completes when the command is done.</returns>
    private async Task ExportPngAsync()
    {
        var page = Session.RequirePage();
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export page as PNG",
            SuggestedFileName = $"{page.Name}.png",
            DefaultExtension = "png",
            FileTypeChoices = [PngFiles],
        }).ConfigureAwait(true);
        if (file?.TryGetLocalPath() is { } path)
        {
            DemoSession.ExportPng(page, path, scale: 2);
            Session.SetMessage($"Exported {page.Name} to {Path.GetFileName(path)}");
        }
    }

    /// <summary>
    ///     Adds a node of the given type in the middle of the view (the same thing a palette double-click does) and gives the canvas the keyboard focus.
    /// </summary>
    /// <param name="typeKey">The node type key.</param>
    private void AddNodeFromMenu(string typeKey)
    {
        Editor.AddNodeAtCenter(Session.Diagram.Types.Get(typeKey));
        Editor.Focus();
    }

    /// <summary>
    ///     Edit &gt; Rename: starts the inline rename of the selected node.
    /// </summary>
    private void RenameSelectedNode()
    {
        if (Editor.Selection is [Node node])
        {
            Editor.BeginRename(node);
        }
    }

    /// <summary>
    ///     Edit &gt; Properties...: opens the properties dialog of the single selected node.
    /// </summary>
    /// <returns>A task that completes when the dialog closes; a completed task when no single node is selected.</returns>
    private Task ShowSelectedNodePropertiesAsync() =>
        Editor.Selection is [Node node] ? ShowPropertiesAsync(node) : Task.CompletedTask;

    /// <summary>
    ///     Handles a double-click on a node: a switch flips, any other node opens its properties.
    /// </summary>
    /// <remarks>
    ///     A switch has a single interesting property, so flipping it is the quicker double-click; its
    ///     properties dialog stays available from the context menu and Edit &gt; Properties.
    /// </remarks>
    /// <param name="node">The double-clicked node.</param>
    /// <returns>A task that completes when the dialog closes; a completed task for a switch.</returns>
    private Task ActivateNodeAsync(Node node)
    {
        if (node.Type.Key != DemoNodeTypes.Switch)
        {
            return ShowPropertiesAsync(node);
        }

        ToggleSwitch(node);
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Flips a switch and reports its new state in the status bar.
    /// </summary>
    /// <param name="node">The switch node.</param>
    private void ToggleSwitch(Node node) =>
        Session.SetMessage($"{node.Name} switched {(DemoSession.ToggleSwitch(node) ? "on" : "off")}.");

    /// <summary>
    ///     Insert &gt; Group Selection: asks for a title, then groups.
    /// </summary>
    /// <returns>A task that completes when the command is done.</returns>
    private async Task GroupSelectionAsync()
    {
        var title = await PromptDialog.AskAsync(this, "Group selection", "Group title", "Group").ConfigureAwait(true);
        if (title is not null)
        {
            Editor.GroupSelection(string.IsNullOrWhiteSpace(title) ? "Group" : title.Trim());
        }
    }

    /// <summary>
    ///     Page &gt; Rename...
    /// </summary>
    /// <returns>A task that completes when the command is done.</returns>
    private async Task RenamePageAsync()
    {
        var page = Session.RequirePage();
        var name = await PromptDialog.AskAsync(this, "Rename page", "Page name", page.Name).ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(name))
        {
            page.Name = name.Trim();
        }
    }

    /// <summary>
    ///     Page &gt; Delete.
    /// </summary>
    /// <returns>A completed task (deleting is undoable, so there is nothing to confirm).</returns>
    private Task DeletePageAsync()
    {
        Session.DeletePage(Session.RequirePage());
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Page &gt; Move Left / Right.
    /// </summary>
    /// <param name="delta">-1 or +1.</param>
    private void MoveCurrentPage(int delta) => Session.MovePage(Session.RequirePage(), delta);

    /// <summary>
    ///     Page &gt; Move Selection To.
    /// </summary>
    /// <param name="target">The destination page.</param>
    private void MoveSelectionToPage(DiagramPage target) => Editor.MoveSelectionToPage(target);

    /// <summary>
    ///     Opens the properties dialog for a node; OK applies everything as one undo entry.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>A task that completes when the dialog is closed.</returns>
    private async Task ShowPropertiesAsync(Node node)
    {
        // The library's dialog does everything: the staged property grid, validation and one undo entry on OK
        if (await NodePropertiesDialog.ShowAsync(this, node).ConfigureAwait(true))
        {
            Session.SetMessage($"Edited {node.Name}");
        }
    }
}

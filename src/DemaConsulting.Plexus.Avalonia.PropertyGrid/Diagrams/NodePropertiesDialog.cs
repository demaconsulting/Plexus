using Avalonia.Controls;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid.Diagrams;

/// <summary>
///     The properties dialog of a Plexus node: a modal window that shows the node's name, properties and host data in a
///     staged <see cref="PropertyGrid"/>. OK applies everything as one undo entry; Cancel changes nothing.
/// </summary>
/// <remarks>
///     <para>
///         It is a thin layer over <see cref="PropertyGridDialog"/> and <see cref="NodePropertySource"/>: the title is
///         <c>"{Name} - Properties"</c>, the grid is staged, and the node adapter makes the commit one undo entry (the
///         rename, the changed properties and the edited host data together). OK is refused with the reason shown while a
///         value is invalid or the rename is vetoed. Custom editors named by <see cref="PropertyDefinition.EditorHint"/> or
///         <see cref="PropertyEditorAttribute"/> come from <see cref="PropertyGrid.DefaultEditors"/>, so register
///         them there once at start-up (see <see cref="PropertyGrid.EditorRegistry"/> for giving a single grid its own, which the dialog's
///         <see cref="PropertyGridDialog.Grid"/> from <see cref="Create"/> also accepts).
///     </para>
///     <para>
///         The dialog's controls carry the automation ids of the grid and its dialog: <c>PropertyGridDialog</c>,
///         <c>PropertyGridDialog_OK</c>, <c>PropertyGridDialog_Cancel</c>, <c>PropertyGridDialog_Status</c> and, for every
///         row, <c>PropertyGrid_Row_{path}</c> and <c>PropertyGrid_Editor_{path}</c> (for example
///         <c>PropertyGrid_Row_Node.Name</c>, see <see cref="NodePropertySource"/> for the row paths).
///     </para>
/// </remarks>
/// <example>
///     One line in the window that hosts a <c>DiagramEditor</c>: double-click a node to edit it. The host data of the node (<c>Node.Data</c>) appears
///     in the dialog as rows when its node type declares a <c>DataType</c> (see <see cref="NodePropertySource"/>).
///     <code><![CDATA[
///     using Avalonia.Controls;
///     using DemaConsulting.Plexus.Avalonia;
///     using DemaConsulting.Plexus.Avalonia.PropertyGrid.Diagrams;
///
///     // Inside a Window (this), with the editor of the window
///     public sealed class EditorWindow : Window
///     {
///         public EditorWindow(DiagramEditor editor)
///         {
///             Content = editor;
///             editor.NodeDoubleClicked += async (_, e) =>
///             {
///                 bool changed = await NodePropertiesDialog.ShowAsync(this, e.Node);   // true when OK applied the edits as one undo entry
///             };
///         }
///     }
///     ]]></code>
/// </example>
public static class NodePropertiesDialog
{
    /// <summary>
    ///     The width of the dialog, in device-independent pixels.
    /// </summary>
    private const double DialogWidth = 480;

    /// <summary>
    ///     The height of the dialog, in device-independent pixels.
    /// </summary>
    private const double DialogHeight = 640;

    /// <summary>
    ///     Shows the properties dialog of a node modally.
    /// </summary>
    /// <param name="owner">The owner window.</param>
    /// <param name="node">The node to edit; it must be attached to a diagram.</param>
    /// <returns>
    ///     A task that completes with <see langword="true"/> when the user pressed OK and the edits were committed as one
    ///     undo entry, and <see langword="false"/> when the user cancelled (nothing changed).
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> or <paramref name="node"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="node"/> is not attached to a diagram.</exception>
    public static Task<bool> ShowAsync(Window owner, Node node)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(node);
        return Create(node).ShowDialog<bool>(owner);
    }

    /// <summary>
    ///     Creates the dialog window without showing it (for hosts that show it themselves).
    /// </summary>
    /// <param name="node">The node to edit; it must be attached to a diagram.</param>
    /// <returns>The window; use <see cref="PropertyGridDialog.Accept"/> or <see cref="Window.Close()"/> to finish it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="node"/> is not attached to a diagram.</exception>
    public static PropertyGridDialog Create(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return new PropertyGridDialog(node, GetTitle(node)) { Width = DialogWidth, Height = DialogHeight };
    }

    /// <summary>
    ///     Gets the title of a node's dialog: <c>"{Name} - Properties"</c>.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>The title.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is <see langword="null"/>.</exception>
    public static string GetTitle(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return node.Name + " - Properties";
    }
}

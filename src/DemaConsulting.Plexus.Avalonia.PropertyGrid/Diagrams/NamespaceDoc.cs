namespace DemaConsulting.Plexus.Avalonia.PropertyGrid.Diagrams;

/// <summary>
///     The diagram adapters of the property grid: they show and edit Plexus diagram elements. <see cref="NodePropertySource"/>
///     lists a node's name, declared properties and host data, <see cref="AnnotationPropertySource"/> and
///     <see cref="GroupPropertySource"/> list the few properties of annotations and groups, and
///     <see cref="NodePropertiesDialog"/> is the ready-made modal dialog for a node.
/// </summary>
/// <remarks>
///     How the package registers the sources: when this assembly loads, it adds the node, annotation and group sources to
///     <see cref="PropertySourceFactory"/> (a module initializer), so <c>grid.SelectedObject = node</c> (or an annotation, or a group) needs no setup
///     as soon as any type of this assembly has been used, which is always true once your code mentions <see cref="PropertyGrid"/>. Every commit through these sources
///     is one undo entry of the element's diagram (<see cref="IPropertyCommitScope"/>): a staged grid (a dialog) makes OK
///     one step, a live grid (a docked panel) makes each edit one step. Custom editors for node properties and host data come from
///     <see cref="PropertyGrid.DefaultEditors"/> (register them once at start-up; the dialog needs that) or from one grid's
///     <see cref="PropertyGrid.EditorRegistry"/>. The rest of the grid is generic and does not
///     depend on Plexus types.
/// </remarks>
/// <example>
///     <para>
///         The dialog is one line in the window that hosts a <c>DiagramEditor</c>, and a docked panel is one property:
///     </para>
///     <code><![CDATA[
///     using Avalonia.Controls;
///     using DemaConsulting.Plexus.Avalonia;
///     using DemaConsulting.Plexus.Avalonia.PropertyGrid;
///     using DemaConsulting.Plexus.Avalonia.PropertyGrid.Diagrams;
///
///     // Inside a Window (this): editor is the window's DiagramEditor
///     public sealed class EditorWindow : Window
///     {
///         public EditorWindow(DiagramEditor editor)
///         {
///             var panel = new PropertyGrid { EditMode = PropertyEditMode.Live };
///             Content = new DockPanel { Children = { editor, panel } };
///
///             // Double-click a node: modal dialog, OK applies everything as one undo entry
///             editor.NodeDoubleClicked += async (_, e) => await NodePropertiesDialog.ShowAsync(this, e.Node);
///
///             // A docked panel follows the selection; every edit in it is one undo entry
///             editor.SelectionChanged += (_, _) => panel.SelectedObject = editor.Selection.Count == 1 ? editor.Selection[0] : null;
///         }
///     }
///     ]]></code>
/// </example>
internal static class NamespaceDoc
{
}

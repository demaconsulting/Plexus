namespace DemaConsulting.Plexus.Avalonia.PropertyGrid.Plexus;

/// <summary>
///     The Plexus adapters of the property grid: they show and edit diagram elements. <see cref="NodePropertySource"/>
///     lists a node's name, declared properties and host data, <see cref="AnnotationPropertySource"/> and
///     <see cref="GroupPropertySource"/> list the few properties of annotations and groups, and
///     <see cref="NodePropertiesDialog"/> is the ready-made modal dialog for a node.
/// </summary>
/// <remarks>
///     The package registers the sources with <see cref="PropertySourceFactory"/> when it loads, so
///     <c>grid.SelectedObject = node</c> (or an annotation, or a group) needs no setup. Every commit through these sources
///     is one undo entry of the element's diagram (<see cref="IPropertyCommitScope"/>): a staged grid (a dialog) makes OK
///     one step, a live grid (a docked panel) makes each edit one step. The rest of the grid is generic and does not
///     depend on Plexus types.
/// </remarks>
/// <example>
///     <para>
///         The dialog is one line in the window that hosts a <c>DiagramEditor</c>, and a docked panel is one property:
///     </para>
///     <code><![CDATA[
///     using DemaConsulting.Plexus.Avalonia;
///     using DemaConsulting.Plexus.Avalonia.PropertyGrid;
///     using DemaConsulting.Plexus.Avalonia.PropertyGrid.Plexus;
///
///     editor.NodeDoubleClicked += async (_, e) => await NodePropertiesDialog.ShowAsync(this, e.Node);
///
///     var panel = new PropertyGrid { EditMode = PropertyEditMode.Live };
///     editor.SelectionChanged += (_, _) => panel.SelectedObject = editor.Selection.Count == 1 ? editor.Selection[0] : null;
///     ]]></code>
/// </example>
internal static class NamespaceDoc
{
}

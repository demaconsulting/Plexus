namespace DemaConsulting.Plexus.Avalonia.PropertyGrid;

/// <summary>
///     A generic, annotation-driven property grid for Avalonia: give it any annotated .NET object and it shows and
///     edits the object's properties honoring the <c>System.ComponentModel</c> annotations, the way the WinForms
///     property grid does. This namespace holds the Avalonia controls (<see cref="PropertyGrid"/>,
///     <see cref="PropertyGridDialog"/>, <see cref="CollectionEditorView"/>) and the UI-free layers they are built on.
/// </summary>
/// <remarks>
///     <para>
///         The layers, from the data to the UI:
///     </para>
///     <list type="bullet">
///         <item>
///             <description>
///                 <b>Property model</b>: <see cref="PropertyItem"/> is one row (name, category, description, type,
///                 read-only, default / changed, value access, type-converter text conversion, standard values,
///                 expandable children and collection elements). Failures are reported as <see cref="PropertyResult"/>.
///             </description>
///         </item>
///         <item>
///             <description>
///                 <b>Sources</b>: <see cref="IPropertySource"/> supplies items. <see cref="ReflectionPropertySource"/> reads
///                 any object through <see cref="System.ComponentModel.TypeDescriptor"/>; <see cref="CustomPropertySource"/> and
///                 <see cref="CustomPropertyItem"/> let a host supply items without reflection;
///                 <see cref="CompositePropertySource"/> merges sources; <see cref="PropertySourceFactory"/> picks the source
///                 for an object; <see cref="PropertyOrdering"/> sorts and groups items.
///             </description>
///         </item>
///         <item>
///             <description>
///                 <b>Edit session</b>: <see cref="PropertyEditSession"/> applies edits immediately
///                 (<see cref="PropertyEditMode.Live"/>) or buffers them until <see cref="PropertyEditSession.Commit"/>
///                 (<see cref="PropertyEditMode.Staged"/>), with per-property errors and a hook for undo transactions;
///                 <see cref="CollectionEditor"/> edits collections through a working copy.
///             </description>
///         </item>
///         <item>
///             <description>
///                 <b>Custom editors</b>: <see cref="PropertyEditorAttribute"/> (or the dependency-free
///                 <c>[UIHint("key")]</c> data annotation) names an editor and
///                 <see cref="PropertyEditorRegistry{TEditor}"/> maps keys and types to editor factories
///                 (<c>Func&lt;PropertyEditorContext, Control&gt;</c>). WinForms <c>UITypeEditor</c> classes cannot be reused.
///             </description>
///         </item>
///         <item>
///             <description>
///                 <b>Controls</b>: <see cref="PropertyGrid"/> is the grid itself (categorized or alphabetical, search,
///                 collapsible categories, nested objects, description pane, keyboard navigation, inline validation,
///                 live or staged editing); <see cref="PropertyGridDialog"/> shows an object in a staged grid with OK and
///                 Cancel; <see cref="CollectionEditorView"/> and <see cref="CollectionEditorDialog"/> edit collections on a
///                 working copy. Their editors, including <see cref="PropertyEditorContext"/> for writing your own, come
///                 with the grid.
///             </description>
///         </item>
///     </list>
///     <para>
///         Text conversion uses the current culture unless a culture is passed explicitly; see <see cref="PropertyItem"/>.
///         The generic layers do not depend on Plexus types; the Plexus adapters (a source for nodes, annotations and groups
///         and the node properties dialog) live in the <c>DemaConsulting.Plexus.Avalonia.PropertyGrid.Plexus</c> namespace.
///         Use the controls from the UI thread.
///     </para>
/// </remarks>
/// <example>
///     <para>
///         Show an object in a grid (live editing, as in a properties panel) and register a custom editor:
///     </para>
///     <code><![CDATA[
///     using System.ComponentModel;
///     using DemaConsulting.Plexus.Avalonia.PropertyGrid;
///
///     public class Mixer
///     {
///         [Category("Levels"), Description("Output level in dB."), DefaultValue(0.0)]
///         public double Gain { get; set; }
///
///         [Category("Levels")]
///         public bool Muted { get; set; }
///
///         [Category("Channels")]
///         public List<string> Names { get; } = ["left", "right"];
///
///         [Browsable(false)]
///         public string Id { get; set; } = "";
///     }
///
///     var grid = new PropertyGrid
///     {
///         SelectedObject = new Mixer(),   // category bands, bold changed values, description pane, search
///         SortMode = PropertySortMode.Categorized,
///     };
///     grid.EditorRegistry = PropertyGrid.CreateEditorRegistry();
///     grid.EditorRegistry.Register("slider", context => new Slider { Value = (double)(context.Value ?? 0.0) });
///     ]]></code>
///     <para>
///         Edit an object in a dialog: OK commits (here inside the host's undo transaction), Cancel discards:
///     </para>
///     <code><![CDATA[
///     var mixer = new Mixer();
///     bool changed = await PropertyGridDialog.ShowAsync(
///         owner,
///         mixer,
///         commitScope: apply =>
///         {
///             using var step = history.BeginTransaction("Edit mixer");
///             apply();
///         });
///     ]]></code>
///     <para>
///         Without the controls, a staged session edits an object through code:
///     </para>
///     <code><![CDATA[
///     using var source = new ReflectionPropertySource(mixer);
///     var session = new PropertyEditSession(source, PropertyEditMode.Staged);
///     session.SetText(session.FindItem("Gain")!, "6.5");   // pending: mixer.Gain is still 0
///     session.Commit();                                    // mixer.Gain is now 6.5
///     ]]></code>
/// </example>
internal static class NamespaceDoc
{
}

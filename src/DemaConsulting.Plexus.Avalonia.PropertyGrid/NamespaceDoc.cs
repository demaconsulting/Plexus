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
///                 (<c>Func&lt;PropertyEditorContext, Control&gt;</c>). Register your factories once at start-up on
///                 <see cref="PropertyGrid.DefaultEditors"/> (every grid and dialog uses them), or on one grid's own registry
///                 (<see cref="PropertyGrid.EditorRegistry"/>); <see cref="PropertyEditorContext"/> is what a factory writes through.
///                 WinForms <c>UITypeEditor</c> classes cannot be reused.
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
///         and the node properties dialog) live in the <c>DemaConsulting.Plexus.Avalonia.PropertyGrid.Diagrams</c> namespace.
///         Use the controls from the UI thread. The application must include <c>&lt;FluentTheme /&gt;</c> in App.axaml: the grid is built from
///         the Fluent theme's control styles.
///     </para>
///     <para>
///         <b>Packages and where to read next.</b> This package, <c>DemaConsulting.Plexus.Avalonia.PropertyGrid</c>, is separate from
///         <c>DemaConsulting.Plexus.Avalonia</c> (the diagram editor and node palette) and from the core model in <c>DemaConsulting.Plexus</c>; it uses the core
///         model only in the <c>Diagrams</c> namespace. To edit a node's properties from a diagram editor, read <see cref="Diagrams.NodePropertiesDialog"/>
///         (a one-line modal dialog) and <see cref="Diagrams.NodePropertySource"/> (the rows and their paths). To edit your own objects, start with
///         <see cref="PropertyGrid"/> and the annotations it honors (<see cref="ReflectionPropertySource"/>), then <see cref="PropertyEditSession"/> for live versus staged
///         edits and undo. To plug in editors, read <see cref="PropertyGrid.DefaultEditors"/>.
///     </para>
/// </remarks>
/// <example>
///     <para>
///         Show an object in a grid (live editing, as in a properties panel). The grid is an ordinary Avalonia control; in XAML map the CLR namespace
///         (<c>xmlns:pg="clr-namespace:DemaConsulting.Plexus.Avalonia.PropertyGrid;assembly=DemaConsulting.Plexus.Avalonia.PropertyGrid"</c>, then
///         <c>&lt;pg:PropertyGrid x:Name="MixerGrid" /&gt;</c>; see <see cref="PropertyGrid"/> for a complete window):
///     </para>
///     <code><![CDATA[
///     using System.ComponentModel;
///     using Avalonia.Controls;
///     using DemaConsulting.Plexus.Avalonia.PropertyGrid;
///
///     var grid = new PropertyGrid
///     {
///         SelectedObject = new Mixer(),   // category bands, bold changed values, description pane, search
///         SortMode = PropertySortMode.Categorized,
///     };
///     var window = new Window { Content = grid, Width = 420, Height = 560 };
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
///     ]]></code>
///     <para>
///         Register a custom editor once at start-up so every grid and dialog offers it; <c>Gain</c> is then annotated with
///         <c>[PropertyEditor("slider")]</c> (the complete slider editor is on <see cref="PropertyGrid.DefaultEditors"/>):
///     </para>
///     <code><![CDATA[
///     using Avalonia.Controls;
///     using DemaConsulting.Plexus.Avalonia.PropertyGrid;
///
///     PropertyGrid.DefaultEditors.Register("slider", context =>
///     {
///         double Current() => Convert.ToDouble(context.Value ?? 0.0);
///         var slider = new Slider { Minimum = 0, Maximum = 100, Value = Current() };
///         slider.ValueChanged += (_, e) =>
///         {
///             if (e.NewValue != Current()) { context.SetValue(e.NewValue); }   // write only user changes, not the value just shown
///         };
///         context.Refreshed += (_, _) => slider.Value = Current();
///         return slider;
///     });
///     ]]></code>
///     <para>
///         Edit an object in a dialog: OK commits (here inside the host's undo transaction), Cancel discards:
///     </para>
///     <code><![CDATA[
///     using System.ComponentModel;
///     using Avalonia.Controls;
///     using DemaConsulting.Plexus;
///     using DemaConsulting.Plexus.Avalonia.PropertyGrid;
///
///     public static class MixerDialogSample
///     {
///         // owner: the window that opens the dialog; history: the diagram's undo history (diagram.History)
///         public static async Task<bool> EditAsync(Window owner, UndoHistory history, Mixer mixer) =>
///             await PropertyGridDialog.ShowAsync(
///                 owner,
///                 mixer,
///                 commitScope: apply => history.Transact("Edit mixer", apply));
///     }
///
///     public class Mixer
///     {
///         [Category("Levels"), DefaultValue(0.0)]
///         public double Gain { get; set; }
///     }
///     ]]></code>
///     <para>
///         Without the controls, a staged session edits an object through code:
///     </para>
///     <code><![CDATA[
///     using System.ComponentModel;
///     using DemaConsulting.Plexus.Avalonia.PropertyGrid;
///
///     var mixer = new Mixer();
///     using var source = new ReflectionPropertySource(mixer);
///     var session = new PropertyEditSession(source, PropertyEditMode.Staged);
///     session.SetText(session.FindItem("Gain")!, "6.5");   // pending: mixer.Gain is still 0
///     session.Commit();                                    // mixer.Gain is now 6.5
///
///     public class Mixer
///     {
///         [Category("Levels"), DefaultValue(0.0)]
///         public double Gain { get; set; }
///     }
///     ]]></code>
/// </example>
internal static class NamespaceDoc
{
}

using System.Reflection;
using DemaConsulting.Plexus.Avalonia.PropertyGrid.Plexus;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests;

/// <summary>
///     System tests of PlexusAvaloniaPropertyGrid: the generic property grid layers used together through the public API.
/// </summary>
public class PlexusAvaloniaPropertyGridTests
{
    /// <summary>
    ///     An annotated object is shown with its categories, labels, descriptions, text, changed state and without its
    ///     hidden properties. Also the platform-evidence scenario for every platform requirement.
    /// </summary>
    [Fact]
    public void PlexusAvaloniaPropertyGrid_ReflectionSource_ShowsAnnotatedObject()
    {
        var mixer = new Mixer { Gain = 6.5 };
        using var source = new ReflectionPropertySource(mixer);

        var groups = PropertyOrdering.Group(source.GetItems(), PropertySortMode.Categorized);

        Assert.Equal(["Channels", "Levels"], groups.Select(g => g.Category));
        var levels = groups[1].Items;
        Assert.Equal(["Gain", "Muted"], levels.Select(i => i.Label));
        var gain = levels[0];
        Assert.Equal("Output level in dB.", gain.Description);
        Assert.Equal("6.5", gain.GetText(System.Globalization.CultureInfo.InvariantCulture));
        Assert.True(gain.IsChanged);
        Assert.False(levels[1].IsChanged);
        Assert.DoesNotContain(source.GetItems(), i => i.Name == "Id");
        Assert.Equal("(Collection)", groups[0].Items[0].GetText());
    }

    /// <summary>
    ///     A staged session edits an object through text, validates, and commits inside the host's transaction hook.
    /// </summary>
    [Fact]
    public void PlexusAvaloniaPropertyGrid_StagedSession_EditsAndCommitsAnObject()
    {
        var mixer = new Mixer();
        var transactions = new List<string>();
        using var source = new ReflectionPropertySource(mixer);
        var session = new PropertyEditSession(source, PropertyEditMode.Staged, apply =>
        {
            transactions.Add("begin");
            apply();
            transactions.Add("end");
        });
        var gain = session.FindItem("Gain")!;

        Assert.True(session.SetText(gain, "6.5", System.Globalization.CultureInfo.InvariantCulture).Succeeded);
        Assert.False(session.SetText(session.FindItem("Muted")!, "maybe").Succeeded);
        var editor = session.BeginCollectionEdit(session.FindItem("Channels")!);
        editor.Add("center");

        Assert.Equal(0, mixer.Gain);
        Assert.Equal(6.5, session.GetValue(gain));
        Assert.True(session.HasChanges);
        Assert.True(session.HasErrors);
        var result = session.Commit();

        Assert.True(result.Succeeded);
        Assert.Equal(6.5, mixer.Gain);
        Assert.Equal(["left", "right", "center"], mixer.Channels);
        Assert.Equal(["begin", "end"], transactions);
    }

    /// <summary>
    ///     A live session writes at once and a composite source mixes host-supplied items with reflected ones.
    /// </summary>
    [Fact]
    public void PlexusAvaloniaPropertyGrid_LiveSessionOverComposite_WritesThroughBothSources()
    {
        var mixer = new Mixer();
        var title = "Main";
        using var reflected = new ReflectionPropertySource(mixer);
        var host = new CustomPropertySource([new CustomPropertyItem("Title", typeof(string), () => title, v => title = (string)v!)]);
        using var composite = new CompositePropertySource(host, reflected);
        var session = new PropertyEditSession(composite);

        session.SetText(session.FindItem("Title")!, "Renamed");
        session.SetText(session.FindItem("Muted")!, "True");

        Assert.Equal("Renamed", title);
        Assert.True(mixer.Muted);
        var names = composite.GetItems().Select(i => i.Name).ToList();
        Assert.Equal("Title", names[0]);
        Assert.Equal(["Channels", "Gain", "Muted"], names.Skip(1).Order());
    }

    /// <summary>
    ///     The editor registry maps the editor requested by an annotation, or the property type, to the UI layer's editor.
    /// </summary>
    [Fact]
    public void PlexusAvaloniaPropertyGrid_EditorRegistry_ResolvesEditorsForAnnotatedProperties()
    {
        using var source = new ReflectionPropertySource(new Person());
        var registry = new PropertyEditorRegistry<string>();
        registry.Register("color", "color-picker");
        registry.Register(typeof(Enum), "drop-down");
        registry.Fallback = "text-box";

        Assert.Equal("color-picker", registry.Resolve(TestHelpers.Item(source, "Tint")));
        Assert.Equal("drop-down", registry.Resolve(TestHelpers.Item(source, "Units")));
        Assert.Equal("text-box", registry.Resolve(TestHelpers.Item(source, "Name")));
    }

    /// <summary>
    ///     The generic grid layers (every type outside the Plexus adapter namespace) have no field, property, method,
    ///     constructor, event or base type that is a Plexus type; only the adapter layer uses them.
    /// </summary>
    [Fact]
    public void PlexusAvaloniaPropertyGrid_Dependencies_GenericLayers_UseNoPlexusTypes()
    {
        const string adapterNamespace = "DemaConsulting.Plexus.Avalonia.PropertyGrid.Plexus";
        const BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        var generic = typeof(PropertyItem).Assembly.GetTypes()
            .Where(t => t.Namespace is not null && !t.Namespace.StartsWith(adapterNamespace, StringComparison.Ordinal) && !t.Name.StartsWith('<'))
            .ToList();

        static bool IsPlexus(Type? type)
        {
            while (type is { HasElementType: true })
            {
                type = type.GetElementType();
            }

            return type?.Namespace is { } ns && ns.StartsWith("DemaConsulting.Plexus", StringComparison.Ordinal) &&
                   !ns.StartsWith("DemaConsulting.Plexus.Avalonia.PropertyGrid", StringComparison.Ordinal);
        }

        var offenders = new List<string>();
        foreach (var type in generic)
        {
            var used = new List<Type?> { type.BaseType };
            used.AddRange(type.GetInterfaces());
            used.AddRange(type.GetFields(all).Select(f => f.FieldType));
            used.AddRange(type.GetProperties(all).Select(p => p.PropertyType));
            foreach (var method in type.GetMethods(all).Cast<MethodBase>().Concat(type.GetConstructors(all)))
            {
                used.AddRange(method.GetParameters().Select(p => p.ParameterType));
                used.Add((method as MethodInfo)?.ReturnType);
            }

            offenders.AddRange(used.Where(IsPlexus).Select(u => $"{type.Name} uses {u!.Name}"));
        }

        Assert.Empty(offenders);
        Assert.Contains(typeof(PropertyItem).Assembly.GetTypes(), t => t.Namespace == adapterNamespace && t.Name == "NodePropertySource");
    }
    /// <summary>
    ///     The grid control shows an annotated object in categories, edits it live through its editors, marks changed values
    ///     and reports an invalid value inline.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PlexusAvaloniaPropertyGrid_PropertyGrid_ShowsAndEditsAnAnnotatedObject() => UiTest.Run(() =>
    {
        var widget = new Widget();
        var grid = new PropertyGrid { SelectedObject = widget };
        var window = GridHarness.Host(grid);
        var caption = GridHarness.Inner<global::Avalonia.Controls.TextBox>(GridHarness.Editor(grid, "Caption"));
        var timeout = GridHarness.Inner<global::Avalonia.Controls.TextBox>(GridHarness.Editor(grid, "Timeout"));

        caption.Focus();
        caption.Text = "Launch";
        GridHarness.Press(window, global::Avalonia.Input.PhysicalKey.Enter);
        timeout.Focus();
        timeout.Text = "99999";
        GridHarness.Press(window, global::Avalonia.Input.PhysicalKey.Enter);

        Assert.Equal(["Appearance", "Behavior", "Data", "Layout"], GridHarness.RowIds(grid).Where(id => id.StartsWith("PropertyGrid_Category_", StringComparison.Ordinal)).Select(id => id["PropertyGrid_Category_".Length..]));
        Assert.Equal("Launch", widget.Caption);
        Assert.Equal(global::Avalonia.Media.FontWeight.Bold, caption.FontWeight);
        Assert.Equal(30, widget.Timeout);
        Assert.Same(GridTheme.ErrorBrush, GridHarness.Row(grid, "Timeout").ValueHost.BorderBrush);
        window.Close();
    });

    /// <summary>
    ///     The dialog helper edits an object in a staged grid: OK commits inside the host scope, Cancel discards.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PlexusAvaloniaPropertyGrid_PropertyGridDialog_CommitsOrDiscardsTheEdits() => UiTest.Run(() =>
    {
        var widget = new Widget();
        var scopes = 0;
        var owner = GridHarness.Host(new global::Avalonia.Controls.TextBlock());
        var accepted = PropertyGridDialog.ShowAsync(owner, widget, null, apply =>
        {
            scopes++;
            apply();
        });
        GridHarness.Pump(owner);
        var dialog = owner.OwnedWindows.OfType<PropertyGridDialog>().Single();
        dialog.Grid.Session!.SetText(dialog.Grid.Session.FindItem("Caption")!, "Launch");

        GridHarness.Click(GridHarness.Get<global::Avalonia.Controls.Button>(dialog, "PropertyGridDialog_OK"));
        GridHarness.Pump(owner);

        Assert.True(accepted.Result);
        Assert.Equal("Launch", widget.Caption);
        Assert.Equal(1, scopes);

        var cancelled = PropertyGridDialog.ShowAsync(owner, widget);
        GridHarness.Pump(owner);
        var second = owner.OwnedWindows.OfType<PropertyGridDialog>().Single();
        second.Grid.Session!.SetText(second.Grid.Session.FindItem("Caption")!, "Discarded");
        GridHarness.Click(GridHarness.Get<global::Avalonia.Controls.Button>(second, "PropertyGridDialog_Cancel"));
        GridHarness.Pump(owner);

        Assert.False(cancelled.Result);
        Assert.Equal("Launch", widget.Caption);
        owner.Close();
    });

    /// <summary>
    ///     The collection editor arranges a collection on a working copy and applies it through the owning session.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PlexusAvaloniaPropertyGrid_CollectionEditor_EditsCollectionThroughWorkingCopy() => UiTest.Run(() =>
    {
        var widget = new Widget();
        var grid = new PropertyGrid { SelectedObject = widget, EditMode = PropertyEditMode.Staged };
        var window = GridHarness.Host(grid);
        var dialog = new CollectionEditorDialog(grid.Session!, grid.Session!.FindItem("Tags")!);
        dialog.Show();

        dialog.View.Add();
        dialog.View.Move(-1);
        dialog.View.MemberGrid.Session!.SetText(dialog.View.MemberGrid.Session.FindItem("Label")!, "first");
        Assert.True(dialog.Accept());

        Assert.Equal(["primary", "pinned"], widget.Tags.Select(t => t.Label));
        Assert.True(grid.Session.Commit().Succeeded);
        Assert.Equal(["first", "primary", "pinned"], widget.Tags.Select(t => t.Label));
        window.Close();
    });

    /// <summary>
    ///     A custom editor registered under the key of a property annotation is used for that property.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PlexusAvaloniaPropertyGrid_CustomEditor_IsUsedForAnnotatedProperty() => UiTest.Run(() =>
    {
        var widget = new Widget();
        var grid = new PropertyGrid { EditorRegistry = SliderEditor.CreateRegistry(), SelectedObject = widget };
        var window = GridHarness.Host(grid);

        var slider = GridHarness.Inner<global::Avalonia.Controls.Slider>(GridHarness.Editor(grid, "Level"));
        slider.Value = 75;

        Assert.Equal(75, widget.Level);
        window.Close();
    });

    /// <summary>
    ///     A Plexus node is shown by a grid given the node, a staged session over it commits the rename, a property and an edited
    ///     data object as one undo entry, and a live session makes each edit its own entry.
    /// </summary>
    [Fact]
    public void PlexusAvaloniaPropertyGrid_PlexusNode_IsShownAndEditedAsOneUndoEntry()
    {
        // Arrange
        var (diagram, node) = NodeFixture.Create();
        var original = node.Data;
        var before = diagram.History.UndoCount;
        using var source = (NodePropertySource)PropertySourceFactory.Create(node);
        var staged = new PropertyEditSession(source, PropertyEditMode.Staged);

        // Act: staged
        staged.SetText(staged.FindItem("Name")!, "Lead");
        staged.SetText(staged.FindItem("Frequency")!, "880 Hz");
        staged.SetText(staged.FindItem("Level")!, "9");
        var committed = staged.Commit();
        var stagedEntries = diagram.History.UndoCount - before;

        // Act: live
        var live = new PropertyEditSession(source, PropertyEditMode.Live);
        live.SetText(live.FindItem("Level")!, "4");
        live.SetText(live.FindItem("Frequency")!, "100");

        // Assert
        Assert.True(committed.Succeeded);
        Assert.Equal(1, stagedEntries);
        Assert.Equal(2, diagram.History.UndoCount - before - stagedEntries);
        Assert.Equal("Lead", node.Name);
        Assert.Equal(100.0, node.Properties["Frequency"]);
        Assert.Equal(4, ((ToneSettings)node.Data!).Level);
        Assert.Equal(5, ((ToneSettings)original!).Level);
    }

    /// <summary>
    ///     The node properties dialog commits on OK as one undo entry and discards on Cancel.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PlexusAvaloniaPropertyGrid_NodePropertiesDialog_CommitsOrDiscardsAsOneUndoEntry() => UiTest.Run(() =>
    {
        // Arrange
        var (diagram, node) = NodeFixture.Create();
        var before = diagram.History.UndoCount;
        var owner = GridHarness.Host(new global::Avalonia.Controls.TextBlock { Text = "owner" });
        var cancelled = NodePropertiesDialog.ShowAsync(owner, node);
        GridHarness.Pump(owner);
        var first = owner.OwnedWindows.OfType<PropertyGridDialog>().Single();
        first.Grid.Session!.SetText(first.Grid.Session.FindItem("Name")!, "Dropped");

        // Act: Cancel
        GridHarness.Click(GridHarness.Get<global::Avalonia.Controls.Button>(first, "PropertyGridDialog_Cancel"));
        GridHarness.Pump(owner);
        var accepted = NodePropertiesDialog.ShowAsync(owner, node);
        GridHarness.Pump(owner);
        var second = owner.OwnedWindows.OfType<PropertyGridDialog>().Single();
        second.Grid.Session!.SetText(second.Grid.Session.FindItem("Name")!, "Kept");
        second.Grid.Session.SetText(second.Grid.Session.FindItem("Level")!, "8");
        GridHarness.Click(GridHarness.Get<global::Avalonia.Controls.Button>(second, "PropertyGridDialog_OK"));
        GridHarness.Pump(owner);

        // Assert
        Assert.False(cancelled.Result);
        Assert.True(accepted.Result);
        Assert.Equal("Kept", node.Name);
        Assert.Equal(8, ((ToneSettings)node.Data!).Level);
        Assert.Equal(before + 1, diagram.History.UndoCount);
        owner.Close();
    });
    /// <summary>
    ///     The headless Avalonia session used by the UI phase starts and runs a body on the UI thread.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PlexusAvaloniaPropertyGrid_HeadlessHost_RunsOnTheUiThread() =>
        UiTest.Run(() => Assert.NotNull(global::Avalonia.Application.Current));
}

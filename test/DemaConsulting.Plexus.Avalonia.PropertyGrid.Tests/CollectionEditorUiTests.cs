using Avalonia.Controls;
using Avalonia.VisualTree;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests;

/// <summary>
///     An entry of a <see cref="Catalog"/>: a collection element that itself holds a collection.
/// </summary>
public sealed class Entry
{
    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the tags (a nested collection).</summary>
    public List<Tag> Tags { get; set; } = [];

    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>
///     An object whose collection holds elements with collections of their own.
/// </summary>
public sealed class Catalog
{
    /// <summary>Gets or sets the entries.</summary>
    public List<Entry> Entries { get; set; } = [new Entry { Name = "alpha" }, new Entry { Name = "beta" }];
}

/// <summary>
///     Tests of the collection editor view and dialog.
/// </summary>
public class CollectionEditorUiTests
{
    /// <summary>
    ///     Creates a grid over a target in a window.
    /// </summary>
    /// <param name="target">The target object.</param>
    /// <param name="mode">The edit mode.</param>
    /// <returns>The grid and its window.</returns>
    private static (PropertyGrid Grid, Window Window) Create(object target, PropertyEditMode mode = PropertyEditMode.Live)
    {
        var grid = new PropertyGrid { SelectedObject = target, EditMode = mode };
        return (grid, GridHarness.Host(grid));
    }

    /// <summary>
    ///     Lists the texts of the member list of a view.
    /// </summary>
    /// <param name="view">The view.</param>
    /// <returns>The member texts.</returns>
    private static List<string> MemberTexts(CollectionEditorView view) =>
        GridHarness.Get<ListBox>(view, "CollectionEditor_Members").ItemsSource!.Cast<object>()
            .Select(entry => (string)entry.GetType().GetProperty("Text")!.GetValue(entry)!).ToList();

    /// <summary>
    ///     Gets the labels of the tags of a widget.
    /// </summary>
    /// <param name="widget">The widget.</param>
    /// <returns>The labels.</returns>
    private static List<string> Labels(Widget widget) => widget.Tags.Select(t => t.Label).ToList();

    /// <summary>
    ///     Add, Remove and Move change the working list only; Apply writes it to the owner.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task CollectionEditorView_AddRemoveMove_ChangeWorkingListUntilApplied() => UiTest.Run(() =>
    {
        var widget = new Widget();
        var (grid, window) = Create(widget);
        var view = new CollectionEditorView(grid.Session!, grid.Session!.FindItem("Tags")!);
        window.Content = view;
        GridHarness.Pump(window);
        Assert.Equal(["primary", "pinned"], MemberTexts(view));
        Assert.Equal(0, view.SelectedIndex);
        Assert.False(view.HasChanges);

        Assert.True(view.Add().Succeeded);
        Assert.Equal(1, view.SelectedIndex);
        Assert.Equal(["primary", "Tag", "pinned"], MemberTexts(view));
        Assert.True(view.Move(1).Succeeded);
        Assert.Equal(["primary", "pinned", "Tag"], MemberTexts(view));
        Assert.True(view.Move(-1).Succeeded);
        view.SelectedIndex = 0;
        Assert.True(view.Remove().Succeeded);

        Assert.Equal(["Tag", "pinned"], MemberTexts(view));
        Assert.Equal(["primary", "pinned"], Labels(widget));
        Assert.True(view.HasChanges);

        Assert.True(view.Apply().Succeeded);

        Assert.Equal(["", "pinned"], Labels(widget));
        window.Close();
    });

    /// <summary>
    ///     The buttons of the view drive the same operations and enable according to the selection.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task CollectionEditorView_Buttons_EnableByCapabilityAndSelection() => UiTest.Run(() =>
    {
        var (grid, window) = Create(new Widget());
        var view = new CollectionEditorView(grid.Session!, grid.Session!.FindItem("Tags")!);
        window.Content = view;
        GridHarness.Pump(window);
        var up = GridHarness.Get<Button>(view, "CollectionEditor_MoveUp");
        var down = GridHarness.Get<Button>(view, "CollectionEditor_MoveDown");

        Assert.False(up.IsEnabled);
        Assert.True(down.IsEnabled);

        GridHarness.Click(GridHarness.Get<Button>(view, "CollectionEditor_Add"));
        GridHarness.Click(GridHarness.Get<Button>(view, "CollectionEditor_Remove"));

        Assert.Equal(2, view.Items.Count);
        view.SelectedIndex = 1;
        Assert.True(up.IsEnabled);
        Assert.False(down.IsEnabled);
        window.Close();
    });

    /// <summary>
    ///     The nested grid edits the properties of the selected member and the member list shows the new text.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task CollectionEditorView_NestedGrid_EditsSelectedMember() => UiTest.Run(() =>
    {
        var widget = new Widget();
        var (grid, window) = Create(widget);
        var view = new CollectionEditorView(grid.Session!, grid.Session!.FindItem("Tags")!);
        window.Content = view;
        GridHarness.Pump(window);
        view.SelectedIndex = 1;
        GridHarness.Pump(window);

        var label = view.MemberGrid.Session!.FindItem("Label")!;
        Assert.True(view.MemberGrid.Session.SetText(label, "renamed").Succeeded);

        Assert.Equal("renamed", widget.Tags[1].Label);
        Assert.Equal(["primary", "renamed"], MemberTexts(view));
        Assert.Equal("renamed", GridHarness.Inner<TextBox>(GridHarness.Editor(view.MemberGrid, "Label")).Text);
        window.Close();
    });

    /// <summary>
    ///     Value elements (strings) are edited through a single Value row and an element text formatter can be supplied.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task CollectionEditorView_ValueElements_UseValueRowAndFormatter() => UiTest.Run(() =>
    {
        var widget = new Widget();
        var (grid, window) = Create(widget);
        var view = new CollectionEditorView(grid.Session!, grid.Session!.FindItem("Notes")!, value => $"<{value}>");
        window.Content = view;
        GridHarness.Pump(window);
        Assert.Equal(["<first>", "<second>"], MemberTexts(view));
        Assert.Equal(["Value"], GridHarness.PropertyPaths(view.MemberGrid));

        Assert.True(view.MemberGrid.Session!.SetText(view.MemberGrid.Session.FindItem("Value")!, "changed").Succeeded);
        view.Add();

        Assert.Equal(["<changed>", "<>", "<second>"], MemberTexts(view));
        Assert.True(view.Apply().Succeeded);
        Assert.Equal(["changed", string.Empty, "second"], widget.Notes);
        window.Close();
    });

    /// <summary>
    ///     Without a formatter a member is shown by its ToString, an empty string as "(empty)", and objects without text by
    ///     their default property or type name.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task CollectionEditorView_FormatItem_UsesToStringThenFallbacks() => UiTest.Run(() =>
    {
        var (grid, window) = Create(new Widget());
        var view = new CollectionEditorView(grid.Session!, grid.Session!.FindItem("Tags")!);

        Assert.Equal("(null)", view.FormatItem(null));
        Assert.Equal("(empty)", view.FormatItem(string.Empty));
        Assert.Equal("text", view.FormatItem("text"));
        Assert.Equal("Tag", view.FormatItem(new Tag()));
        Assert.Equal("Title text", view.FormatItem(new Document { Title = "Title text" }));
        Assert.Equal("Empty", view.FormatItem(new Empty()));
        window.Close();
    });

    /// <summary>
    ///     A collection that cannot be resized refuses Add and Remove with a message but can be reordered.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task CollectionEditorView_FixedArray_RefusesResizeButAllowsReorder() => UiTest.Run(() =>
    {
        var person = new Person();
        var (grid, window) = Create(person);
        var view = new CollectionEditorView(grid.Session!, grid.Session!.FindItem("Codes")!);
        window.Content = view;
        GridHarness.Pump(window);
        Assert.False(view.CanResize);
        Assert.True(view.CanReorder);

        var added = view.Add();

        Assert.False(added.Succeeded);
        Assert.Contains("cannot be added or removed", GridHarness.Get<TextBlock>(view, "CollectionEditor_Message").Text, StringComparison.Ordinal);
        Assert.False(GridHarness.Get<Button>(view, "CollectionEditor_Add").IsEnabled);

        Assert.True(view.Move(1).Succeeded);
        Assert.True(view.Apply().Succeeded);
        Assert.Equal(["b", "a"], person.Codes);
        window.Close();
    });

    /// <summary>
    ///     Elements without a default constructor fail to add with a message, until the grid supplies a factory.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task CollectionEditorView_Add_UsesCollectionItemFactory() => UiTest.Run(() =>
    {
        var person = new Person();
        var (grid, window) = Create(person);
        var view = new CollectionEditorView(grid.Session!, grid.Session!.FindItem("Badges")!);
        Assert.False(view.Add().Succeeded);

        grid.CollectionItemFactory = _ => new Badge(7);
        var withFactory = new CollectionEditorView(grid.Session, grid.Session.FindItem("Badges")!);

        Assert.True(withFactory.Add().Succeeded);
        Assert.True(withFactory.Apply().Succeeded);
        Assert.Equal(7, person.Badges.Single().Level);
        window.Close();
    });

    /// <summary>
    ///     A view rejects a property that is not a collection.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task CollectionEditorView_NotACollection_Throws() => UiTest.Run(() =>
    {
        var (grid, window) = Create(new Widget());

        Assert.Throws<ArgumentException>(() => new CollectionEditorView(grid.Session!, grid.Session!.FindItem("Caption")!));
        Assert.Throws<ArgumentNullException>(() => new CollectionEditorView(null!, grid.Session!.FindItem("Tags")!));
        window.Close();
    });

    /// <summary>
    ///     OK applies the arranged list into a live session and closes; Cancel leaves the collection untouched.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task CollectionEditorDialog_OkAndCancel_Live() => UiTest.Run(() =>
    {
        var widget = new Widget();
        var (grid, window) = Create(widget);
        var tags = grid.Session!.FindItem("Tags")!;

        var cancelled = new CollectionEditorDialog(grid.Session, tags);
        cancelled.Show();
        cancelled.View.Remove();
        GridHarness.Click(GridHarness.Get<Button>(cancelled, "CollectionEditorDialog_Cancel"));
        GridHarness.Pump(window);

        Assert.Equal(["primary", "pinned"], Labels(widget));

        var accepted = new CollectionEditorDialog(grid.Session, tags);
        accepted.Show();
        accepted.View.Remove();
        GridHarness.Click(GridHarness.Get<Button>(accepted, "CollectionEditorDialog_OK"));
        GridHarness.Pump(window);

        Assert.Equal(["pinned"], Labels(widget));
        Assert.Equal("(Collection) 1 item", GridHarness.Inner<TextBlock>(GridHarness.Editor(grid, "Tags")).Text);
        window.Close();
    });

    /// <summary>
    ///     In a staged session OK records one pending edit that the host commits (or cancels) with the others.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task CollectionEditorDialog_Ok_Staged_RecordsPendingEdit() => UiTest.Run(() =>
    {
        var widget = new Widget();
        var (grid, window) = Create(widget, PropertyEditMode.Staged);
        var dialog = new CollectionEditorDialog(grid.Session!, grid.Session!.FindItem("Tags")!);
        dialog.Show();
        dialog.View.Add();

        Assert.True(dialog.Accept());
        GridHarness.Pump(window);

        Assert.Equal(["primary", "pinned"], Labels(widget));
        Assert.True(grid.Session.HasChanges);
        Assert.Equal("(Collection) 3 items", GridHarness.Inner<TextBlock>(GridHarness.Editor(grid, "Tags")).Text);

        var again = new CollectionEditorDialog(grid.Session, grid.Session.FindItem("Tags")!);
        Assert.Equal(3, again.View.Items.Count);

        Assert.True(grid.Session.Commit().Succeeded);
        Assert.Equal(["primary", string.Empty, "pinned"], Labels(widget));
        window.Close();
    });

    /// <summary>
    ///     The grid's collection button opens the dialog over its session; OK applies and the awaited task completes.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyGrid_CollectionButton_OpensDialogThatApplies() => UiTest.Run(() =>
    {
        var widget = new Widget();
        var (grid, window) = Create(widget);

        var pending = grid.EditCollectionAsync(grid.Session!.FindItem("Tags")!);
        GridHarness.Pump(window);
        var dialog = window.OwnedWindows.OfType<CollectionEditorDialog>().Single();
        Assert.Contains("Tags", dialog.Title, StringComparison.Ordinal);
        dialog.View.Add();
        GridHarness.Click(GridHarness.Get<Button>(dialog, "CollectionEditorDialog_OK"));
        GridHarness.Pump(window);

        Assert.True(pending.IsCompletedSuccessfully);
        Assert.True(pending.Result);
        Assert.Equal(3, widget.Tags.Count);
        window.Close();
    });

    /// <summary>
    ///     Collections of objects that hold collections work recursively: the nested grid of a member offers the
    ///     member's own collection editor.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task CollectionEditorView_NestedCollections_WorkRecursively() => UiTest.Run(() =>
    {
        var catalog = new Catalog();
        var (grid, window) = Create(catalog);
        var outer = new CollectionEditorView(grid.Session!, grid.Session!.FindItem("Entries")!);
        window.Content = outer;
        GridHarness.Pump(window);
        outer.SelectedIndex = 1;
        GridHarness.Pump(window);
        var requested = new List<string>();
        outer.MemberGrid.CollectionEditorRequested += (_, e) =>
        {
            requested.Add(e.Item.Path);
            e.Handled = true;
        };

        GridHarness.Click(GridHarness.Inner<Button>(GridHarness.Editor(outer.MemberGrid, "Tags")));

        Assert.Equal(["Tags"], requested);
        var tagsItem = outer.MemberGrid.Session!.FindItem("Tags")!;
        var inner = new CollectionEditorView(outer.MemberGrid.Session, tagsItem);
        Assert.True(inner.Add().Succeeded);
        inner.MemberGrid.Session!.SetText(inner.MemberGrid.Session.FindItem("Label")!, "deep");
        Assert.True(inner.Apply().Succeeded);

        Assert.Equal(["deep"], catalog.Entries[1].Tags.Select(t => t.Label));
        Assert.Empty(catalog.Entries[0].Tags);
        Assert.Equal("(Collection) 1 item", GridHarness.Inner<TextBlock>(GridHarness.Editor(outer.MemberGrid, "Tags")).Text);

        Assert.True(outer.Add().Succeeded);
        outer.MemberGrid.Session.SetText(outer.MemberGrid.Session.FindItem("Name")!, "gamma");
        Assert.True(outer.Apply().Succeeded);
        Assert.Equal(["alpha", "beta", "gamma"], catalog.Entries.Select(e => e.Name));
        window.Close();
    });

    /// <summary>
    ///     The nested grid of a value-type collection element is hidden behind a Value row that can be expanded.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task CollectionEditorView_StructElements_ExpandInValueRow() => UiTest.Run(() =>
    {
        var holder = new StructHolder();
        var (grid, window) = Create(holder);
        var view = new CollectionEditorView(grid.Session!, grid.Session!.FindItem("Extents")!);
        window.Content = view;
        GridHarness.Pump(window);

        view.MemberGrid.SetExpanded("Value", true);
        GridHarness.Pump(window);
        var set = view.MemberGrid.Session!.SetText(view.MemberGrid.Session.FindItem("Value.Width")!, "33");
        Assert.True(set.Succeeded, set.Error);
        Assert.True(view.Apply().Succeeded);

        Assert.Equal(33, holder.Extents[0].Width);
        Assert.Contains(view.GetVisualDescendants().OfType<PropertyRowView>(), r => r.Item!.Path == "Value.Height");
        window.Close();
    });
}

/// <summary>
///     An object with a collection of value-type elements.
/// </summary>
public sealed class StructHolder
{
    /// <summary>Gets or sets the extents.</summary>
    public List<Extent> Extents { get; set; } = [new Extent { Width = 1, Height = 2 }];
}

using Avalonia;
using Avalonia.Controls;

namespace DemaConsulting.Plexus.Avalonia.Tests;

/// <summary>
///     Tests of how the editor copes with pages: a zoom-to-fit requested before the first layout, following the removal of the shown page, and moving elements to another page.
/// </summary>
public class DiagramEditorPageTests
{
    /// <summary>
    ///     Validates that a ZoomToFit requested before the editor has a size is applied after its first layout.
    /// </summary>
    [Fact]
    public Task DiagramEditor_ZoomToFit_BeforeFirstLayout_AppliesAfterLayout() => UiTest.Run(host =>
    {
        // Arrange: a page with two far-apart nodes shown by an editor that is not in a window yet
        var a = host.Page.AddNode("test.Source", 0, 0);
        var b = host.Page.AddNode("test.Sink", 3000, 2000);
        var editor = new DiagramEditor { Page = host.Page };

        // Act: ask for the fit like a window constructor would, then lay the editor out
        editor.ZoomToFit();
        var zoomBefore = editor.Zoom;
        var window = new Window { Width = 800, Height = 600, Content = editor };
        window.Show();
        window.UpdateLayout();

        // Assert: nothing happened before the layout, and afterwards both nodes are in view
        var area = new Rect(editor.Bounds.Size);
        Assert.Multiple(
            () => Assert.Equal(1.0, zoomBefore),
            () => Assert.True(editor.Zoom < 1),
            () => Assert.True(area.Contains(editor.GetElementScreenBounds(a))),
            () => Assert.True(area.Contains(editor.GetElementScreenBounds(b))));
        window.Close();
    });

    /// <summary>
    ///     Validates that a pending ZoomToFit waits for a page when the editor has a size but shows none, and is applied when the page is set.
    /// </summary>
    [Fact]
    public Task DiagramEditor_ZoomToFit_WithoutPage_AppliesWhenPageIsSet() => UiTest.Run(host =>
    {
        // Arrange: a sized editor with no page, and a page with far-apart nodes
        host.Page.AddNode("test.Source", 0, 0);
        host.Page.AddNode("test.Sink", 3000, 2000);
        var editor = new DiagramEditor();
        var window = new Window { Width = 800, Height = 600, Content = editor };
        window.Show();
        window.UpdateLayout();

        // Act: request the fit with nothing to show, then show the page
        editor.ZoomToFit();
        var zoomBefore = editor.Zoom;
        editor.Page = host.Page;

        // Assert
        Assert.Multiple(
            () => Assert.Equal(1.0, zoomBefore),
            () => Assert.True(editor.Zoom < 1));
        window.Close();
    });

    /// <summary>
    ///     Validates that a request is applied once only: later size changes do not undo the user's zoom.
    /// </summary>
    [Fact]
    public Task DiagramEditor_ZoomToFit_Pending_IsAppliedOnlyOnce() => UiTest.Run(host =>
    {
        // Arrange: a pending fit that is applied by the first layout
        host.Page.AddNode("test.Source", 0, 0);
        host.Page.AddNode("test.Sink", 3000, 2000);
        var editor = new DiagramEditor { Page = host.Page };
        editor.ZoomToFit();
        var window = new Window { Width = 800, Height = 600, Content = editor };
        window.Show();
        window.UpdateLayout();
        var fitted = editor.Zoom;

        // Act: the user zooms, then the window is resized
        editor.Zoom = 0.5;
        window.Width = 900;
        window.UpdateLayout();

        // Assert
        Assert.Multiple(
            () => Assert.True(fitted < 1),
            () => Assert.Equal(0.5, editor.Zoom));
        window.Close();
    });

    /// <summary>
    ///     Validates that removing the shown last page shows the previous page and clears the selection.
    /// </summary>
    [Fact]
    public Task DiagramEditor_PageRemoved_ShownLastPage_ShowsPreviousPageAndClearsSelection() => UiTest.Run(host =>
    {
        // Arrange: two pages, the second shown with a selected node
        var second = host.Diagram.Pages.Add("Second");
        var node = second.AddNode("test.Source", 100, 100);
        host.Editor.Page = second;
        host.Editor.Select(node);

        // Act
        host.Diagram.Pages.Remove(second);
        host.Flush();

        // Assert
        Assert.Multiple(
            () => Assert.Same(host.Page, host.Editor.Page),
            () => Assert.Empty(host.Editor.Selection));
    });

    /// <summary>
    ///     Validates that removing a shown page in the middle shows the page that took its place.
    /// </summary>
    [Fact]
    public Task DiagramEditor_PageRemoved_ShownMiddlePage_ShowsPageAtSameIndex() => UiTest.Run(host =>
    {
        // Arrange: three pages with the middle one shown
        var middle = host.Diagram.Pages.Add("Middle");
        var last = host.Diagram.Pages.Add("Last");
        host.Editor.Page = middle;

        // Act
        host.Diagram.Pages.Remove(middle);

        // Assert: the former third page now sits at the same index
        Assert.Same(last, host.Editor.Page);
    });

    /// <summary>
    ///     Validates that removing a page that is not shown leaves the editor and its selection alone.
    /// </summary>
    [Fact]
    public Task DiagramEditor_PageRemoved_OtherPage_KeepsShownPageAndSelection() => UiTest.Run(host =>
    {
        // Arrange: a selected node on the shown page and a second page
        var node = host.AddSource(100, 100);
        host.Editor.Select(node);
        var other = host.Diagram.Pages.Add("Other");

        // Act
        host.Diagram.Pages.Remove(other);

        // Assert
        Assert.Multiple(
            () => Assert.Same(host.Page, host.Editor.Page),
            () => Assert.Contains(node, host.Editor.Selection));
    });

    /// <summary>
    ///     Validates that removing the only page leaves the editor with no page.
    /// </summary>
    [Fact]
    public Task DiagramEditor_PageRemoved_OnlyPage_ShowsNothing() => UiTest.Run(host =>
    {
        // Arrange / Act
        host.Diagram.Pages.Remove(host.Page);

        // Assert
        Assert.Null(host.Editor.Page);
    });

    /// <summary>
    ///     Validates that undoing "Add page" while the new page is shown follows to the old page, and that redo does not jump back.
    /// </summary>
    [Fact]
    public Task DiagramEditor_UndoAddPage_ShownPage_FollowsAndDoesNotJumpOnRedo() => UiTest.Run(host =>
    {
        // Arrange: add a page and show it
        var added = host.Diagram.Pages.Add("Added");
        host.Editor.Page = added;

        // Act: undo removes the shown page; redo brings it back
        host.Diagram.History.Undo();
        var afterUndo = host.Editor.Page;
        host.Diagram.History.Redo();

        // Assert
        Assert.Same(host.Page, afterUndo);
        Assert.Same(host.Page, host.Editor.Page);
        Assert.Contains(added, host.Diagram.Pages);
    });

    /// <summary>
    ///     Validates that a binding on Page survives the editor switching pages by itself.
    /// </summary>
    [Fact]
    public Task DiagramEditor_PageRemoved_BoundPage_WritesBackThroughTwoWayBinding() => UiTest.Run(host =>
    {
        // Arrange: a two-way binding of Page to a holder object
        var holder = new PageHolder { Current = host.Page };
        var second = host.Diagram.Pages.Add("Second");
        var editor = new DiagramEditor { DataContext = holder };
        editor.Bind(DiagramEditor.PageProperty, new global::Avalonia.Data.Binding(nameof(PageHolder.Current)) { Mode = global::Avalonia.Data.BindingMode.TwoWay });
        host.Window.Content = editor;
        host.Flush();
        holder.Current = second;
        host.Flush();
        var shown = editor.Page;

        // Act
        host.Diagram.Pages.Remove(second);
        host.Flush();

        // Assert: the editor switched and the bound source followed
        Assert.Multiple(
            () => Assert.Same(second, shown),
            () => Assert.Same(host.Page, editor.Page),
            () => Assert.Same(host.Page, holder.Current));
    });

    /// <summary>
    ///     Validates that elements moved to another page leave the selection.
    /// </summary>
    [Fact]
    public Task DiagramEditor_MoveToPage_SelectedElements_LeaveSelection() => UiTest.Run(host =>
    {
        // Arrange: two selected nodes and one that stays selected
        var a = host.AddSource(100, 100);
        var b = host.AddSink(300, 100);
        var c = host.AddSource(100, 300);
        host.Editor.Select([a, b, c]);
        var target = host.Diagram.Pages.Add("Target");

        // Act: move two of them
        host.Diagram.MoveToPage([a, b], target);

        // Assert: only the node that stayed on the shown page is still selected
        Assert.Equal([c], host.Editor.Selection);
    });

    /// <summary>
    ///     A holder with a bindable page, standing in for a view model.
    /// </summary>
    private sealed class PageHolder : System.ComponentModel.INotifyPropertyChanged
    {
        /// <summary>
        ///     The current page backing field.
        /// </summary>
        private DiagramPage? _current;

        /// <inheritdoc />
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        ///     Gets or sets the current page.
        /// </summary>
        public DiagramPage? Current
        {
            get => _current;
            set
            {
                _current = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Current)));
            }
        }
    }
}

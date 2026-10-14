using Avalonia;
using Avalonia.Input;
using DemaConsulting.Plexus.Scene;
using PlexusRect = DemaConsulting.Plexus.Geometry.Rect;

namespace DemaConsulting.Plexus.Avalonia.Tests;

/// <summary>
///     Tests of inline annotation text editing and of the shared <see cref="DiagramEditor.IsEditing"/>,
///     <see cref="DiagramEditor.CommitEdit"/> and <see cref="DiagramEditor.CancelEdit"/> members.
/// </summary>
public class DiagramEditorAnnotationEditTests
{
    /// <summary>
    ///     Adds an annotation at a fixed place.
    /// </summary>
    /// <param name="host">The host.</param>
    /// <param name="text">The annotation text.</param>
    /// <returns>The annotation.</returns>
    private static Annotation AddNote(EditorHost host, string text = "note") =>
        host.Page.AddAnnotation(text, new PlexusRect(150, 150, 150, 60));

    /// <summary>
    ///     Validates that BeginEditAnnotation opens a multi-line text box with the current text, over the annotation.
    /// </summary>
    [Fact]
    public Task DiagramEditor_BeginEditAnnotation_OpensTextBoxOverAnnotation() => UiTest.Run(host =>
    {
        // Arrange
        var note = AddNote(host, "hello");

        // Act
        host.Editor.BeginEditAnnotation(note);
        host.Flush();

        // Assert: editing state, box content and position
        var box = host.Editor.AnnotationEditor!;
        Assert.Multiple(
            () => Assert.True(host.Editor.IsEditingAnnotation),
            () => Assert.True(host.Editor.IsEditing),
            () => Assert.False(host.Editor.IsRenaming),
            () => Assert.Equal("hello", box.Text),
            () => Assert.True(box.AcceptsReturn),
            () => Assert.Equal(DiagramEditor.AnnotationEditorAutomationId, global::Avalonia.Automation.AutomationProperties.GetAutomationId(box)),
            () => Assert.Equal(150, box.Bounds.X, 1),
            () => Assert.Equal(150, box.Bounds.Y, 1),
            () => Assert.Equal(150, box.Bounds.Width, 1));
    });

    /// <summary>
    ///     Validates that Ctrl+Enter commits the text as exactly one undo entry.
    /// </summary>
    [Fact]
    public Task DiagramEditor_AnnotationEdit_CtrlEnter_CommitsWithOneUndoEntry() => UiTest.Run(host =>
    {
        // Arrange
        var note = AddNote(host, "before");
        host.Editor.BeginEditAnnotation(note);
        host.Editor.AnnotationEditor!.Text = "first line\nsecond line";
        var undo = host.Diagram.History.UndoCount;

        // Act
        host.PressFocused(Key.Enter, RawInputModifiers.Control);

        // Assert
        Assert.Multiple(
            () => Assert.Equal("first line\nsecond line", note.Text),
            () => Assert.False(host.Editor.IsEditing),
            () => Assert.Equal(undo + 1, host.Diagram.History.UndoCount),
            () => Assert.Equal("Edit annotation text", host.Diagram.History.UndoDescription));

        // And undo brings the old text back
        host.Diagram.History.Undo();
        Assert.Equal("before", note.Text);
    });

    /// <summary>
    ///     Validates that a plain Enter keeps editing (it is a line break, not a commit).
    /// </summary>
    [Fact]
    public Task DiagramEditor_AnnotationEdit_PlainEnter_DoesNotCommit() => UiTest.Run(host =>
    {
        // Arrange
        var note = AddNote(host, "before");
        host.Editor.BeginEditAnnotation(note);
        host.Editor.AnnotationEditor!.Text = "changed";

        // Act
        host.PressFocused(Key.Enter);

        // Assert: still editing, nothing committed yet
        Assert.Multiple(
            () => Assert.True(host.Editor.IsEditingAnnotation),
            () => Assert.Equal("before", note.Text));
    });

    /// <summary>
    ///     Validates that Escape cancels the edit and records nothing.
    /// </summary>
    [Fact]
    public Task DiagramEditor_AnnotationEdit_Escape_CancelsWithoutChange() => UiTest.Run(host =>
    {
        // Arrange
        var note = AddNote(host, "keep");
        host.Editor.BeginEditAnnotation(note);
        host.Editor.AnnotationEditor!.Text = "discarded";
        var undo = host.Diagram.History.UndoCount;

        // Act
        host.PressFocused(Key.Escape);

        // Assert
        Assert.Multiple(
            () => Assert.False(host.Editor.IsEditing),
            () => Assert.Equal("keep", note.Text),
            () => Assert.Equal(undo, host.Diagram.History.UndoCount));
    });

    /// <summary>
    ///     Validates that moving focus away commits the edit.
    /// </summary>
    [Fact]
    public Task DiagramEditor_AnnotationEdit_FocusLoss_Commits() => UiTest.Run(host =>
    {
        // Arrange
        var note = AddNote(host, "before");
        host.Editor.BeginEditAnnotation(note);
        host.Editor.AnnotationEditor!.Text = "after";
        var undo = host.Diagram.History.UndoCount;

        // Act: focus moves back to the canvas
        host.Editor.Focus();
        host.Flush();

        // Assert
        Assert.Multiple(
            () => Assert.False(host.Editor.IsEditing),
            () => Assert.Equal("after", note.Text),
            () => Assert.Equal(undo + 1, host.Diagram.History.UndoCount));
    });

    /// <summary>
    ///     Validates that clicking the empty canvas commits the edit.
    /// </summary>
    [Fact]
    public Task DiagramEditor_AnnotationEdit_ClickOnCanvas_Commits() => UiTest.Run(host =>
    {
        // Arrange
        var note = AddNote(host, "before");
        host.Editor.BeginEditAnnotation(note);
        host.Editor.AnnotationEditor!.Text = "after";

        // Act: click far away from the annotation
        host.Click(new Point(600, 500));

        // Assert
        Assert.Multiple(
            () => Assert.False(host.Editor.IsEditing),
            () => Assert.Equal("after", note.Text));
    });

    /// <summary>
    ///     Validates that committing an unchanged text records no undo entry, even when line breaks differ in form.
    /// </summary>
    [Fact]
    public Task DiagramEditor_AnnotationEdit_UnchangedText_RecordsNothing() => UiTest.Run(host =>
    {
        // Arrange: a two-line annotation, edited with CRLF line breaks that mean the same text
        var note = AddNote(host, "one\ntwo");
        host.Editor.BeginEditAnnotation(note);
        host.Editor.AnnotationEditor!.Text = "one\r\ntwo";
        var undo = host.Diagram.History.UndoCount;

        // Act
        var committed = host.Editor.CommitEdit();

        // Assert
        Assert.Multiple(
            () => Assert.True(committed),
            () => Assert.False(host.Editor.IsEditing),
            () => Assert.Equal("one\ntwo", note.Text),
            () => Assert.Equal(undo, host.Diagram.History.UndoCount));
    });

    /// <summary>
    ///     Validates that committed text always uses line feeds.
    /// </summary>
    [Fact]
    public Task DiagramEditor_AnnotationEdit_Commit_NormalizesLineBreaks() => UiTest.Run(host =>
    {
        // Arrange
        var note = AddNote(host, "x");
        host.Editor.BeginEditAnnotation(note);
        host.Editor.AnnotationEditor!.Text = "a\r\nb\rc";

        // Act
        host.Editor.CommitEdit();

        // Assert
        Assert.Equal("a\nb\nc", note.Text);
    });

    /// <summary>
    ///     Validates that double-clicking an annotation starts editing it.
    /// </summary>
    [Fact]
    public Task DiagramEditor_DoubleClick_Annotation_BeginsEdit() => UiTest.Run(host =>
    {
        // Arrange
        var note = AddNote(host, "double");

        // Act: double-click inside the annotation, away from its resize handle
        host.DoubleClick(new Point(180, 170));

        // Assert
        Assert.Multiple(
            () => Assert.True(host.Editor.IsEditingAnnotation),
            () => Assert.Equal("double", host.Editor.AnnotationEditor!.Text),
            () => Assert.Contains(note, host.Editor.Selection));
    });

    /// <summary>
    ///     Validates that a double-click on a read-only editor does not start editing.
    /// </summary>
    [Fact]
    public Task DiagramEditor_DoubleClick_AnnotationInReadOnlyEditor_DoesNothing() => UiTest.Run(host =>
    {
        // Arrange
        AddNote(host);
        host.Editor.IsReadOnly = true;

        // Act
        host.DoubleClick(new Point(180, 170));

        // Assert
        Assert.False(host.Editor.IsEditing);
    });

    /// <summary>
    ///     Validates that F2 with a single selected annotation starts editing it.
    /// </summary>
    [Fact]
    public Task DiagramEditor_F2_SelectedAnnotation_BeginsEdit() => UiTest.Run(host =>
    {
        // Arrange
        var note = AddNote(host);
        host.Editor.Select(note);

        // Act
        host.Press(Key.F2);

        // Assert
        Assert.True(host.Editor.IsEditingAnnotation);
    });

    /// <summary>
    ///     Validates that F2 does nothing for several selected elements.
    /// </summary>
    [Fact]
    public Task DiagramEditor_F2_MultipleSelected_DoesNothing() => UiTest.Run(host =>
    {
        // Arrange
        var note = AddNote(host);
        var node = host.AddSource(400, 300);
        host.Editor.Select([note, node]);

        // Act
        host.Press(Key.F2);

        // Assert
        Assert.False(host.Editor.IsEditing);
    });

    /// <summary>
    ///     Validates that a read-only editor refuses to start an annotation edit.
    /// </summary>
    [Fact]
    public Task DiagramEditor_BeginEditAnnotation_ReadOnlyEditor_DoesNothing() => UiTest.Run(host =>
    {
        // Arrange
        var note = AddNote(host);
        host.Editor.IsReadOnly = true;

        // Act
        host.Editor.BeginEditAnnotation(note);

        // Assert
        Assert.False(host.Editor.IsEditing);
    });

    /// <summary>
    ///     Validates that making the editor read-only abandons an edit in progress.
    /// </summary>
    [Fact]
    public Task DiagramEditor_AnnotationEdit_EditorBecomesReadOnly_Cancels() => UiTest.Run(host =>
    {
        // Arrange
        var note = AddNote(host, "keep");
        host.Editor.BeginEditAnnotation(note);
        host.Editor.AnnotationEditor!.Text = "lost";

        // Act
        host.Editor.IsReadOnly = true;
        host.Flush();

        // Assert
        Assert.Multiple(
            () => Assert.False(host.Editor.IsEditing),
            () => Assert.Equal("keep", note.Text));
    });

    /// <summary>
    ///     Validates that removing the annotation from the model ends the edit.
    /// </summary>
    [Fact]
    public Task DiagramEditor_AnnotationEdit_AnnotationRemoved_Cancels() => UiTest.Run(host =>
    {
        // Arrange
        var note = AddNote(host);
        host.Editor.BeginEditAnnotation(note);

        // Act
        host.Page.Remove(note);
        host.Flush();

        // Assert
        Assert.False(host.Editor.IsEditing);
    });

    /// <summary>
    ///     Validates that starting a rename replaces an annotation edit (only one inline edit at a time).
    /// </summary>
    [Fact]
    public Task DiagramEditor_BeginRename_WhileEditingAnnotation_ReplacesTheEdit() => UiTest.Run(host =>
    {
        // Arrange
        var note = AddNote(host);
        var node = host.AddSource(400, 300, "Src");
        host.Editor.BeginEditAnnotation(note);

        // Act
        host.Editor.BeginRename(node);

        // Assert
        Assert.Multiple(
            () => Assert.True(host.Editor.IsRenaming),
            () => Assert.False(host.Editor.IsEditingAnnotation));
    });

    /// <summary>
    ///     Validates that CommitEdit commits a rename and CancelEdit abandons one, so hosts need not know which edit is open.
    /// </summary>
    [Fact]
    public Task DiagramEditor_CommitEditAndCancelEdit_WorkForRename() => UiTest.Run(host =>
    {
        // Arrange
        var node = host.AddSource(150, 150, "Old");
        host.Editor.BeginRename(node);
        host.Editor.RenameEditor!.Text = "Fresh";

        // Act: commit
        var committed = host.Editor.CommitEdit();
        host.Editor.BeginRename(node);
        host.Editor.RenameEditor.Text = "Ignored";
        host.Editor.CancelEdit();

        // Assert
        Assert.Multiple(
            () => Assert.True(committed),
            () => Assert.Equal("Fresh", node.Name),
            () => Assert.False(host.Editor.IsEditing));
    });

    /// <summary>
    ///     Validates that CommitEdit reports a rejected rename and keeps the edit open.
    /// </summary>
    [Fact]
    public Task DiagramEditor_CommitEdit_RejectedRename_ReturnsFalse() => UiTest.Run(host =>
    {
        // Arrange: renaming to a name that is taken
        var node = host.AddSource(150, 150, "First");
        host.AddSource(350, 150, "Second");
        host.Editor.BeginRename(node);
        host.Editor.RenameEditor!.Text = "Second";

        // Act
        var committed = host.Editor.CommitEdit();

        // Assert
        Assert.Multiple(
            () => Assert.False(committed),
            () => Assert.True(host.Editor.IsRenaming));
    });

    /// <summary>
    ///     Validates that CommitEdit and CancelEdit are harmless when nothing is being edited, and do not steal focus.
    /// </summary>
    [Fact]
    public Task DiagramEditor_CommitEditAndCancelEdit_NothingOpen_AreNoOps() => UiTest.Run(host =>
    {
        // Arrange / Act
        var committed = host.Editor.CommitEdit();
        host.Editor.CancelEdit();

        // Assert
        Assert.Multiple(
            () => Assert.True(committed),
            () => Assert.False(host.Editor.IsEditing));
    });

    /// <summary>
    ///     Validates that the annotation edit box follows the annotation when the view is panned and zoomed.
    /// </summary>
    [Fact]
    public Task DiagramEditor_AnnotationEdit_ViewChanges_BoxFollowsAnnotation() => UiTest.Run(host =>
    {
        // Arrange
        var note = AddNote(host);
        host.Editor.BeginEditAnnotation(note);
        host.Flush();

        // Act
        host.Editor.Zoom = 2;
        host.Editor.Offset = new Point(50, 50);
        host.Flush();

        // Assert: the box sits at the screen position of the annotation corner and scales with the zoom
        var box = host.Editor.AnnotationEditor!;
        var expected = host.Editor.DiagramToScreen(new Point(150, 150));
        Assert.Multiple(
            () => Assert.Equal(expected.X, box.Bounds.X, 1),
            () => Assert.Equal(expected.Y, box.Bounds.Y, 1),
            () => Assert.Equal(300, box.Bounds.Width, 1));
    });

    /// <summary>
    ///     Validates that null arguments are rejected.
    /// </summary>
    [Fact]
    public Task DiagramEditor_BeginEditAnnotation_Null_Throws() => UiTest.Run(host =>
        Assert.Throws<ArgumentNullException>(() => host.Editor.BeginEditAnnotation(null!)));
}

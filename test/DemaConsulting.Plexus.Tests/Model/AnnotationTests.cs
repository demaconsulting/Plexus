using DemaConsulting.Plexus.Geometry;

namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     Unit tests for the Annotation unit: text, fill and bounds.
/// </summary>
public class AnnotationTests
{
    /// <summary>
    ///     Creates a diagram with one page holding an annotation.
    /// </summary>
    /// <returns>The diagram, page and annotation.</returns>
    private static (Diagram Diagram, DiagramPage Page, Annotation Note) Create()
    {
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        return (diagram, page, page.AddAnnotation("note", new Rect(10, 20, 100, 40)));
    }

    /// <summary>
    ///     Validates the initial state of a new annotation: its text and bounds and no fill override.
    /// </summary>
    [Fact]
    public void Annotation_Initial_NewAnnotation_HasTextBoundsAndNoFill()
    {
        // Arrange / Act
        var (_, _, note) = Create();

        // Assert
        Assert.Equal("note", note.Text);
        Assert.Equal(new Rect(10, 20, 100, 40), note.Bounds);
        Assert.Null(note.Fill);
        Assert.Null(note.Parent);
    }

    /// <summary>
    ///     Validates that assigning the current text records nothing.
    /// </summary>
    [Fact]
    public void Annotation_Text_SameValue_RecordsNothing()
    {
        // Arrange
        var (diagram, _, note) = Create();
        var entries = diagram.History.UndoCount;

        // Act
        note.Text = "note";

        // Assert
        Assert.Equal(entries, diagram.History.UndoCount);
    }

    /// <summary>
    ///     Validates that an annotation can carry an empty text.
    /// </summary>
    [Fact]
    public void Annotation_Text_EmptyString_IsAccepted()
    {
        // Arrange
        var (diagram, _, note) = Create();

        // Act / Assert
        TestModel.AssertRoundTrip(diagram, () => note.Text = string.Empty);
        Assert.Equal(string.Empty, note.Text);
    }

    /// <summary>
    ///     Validates that assigning the current fill records nothing.
    /// </summary>
    [Fact]
    public void Annotation_Fill_SameValue_RecordsNothing()
    {
        // Arrange
        var (diagram, _, note) = Create();
        var entries = diagram.History.UndoCount;

        // Act
        note.Fill = null;
        var afterNull = diagram.History.UndoCount;
        note.Fill = Color.Red;
        var afterChange = diagram.History.UndoCount;
        note.Fill = Color.Red;

        // Assert
        Assert.Equal(entries, afterNull);
        Assert.Equal(entries + 1, afterChange);
        Assert.Equal(afterChange, diagram.History.UndoCount);
    }

    /// <summary>
    ///     Validates that setting identical bounds records nothing and non-finite or negative bounds are rejected.
    /// </summary>
    [Fact]
    public void Annotation_Bounds_SameOrInvalid_NoEntryOrThrows()
    {
        // Arrange
        var (diagram, _, note) = Create();
        var entries = diagram.History.UndoCount;

        // Act
        note.Bounds = new Rect(10, 20, 100, 40);

        // Assert
        Assert.Equal(entries, diagram.History.UndoCount);
        Assert.Throws<ArgumentException>(() => note.Bounds = new Rect(0, 0, -5, 10));
        Assert.Throws<ArgumentException>(() => note.Bounds = new Rect(double.NaN, 0, 10, 10));
        Assert.Equal(new Rect(10, 20, 100, 40), note.Bounds);
    }

    /// <summary>
    ///     Validates that a pure move is described as a move and a size change as a resize, and GetBounds reports the box.
    /// </summary>
    [Fact]
    public void Annotation_Bounds_MoveOrResize_DescribedAndReported()
    {
        // Arrange
        var (diagram, _, note) = Create();

        // Act
        note.Bounds = new Rect(50, 60, 100, 40);
        var moveDescription = diagram.History.UndoDescription;
        note.Bounds = new Rect(50, 60, 150, 80);

        // Assert
        Assert.Equal("Move", moveDescription);
        Assert.Equal("Resize", diagram.History.UndoDescription);
        Assert.Equal(new Rect(50, 60, 150, 80), note.GetBounds());
    }

    /// <summary>
    ///     Validates that every setter rejects a removed annotation.
    /// </summary>
    [Fact]
    public void Annotation_Setters_RemovedAnnotation_ThrowInvalidOperationException()
    {
        // Arrange
        var (_, _, note) = Create();
        note.Remove();

        // Act / Assert
        Assert.Throws<InvalidOperationException>(() => note.Text = "x");
        Assert.Throws<InvalidOperationException>(() => note.Fill = Color.Red);
        Assert.Throws<InvalidOperationException>(() => note.Bounds = new Rect(0, 0, 10, 10));
    }

    /// <summary>
    ///     Validates that text, fill and bounds changes raise PropertyChanged, also for undo and redo.
    /// </summary>
    [Fact]
    public void Annotation_PropertyChanged_TextFillAndBounds_RaisedForSetUndoAndRedo()
    {
        // Arrange
        var (diagram, _, note) = Create();
        var raised = new List<string?>();
        note.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        // Act
        note.Text = "changed";
        note.Fill = Color.Red;
        note.Bounds = new Rect(10, 20, 200, 40);

        // Assert
        Assert.Equal([nameof(Annotation.Text), nameof(Annotation.Fill), nameof(Annotation.Bounds)], raised);

        // Act
        raised.Clear();
        diagram.History.Undo();
        diagram.History.Redo();

        // Assert
        Assert.Equal([nameof(Annotation.Bounds), nameof(Annotation.Bounds)], raised);
    }
}

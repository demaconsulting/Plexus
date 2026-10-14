namespace DemaConsulting.Plexus.Tests.Serialization;

/// <summary>
///     Unit tests for <see cref="PasteResult.Message"/>.
/// </summary>
public class PasteResultMessageTests
{
    /// <summary>
    ///     Validates that a successful paste summarizes what was pasted.
    /// </summary>
    [Fact]
    public void PasteResult_Message_Success_SummarizesElements()
    {
        // Arrange
        var source = SerializationFixtures.CreateRichDiagram();
        var json = PlexusClipboard.Copy(source.Pages[1].Elements);
        var target = SerializationFixtures.CreateDiagram();
        var page = target.Pages.Add("Main");

        // Act
        var result = PlexusClipboard.Paste(page, json);

        // Assert
        Assert.True(result.Success);
        Assert.Equal($"Pasted {result.Elements.Count} element(s).", result.Message);
    }

    /// <summary>
    ///     Validates that warnings of a successful paste are part of the message.
    /// </summary>
    [Fact]
    public void PasteResult_Message_SuccessWithWarnings_IncludesThem()
    {
        // Arrange: the second wire into a strict input is skipped with a warning
        var source = new Diagram(SerializationFixtures.CreateTypesWithMerger(allowsFanIn: true));
        var sourcePage = source.Pages.Add("Main");
        var first = sourcePage.AddNode("acme.Producer", 0, 0, "A");
        var second = sourcePage.AddNode("acme.Producer", 0, 150, "B");
        var merger = sourcePage.AddNode("acme.Merger", 300, 60, "M");
        sourcePage.Connect(first.Port("value"), merger.Port("in"));
        sourcePage.Connect(second.Port("value"), merger.Port("in"));
        var json = PlexusClipboard.Copy([first, second, merger]);
        var targetPage = new Diagram(SerializationFixtures.CreateTypesWithMerger(allowsFanIn: false)).Pages.Add("Main");

        // Act
        var result = PlexusClipboard.Paste(targetPage, json);

        // Assert
        Assert.StartsWith("Pasted ", result.Message, StringComparison.Ordinal);
        Assert.Contains("Warnings: ", result.Message, StringComparison.Ordinal);
        Assert.Contains(Assert.Single(result.Warnings), result.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a failure caused by unknown node types names every missing type.
    /// </summary>
    [Fact]
    public void PasteResult_Message_MissingNodeTypes_NamesThem()
    {
        // Arrange
        var source = SerializationFixtures.CreateRichDiagram();
        var json = PlexusClipboard.Copy(source.Pages[0].Elements);
        var page = TestModel.CreateDiagram().Pages.Add("Main");

        // Act
        var result = PlexusClipboard.Paste(page, json);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Paste failed: this application does not know the node type(s) acme.Sensor, acme.Custom.", result.Message);
    }

    /// <summary>
    ///     Validates that any other failure reads "Paste failed:" followed by the reason, also for an empty clipboard.
    /// </summary>
    [Fact]
    public void PasteResult_Message_OtherFailure_UsesTheError()
    {
        // Arrange
        var page = TestModel.CreateDiagram().Pages.Add("Main");

        // Act
        var result = PlexusClipboard.Paste(page, "definitely not plexus data");
        var empty = PlexusClipboard.Paste(page, string.Empty);

        // Assert
        Assert.False(result.Success);
        Assert.Equal($"Paste failed: {result.Error}", result.Message);
        Assert.Equal($"Paste failed: {empty.Error}", empty.Message);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }
}

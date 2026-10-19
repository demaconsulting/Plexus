using System.Text;
using System.Text.Json;

namespace DemaConsulting.Plexus.Tests.Serialization;

/// <summary>
///     Unit tests for the stream overloads <see cref="Diagram.ToJson(Stream, bool)"/> and <see cref="Diagram.FromJson(Stream, NodeTypeRegistry, DiagramOptions?)"/>.
/// </summary>
public class DiagramStreamTests
{
    /// <summary>
    ///     A stream that records whether it was disposed.
    /// </summary>
    private sealed class TrackingStream : MemoryStream
    {
        /// <summary>
        ///     Gets a value indicating whether the stream was closed.
        /// </summary>
        public bool WasClosed { get; private set; }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            WasClosed = true;
            base.Dispose(disposing);
        }
    }

    /// <summary>
    ///     Validates that a diagram written to a stream reads back identically and that both calls leave the stream open.
    /// </summary>
    [Fact]
    public void Diagram_ToJsonFromJson_Stream_RoundTripsAndLeavesStreamOpen()
    {
        // Arrange
        var source = SerializationFixtures.CreateRichDiagram();
        using var stream = new TrackingStream();

        // Act
        source.ToJson(stream);
        stream.Position = 0;
        var loaded = Diagram.FromJson(stream, SerializationFixtures.CreateTypes());

        // Assert
        Assert.False(stream.WasClosed);
        Assert.Equal(TestModel.Dump(source), TestModel.Dump(loaded));
    }

    /// <summary>
    ///     Validates that the stream holds the same UTF-8 text as the string form, without a byte-order mark, and honors the indent flag.
    /// </summary>
    [Fact]
    public void Diagram_ToJson_Stream_WritesTheSameUtf8TextAsTheStringForm()
    {
        // Arrange
        var diagram = SerializationFixtures.CreateRichDiagram();
        using var indented = new MemoryStream();
        using var compact = new MemoryStream();

        // Act
        diagram.ToJson(indented);
        diagram.ToJson(compact, indented: false);

        // Assert
        Assert.Equal(diagram.ToJson(), Encoding.UTF8.GetString(indented.ToArray()));
        Assert.Equal(diagram.ToJson(indented: false), Encoding.UTF8.GetString(compact.ToArray()));
        Assert.NotEqual(0xEF, indented.ToArray()[0]);
    }

    /// <summary>
    ///     Validates that a UTF-8 byte-order mark at the start of the stream is accepted.
    /// </summary>
    [Fact]
    public void Diagram_FromJson_Stream_AcceptsByteOrderMark()
    {
        // Arrange
        var source = SerializationFixtures.CreateRichDiagram();
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetPreamble().Concat(Encoding.UTF8.GetBytes(source.ToJson())).ToArray();
        using var stream = new MemoryStream(bytes);

        // Act
        var loaded = Diagram.FromJson(stream, SerializationFixtures.CreateTypes());

        // Assert
        Assert.Equal(TestModel.Dump(source), TestModel.Dump(loaded));
    }

    /// <summary>
    ///     Validates the error behavior of the stream overloads: nulls, malformed content and unknown node types.
    /// </summary>
    [Fact]
    public void Diagram_FromJson_Stream_InvalidInput_Throws()
    {
        // Arrange
        var json = SerializationFixtures.CreateRichDiagram().ToJson();

        using var empty = new MemoryStream();
        using var notJson = new MemoryStream(Encoding.UTF8.GetBytes("{ not json"));
        using var richJson = new MemoryStream(Encoding.UTF8.GetBytes(json));
        using var badVersion = new MemoryStream(Encoding.UTF8.GetBytes("{\"formatVersion\":99,\"pages\":[]}"));
        using var nullJson = new MemoryStream(Encoding.UTF8.GetBytes("null"));

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => Diagram.FromJson((Stream)null!, TestModel.CreateTypes()));
        Assert.Throws<ArgumentNullException>(() => Diagram.FromJson(empty, null!));
        Assert.Throws<ArgumentNullException>(() => TestModel.CreateDiagram().ToJson((Stream)null!));
        Assert.ThrowsAny<JsonException>(() => Diagram.FromJson(notJson, TestModel.CreateTypes()));
        Assert.Throws<SnapshotException>(() => Diagram.FromJson(richJson, TestModel.CreateTypes()));
        Assert.Throws<SnapshotException>(() => Diagram.FromJson(badVersion, TestModel.CreateTypes()));
        Assert.ThrowsAny<JsonException>(() => Diagram.FromJson(nullJson, TestModel.CreateTypes()));
    }
}

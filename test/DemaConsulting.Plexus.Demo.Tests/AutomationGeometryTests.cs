using System.IO.Pipes;
using System.Text;
using DemaConsulting.Plexus.Demo.Gui.Automation;

namespace DemaConsulting.Plexus.Demo.Tests;

/// <summary>
///     Tests for the automation <c>geometry</c> and <c>view</c> commands, which let an external driver aim real mouse input.
/// </summary>
public class AutomationGeometryTests
{
    /// <summary>
    ///     Reads a double from a JSON path.
    /// </summary>
    /// <param name="node">The node holding the value.</param>
    /// <returns>The number.</returns>
    private static double Number(System.Text.Json.Nodes.JsonNode? node) => node!.GetValue<double>();

    /// <summary>
    ///     Validates that a node's geometry gives diagram and screen coordinates that agree with the view's zoom and the monitor scale.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task AutomationGeometry_Node_ScreenSizeFollowsZoomAndScale() => GuiTest.RunAsync(async host =>
    {
        // Arrange
        await host.OkAsync("""{"cmd":"add-node","type":"demo.audio.Filter","x":150,"y":150,"name":"S"}""");
        await host.OkAsync("""{"cmd":"view","zoom":2,"offsetX":0,"offsetY":0}""");

        // Act
        var geometry = await host.OkAsync("""{"cmd":"geometry","node":"S"}""");
        var editor = await host.OkAsync("""{"cmd":"geometry","editor":true}""");

        // Assert: the box is 90x105 units at zoom 2 and the headless monitor scale
        var scale = Number(geometry["scale"]);
        var bounds = geometry["bounds"]!;
        Assert.Equal(90, Number(bounds["diagram"]!["width"]));
        Assert.Equal(90 * 2 * scale, Number(bounds["screen"]!["width"]), 3);
        Assert.Equal(105 * 2 * scale, Number(bounds["screen"]!["height"]), 3);

        // Assert: the screen position is the editor's origin plus the scaled editor-relative position
        var originX = Number(editor["bounds"]!["screen"]!["x"]);
        var originY = Number(editor["bounds"]!["screen"]!["y"]);
        Assert.Equal(originX + (150 * 2 * scale), Number(bounds["screen"]!["x"]), 1);
        Assert.Equal(originY + (150 * 2 * scale), Number(bounds["screen"]!["y"]), 1);
        Assert.Equal(
            Number(bounds["screen"]!["x"]) + (Number(bounds["screen"]!["width"]) / 2),
            Number(bounds["screen"]!["centerX"]),
            1);
        Assert.True(bounds["visible"]!.GetValue<bool>());
        Assert.Equal(2, geometry["ports"]!.AsArray().Count);
    });

    /// <summary>
    ///     Validates that the name rectangle sits above the box and port press points lie inside their hit areas.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task AutomationGeometry_Node_NameAboveBoxAndPortPressInsideHitArea() => GuiTest.RunAsync(async host =>
    {
        // Arrange
        await host.OkAsync("""{"cmd":"add-node","type":"demo.audio.Mixer","x":150,"y":150,"name":"E"}""");

        // Act
        var geometry = await host.OkAsync("""{"cmd":"geometry","node":"E"}""");

        // Assert
        Assert.True(Number(geometry["nameRect"]!["screen"]!["centerY"]) < Number(geometry["bounds"]!["screen"]!["y"]));
        foreach (var port in geometry["ports"]!.AsArray())
        {
            var hit = port!["hit"]!["screen"]!;
            var press = port["press"]!["screen"]!;
            Assert.InRange(Number(press["x"]), Number(hit["x"]), Number(hit["x"]) + Number(hit["width"]));
            Assert.InRange(Number(press["y"]), Number(hit["y"]), Number(hit["y"]) + Number(hit["height"]));
        }
    });

    /// <summary>
    ///     Validates that panning moves screen coordinates by the offset while diagram coordinates stay put.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task AutomationGeometry_View_OffsetMovesScreenNotDiagram() => GuiTest.RunAsync(async host =>
    {
        // Arrange
        await host.OkAsync("""{"cmd":"add-node","type":"demo.audio.Filter","x":150,"y":150,"name":"S"}""");
        await host.OkAsync("""{"cmd":"view","zoom":1,"offsetX":0,"offsetY":0}""");
        var before = await host.OkAsync("""{"cmd":"geometry","node":"S"}""");

        // Act
        var view = await host.OkAsync("""{"cmd":"view","offsetX":30,"offsetY":45}""");
        var after = await host.OkAsync("""{"cmd":"geometry","node":"S"}""");

        // Assert
        var scale = Number(after["scale"]);
        Assert.Equal(30, Number(view["offsetX"]));
        Assert.Equal(Number(before["bounds"]!["diagram"]!["x"]), Number(after["bounds"]!["diagram"]!["x"]));
        Assert.Equal(Number(before["bounds"]!["screen"]!["x"]) - (30 * scale), Number(after["bounds"]!["screen"]!["x"]), 1);
        Assert.Equal(Number(before["bounds"]!["screen"]!["y"]) - (45 * scale), Number(after["bounds"]!["screen"]!["y"]), 1);
    });

    /// <summary>
    ///     Validates that an arbitrary diagram point maps to screen pixels consistently with the editor origin, zoom and scale.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task AutomationGeometry_Point_MapsDiagramPointToScreen() => GuiTest.RunAsync(async host =>
    {
        // Arrange
        await host.OkAsync("""{"cmd":"view","zoom":2,"offsetX":0,"offsetY":0}""");

        // Act
        var geometry = await host.OkAsync("""{"cmd":"geometry","point":{"x":100,"y":50}}""");
        var editor = await host.OkAsync("""{"cmd":"geometry","editor":true}""");

        // Assert
        var scale = Number(geometry["scale"]);
        var point = geometry["point"]!;
        Assert.Multiple(
            () => Assert.Equal("point", geometry["kind"]!.GetValue<string>()),
            () => Assert.Equal(100, Number(point["diagram"]!["x"])),
            () => Assert.Equal(Number(editor["bounds"]!["screen"]!["x"]) + (100 * 2 * scale), Number(point["screen"]!["x"]), 1),
            () => Assert.Equal(Number(editor["bounds"]!["screen"]!["y"]) + (50 * 2 * scale), Number(point["screen"]!["y"]), 1));
    });

    /// <summary>
    ///     Validates that a node scrolled out of view is reported as not visible, so a driver knows to pan first.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task AutomationGeometry_OffscreenNode_IsNotVisible() => GuiTest.RunAsync(async host =>
    {
        // Arrange
        await host.OkAsync("""{"cmd":"add-node","type":"demo.audio.Filter","x":5000,"y":5000,"name":"Far"}""");
        await host.OkAsync("""{"cmd":"view","zoom":1,"offsetX":0,"offsetY":0}""");

        // Act
        var geometry = await host.OkAsync("""{"cmd":"geometry","node":"Far"}""");

        // Assert
        Assert.False(geometry["bounds"]!["visible"]!.GetValue<bool>());
    });

    /// <summary>
    ///     Validates that fit brings a far-away node into view.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task AutomationGeometry_View_FitMakesContentVisible() => GuiTest.RunAsync(async host =>
    {
        // Arrange
        await host.OkAsync("""{"cmd":"add-node","type":"demo.audio.Filter","x":0,"y":0,"name":"Near"}""");
        await host.OkAsync("""{"cmd":"add-node","type":"demo.audio.Filter","x":3000,"y":2000,"name":"Far"}""");

        // Act
        await host.OkAsync("""{"cmd":"view","fit":true}""");
        var far = await host.OkAsync("""{"cmd":"geometry","node":"Far"}""");
        var near = await host.OkAsync("""{"cmd":"geometry","node":"Near"}""");

        // Assert
        Assert.True(far["bounds"]!["visible"]!.GetValue<bool>());
        Assert.True(near["bounds"]!["visible"]!.GetValue<bool>());
    });

    /// <summary>
    ///     Validates the geometry of wires, groups, annotations and ports, and the argument errors.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task AutomationGeometry_OtherElements_DescribeTheirHandles() => GuiTest.RunAsync(async host =>
    {
        // Arrange
        await host.OkAsync("""{"cmd":"add-node","type":"demo.math.Constant","x":0,"y":30,"name":"Src"}""");
        await host.OkAsync("""{"cmd":"add-node","type":"demo.math.Scope","x":300,"y":30,"name":"Plot"}""");
        var wire = await host.OkAsync("""{"cmd":"connect","from":"Src.value","to":"Plot.signal"}""");
        var group = await host.OkAsync("""{"cmd":"group","names":["Src","Plot"],"title":"All"}""");
        host.Session.RequirePage().AddAnnotation("Note", new Geometry.Rect(0, 300, 150, 60));
        var annotation = host.Session.RequirePage().Annotations.Single();
        await host.OkAsync("""{"cmd":"view","fit":true}""");

        // Act
        var wireGeometry = await host.OkAsync($$"""{"cmd":"geometry","element":"{{wire["id"]}}"}""");
        var groupGeometry = await host.OkAsync($$"""{"cmd":"geometry","element":"{{group["id"]}}"}""");
        var annotationGeometry = await host.OkAsync($$"""{"cmd":"geometry","element":"{{annotation.Id}}"}""");
        var portGeometry = await host.OkAsync("""{"cmd":"geometry","port":"Src.value"}""");

        // Assert
        Assert.True(wireGeometry["points"]!.AsArray().Count >= 2);
        Assert.Equal(wireGeometry["points"]!.AsArray().Count - 1, wireGeometry["segmentMidpoints"]!.AsArray().Count);
        Assert.NotNull(groupGeometry["header"]);
        Assert.NotNull(groupGeometry["toggle"]);
        Assert.NotNull(annotationGeometry["resizeHandle"]);
        Assert.Equal("value", portGeometry["port"]!["name"]!.GetValue<string>());
        Assert.Contains("'node'", await host.ErrorAsync("""{"cmd":"geometry"}"""), StringComparison.Ordinal);
    });

    /// <summary>
    ///     Validates that the group toggle rectangle computed by the geometry command really is the editor's collapse toggle.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task AutomationGeometry_GroupToggle_IsWhereTheEditorHitTestsIt() => GuiTest.RunAsync(async host =>
    {
        // Arrange
        await host.OkAsync("""{"cmd":"add-node","type":"demo.audio.Filter","x":60,"y":60,"name":"A"}""");
        var group = await host.OkAsync("""{"cmd":"group","names":["A"],"title":"G"}""");
        await host.OkAsync("""{"cmd":"view","zoom":1,"offsetX":0,"offsetY":0}""");

        // Act
        var geometry = await host.OkAsync($$"""{"cmd":"geometry","element":"{{group["id"]}}"}""");
        var toggle = geometry["toggle"]!["diagram"]!;
        var center = new global::Avalonia.Point(Number(toggle["x"]) + (Number(toggle["width"]) / 2), Number(toggle["y"]) + (Number(toggle["height"]) / 2));
        var hit = host.Editor.HitTest(center);

        // Assert
        Assert.Equal(Scene.HitKind.GroupCollapseToggle, hit.Kind);
    });
}

/// <summary>
///     Tests for <see cref="AutomationServer"/>, the named-pipe transport, without any UI involved.
/// </summary>
public class AutomationServerTests
{
    /// <summary>
    ///     Creates a unique automation name so parallel tests never share a pipe.
    /// </summary>
    /// <returns>The name.</returns>
    // Kept short: macOS limits Unix domain socket paths (TMPDIR + CoreFxPipe_ + name) to 104 bytes.
    private static string UniqueName() => $"t{Guid.NewGuid():N}"[..13];

    /// <summary>
    ///     Connects a client and exchanges lines.
    /// </summary>
    /// <param name="name">The automation name.</param>
    /// <param name="requests">The request lines.</param>
    /// <returns>The reply lines.</returns>
    private static async Task<List<string>> ExchangeAsync(string name, params string[] requests)
    {
        await using var client = new NamedPipeClientStream(".", AutomationServer.PipePrefix + name, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await client.ConnectAsync(10_000);
        var utf8 = new UTF8Encoding(false);
        using var writer = new StreamWriter(client, utf8, 1024, leaveOpen: true) { AutoFlush = true, NewLine = "\n" };
        using var reader = new StreamReader(client, utf8, false, 1024, leaveOpen: true);
        var replies = new List<string>();
        foreach (var request in requests)
        {
            await writer.WriteLineAsync(request);
            replies.Add((await reader.ReadLineAsync())!);
        }

        return replies;
    }

    /// <summary>
    ///     Validates that the pipe name is the documented prefix plus the automation name.
    /// </summary>
    [Fact]
    public void AutomationServer_Constructor_BuildsPipeName()
    {
        // Arrange / Act
        using var server = new AutomationServer("abc", _ => Task.FromResult(new AutomationResponse("{}", false)), () => { });

        // Assert
        Assert.Equal("plexus-demo-abc", server.PipeName);
    }

    /// <summary>
    ///     Validates that one client can send several requests and gets one reply line per request, in order.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public async Task AutomationServer_OneClient_GetsOneReplyPerRequestLine()
    {
        // Arrange
        var name = UniqueName();
        using var server = new AutomationServer(name, line => Task.FromResult(new AutomationResponse($"echo:{line}", false)), () => { });
        server.Start();

        // Act
        var replies = await ExchangeAsync(name, "a", "b", "c");

        // Assert
        Assert.Equal(["echo:a", "echo:b", "echo:c"], replies);
    }

    /// <summary>
    ///     Validates that clients can connect one after another, each served normally.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public async Task AutomationServer_SequentialClients_AreAllServed()
    {
        // Arrange
        var name = UniqueName();
        using var server = new AutomationServer(name, line => Task.FromResult(new AutomationResponse($"echo:{line}", false)), () => { });
        server.Start();

        // Act
        var first = await ExchangeAsync(name, "1");
        var second = await ExchangeAsync(name, "2", "3");
        var third = await ExchangeAsync(name, "4");

        // Assert
        Assert.Equal(["echo:1"], first);
        Assert.Equal(["echo:2", "echo:3"], second);
        Assert.Equal(["echo:4"], third);
    }

    /// <summary>
    ///     Validates that blank lines are skipped rather than answered.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public async Task AutomationServer_BlankLines_AreIgnored()
    {
        // Arrange
        var name = UniqueName();
        using var server = new AutomationServer(name, line => Task.FromResult(new AutomationResponse($"echo:{line}", false)), () => { });
        server.Start();

        // Act: the blank line gets no reply, so the next reply belongs to "x"
        await using var client = new NamedPipeClientStream(".", AutomationServer.PipePrefix + name, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await client.ConnectAsync(10_000, TestContext.Current.CancellationToken);
        using var writer = new StreamWriter(client, new UTF8Encoding(false), 1024, leaveOpen: true) { AutoFlush = true, NewLine = "\n" };
        using var reader = new StreamReader(client, new UTF8Encoding(false), false, 1024, leaveOpen: true);
        await writer.WriteLineAsync(string.Empty);
        await writer.WriteLineAsync("x");
        var reply = await reader.ReadLineAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("echo:x", reply);
    }

    /// <summary>
    ///     Validates that a quit reply is sent first and only then is the quit callback invoked.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public async Task AutomationServer_QuitReply_IsSentThenCallbackRuns()
    {
        // Arrange
        var name = UniqueName();
        var quit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var server = new AutomationServer(name, _ => Task.FromResult(new AutomationResponse("bye", true)), () => quit.TrySetResult());
        server.Start();

        // Act
        var replies = await ExchangeAsync(name, "quit");
        var completed = await Task.WhenAny(quit.Task, Task.Delay(10_000, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["bye"], replies);
        Assert.Same(quit.Task, completed);
    }
}

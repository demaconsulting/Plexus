using System.Text.Json.Nodes;
using DemaConsulting.Plexus.Avalonia;
using DemaConsulting.Plexus.Demo.Gui;
using DemaConsulting.Plexus.Demo.Model;

namespace DemaConsulting.Plexus.Demo.Tests;

/// <summary>
///     Tests for the automation command dispatcher: protocol, queries and model edits.
/// </summary>
public class AutomationDispatcherTests
{
    /// <summary>
    ///     Gets the names of the nodes on the shown page.
    /// </summary>
    /// <param name="host">The host.</param>
    /// <returns>The node names in page order.</returns>
    private static string[] NodeNames(GuiHost host) => [.. host.Session.RequirePage().Nodes.Select(n => n.Name)];

    /// <summary>
    ///     A clipboard that fails like a locked desktop session, with an unrelated stale error text as Windows can attach.
    /// </summary>
    private sealed class LockedClipboard : IDiagramClipboard
    {
        /// <inheritdoc />
        public Task SetAsync(string json) => Task.FromException(Failure());

        /// <inheritdoc />
        public Task<string?> GetAsync() => Task.FromException<string?>(Failure());

        /// <summary>
        ///     Creates the exception: CLIPBRD_E_CANT_OPEN with a misleading message.
        /// </summary>
        /// <returns>The exception.</returns>
        private static System.Runtime.InteropServices.COMException Failure() =>
            new("oldValue has incorrect type for this property", unchecked((int)0x800401D0));
    }

    /// <summary>
    ///     Validates that ping answers with the one-line success envelope.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task AutomationDispatcher_Ping_ReturnsPong() => GuiTest.RunAsync(async host =>
    {
        // Act
        var response = await host.Commands.ExecuteAsync("""{"cmd":"ping"}""");

        // Assert
        Assert.Equal("""{"ok":true,"result":{"pong":true}}""", response.Json);
        Assert.False(response.Quit);
    });

    /// <summary>
    ///     Validates that unknown commands, bad JSON and malformed requests all become error replies, never exceptions.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task AutomationDispatcher_BadRequests_AreErrorRepliesNotExceptions() => GuiTest.RunAsync(async host =>
    {
        // Act / Assert
        Assert.Contains("Unknown command 'bogus'", await host.ErrorAsync("""{"cmd":"bogus"}"""), StringComparison.Ordinal);
        Assert.Contains("Invalid JSON", await host.ErrorAsync("not json"), StringComparison.Ordinal);
        Assert.Contains("JSON object", await host.ErrorAsync("[1,2]"), StringComparison.Ordinal);
        Assert.Contains("'cmd'", await host.ErrorAsync("""{"x":1}"""), StringComparison.Ordinal);
        Assert.Contains("'type'", await host.ErrorAsync("""{"cmd":"add-node","x":1,"y":2}"""), StringComparison.Ordinal);
        Assert.Contains("number", await host.ErrorAsync("""{"cmd":"add-node","type":"Mixer","x":"left","y":2}"""), StringComparison.Ordinal);

        // The unknown node type is the model's refusal and must also be an error reply
        Assert.NotEmpty(await host.ErrorAsync("""{"cmd":"add-node","type":"no.Such","x":0,"y":0}"""));
        Assert.Equal("pong", (await host.OkAsync("""{"cmd":"ping"}""")).AsObject().ContainsKey("pong") ? "pong" : "no");
    });

    /// <summary>
    ///     Validates that quit is answered, then flagged so the transport can end the process after replying.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task AutomationDispatcher_Quit_IsFlagged() => GuiTest.RunAsync(async host =>
    {
        // Act
        var response = await host.Commands.ExecuteAsync("""{"cmd":"quit"}""");

        // Assert
        Assert.True(response.Quit);
        Assert.Contains("\"ok\":true", response.Json, StringComparison.Ordinal);
    });

    /// <summary>
    ///     Validates that every command of the documented protocol exists.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task AutomationDispatcher_CommandNames_CoverTheProtocol() => GuiTest.RunAsync(host =>
    {
        // Arrange
        string[] expected =
        [
            "ping", "info", "list-pages", "select-page", "add-page", "rename-page", "delete-page",
            "list-nodes", "list-wires", "list-annotations", "list-groups",
            "add-node", "move-node", "set-property", "toggle-switch", "rename", "connect", "delete", "select", "clear-selection", "select-all",
            "copy", "cut", "paste", "duplicate", "undo", "redo", "group", "ungroup", "collapse", "move-to-page",
            "geometry", "view", "bring-into-view", "zoom-to", "tooltip", "save", "open", "new", "render",
        ];

        // Act / Assert
        foreach (var name in expected)
        {
            Assert.Contains(name, host.Commands.CommandNames);
        }

        return Task.CompletedTask;
    });

    /// <summary>
    ///     Validates that info describes a fresh document and then reflects an edit, with undo text.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task AutomationDispatcher_Info_ReflectsDocumentState() => GuiTest.RunAsync(async host =>
    {
        // Arrange
        var fresh = await host.OkAsync("""{"cmd":"info"}""");

        // Act
        await host.OkAsync("""{"cmd":"add-node","type":"demo.audio.Mixer","x":30,"y":30,"name":"Mix"}""");
        var edited = await host.OkAsync("""{"cmd":"info"}""");

        // Assert
        Assert.Equal(Environment.ProcessId, fresh["pid"]!.GetValue<int>());
        Assert.Equal("Plexus Demo - Untitled", fresh["title"]!.GetValue<string>());
        Assert.False(fresh["dirty"]!.GetValue<bool>());
        Assert.False(fresh["canUndo"]!.GetValue<bool>());
        Assert.Equal("Page 1", fresh["pages"]![0]!.GetValue<string>());
        Assert.Equal(0, fresh["currentPage"]!.GetValue<int>());
        Assert.Equal("full", fresh["typeSet"]!.GetValue<string>());

        Assert.Equal("Plexus Demo - Untitled*", edited["title"]!.GetValue<string>());
        Assert.True(edited["dirty"]!.GetValue<bool>());
        Assert.True(edited["canUndo"]!.GetValue<bool>());
        Assert.False(string.IsNullOrEmpty(edited["undoText"]!.GetValue<string>()));
        Assert.Single(edited["selection"]!.AsArray());
    });

    /// <summary>
    ///     Validates adding, listing, moving and renaming nodes, including the rename rejection reason.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task AutomationDispatcher_NodeCommands_AddListMoveRename() => GuiTest.RunAsync(async host =>
    {
        // Arrange
        var added = await host.OkAsync("""{"cmd":"add-node","type":"demo.audio.Filter","x":15,"y":30,"name":"Tone"}""");
        await host.OkAsync("""{"cmd":"add-node","type":"Filter","x":300,"y":30}""");

        // Act
        await host.OkAsync("""{"cmd":"move-node","node":"Tone","x":150,"y":75}""");
        await host.OkAsync($$"""{"cmd":"rename","node":"{{added["id"]}}","name":"Warmth"}""");
        var duplicate = await host.ErrorAsync("""{"cmd":"rename","node":"Warmth","name":"Filter1"}""");
        var nodes = (await host.OkAsync("""{"cmd":"list-nodes"}""")).AsArray();

        // Assert
        Assert.Equal(["Warmth", "Filter1"], nodes.Select(n => n!["name"]!.GetValue<string>()));
        Assert.Equal(150, nodes[0]!["bounds"]!["x"]!.GetValue<double>());
        Assert.Equal(75, nodes[0]!["bounds"]!["y"]!.GetValue<double>());
        Assert.Equal("demo.audio.Filter", nodes[0]!["typeKey"]!.GetValue<string>());
        Assert.True(nodes[0]!["visible"]!.GetValue<bool>());
        Assert.Equal(1200, nodes[0]!["properties"]!["Cutoff"]!.GetValue<double>());
        Assert.False(string.IsNullOrWhiteSpace(duplicate));
    });

    /// <summary>
    ///     Validates that set-property parses against the definition and refuses bad values and unknown nodes.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task AutomationDispatcher_SetProperty_ParsesAgainstDefinition() => GuiTest.RunAsync(async host =>
    {
        // Arrange
        await host.OkAsync("""{"cmd":"add-node","type":"demo.math.Scope","x":0,"y":0,"name":"Plot"}""");
        await host.OkAsync("""{"cmd":"add-node","type":"demo.audio.Oscillator","x":300,"y":0,"name":"Osc"}""");

        // Act
        await host.OkAsync("""{"cmd":"set-property","node":"Plot","name":"Samples","value":512}""");
        await host.OkAsync("""{"cmd":"set-property","node":"Plot","name":"Autoscale","value":false}""");
        await host.OkAsync("""{"cmd":"set-property","node":"Osc","name":"Waveform","value":"Square"}""");
        await host.OkAsync("""{"cmd":"set-property","node":"Osc","name":"Frequency","value":880.5}""");
        var badNumber = await host.ErrorAsync("""{"cmd":"set-property","node":"Plot","name":"Samples","value":"many"}""");
        var badChoice = await host.ErrorAsync("""{"cmd":"set-property","node":"Osc","name":"Waveform","value":"Noise"}""");
        var missing = await host.ErrorAsync("""{"cmd":"set-property","node":"Nobody","name":"Samples","value":1}""");
        var plot = host.Session.Diagram.FindNode("Plot")!;
        var osc = host.Session.Diagram.FindNode("Osc")!;

        // Assert
        Assert.Equal(512, plot.Properties.Get<int>("Samples"));
        Assert.False(plot.Properties.Get<bool>("Autoscale"));
        Assert.Equal("Square", osc.Properties.Get<string>("Waveform"));
        Assert.Equal(880.5, osc.Properties.Get<double>("Frequency"));
        Assert.Contains("whole number", badNumber, StringComparison.Ordinal);
        Assert.Contains("allowed choices", badChoice, StringComparison.Ordinal);
        Assert.Contains("Nobody", missing, StringComparison.Ordinal);
    });

    /// <summary>
    ///     Validates connecting by "Node.port", listing wires, and that undo and redo remove and restore the wire.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task AutomationDispatcher_Connect_CreatesWireAndUndoRemovesIt() => GuiTest.RunAsync(async host =>
    {
        // Arrange
        await host.OkAsync("""{"cmd":"add-node","type":"demo.math.Constant","x":0,"y":0,"name":"Src"}""");
        await host.OkAsync("""{"cmd":"add-node","type":"demo.math.Scope","x":300,"y":0,"name":"Plot"}""");

        // Act
        await host.OkAsync("""{"cmd":"connect","from":"Src.value","to":"Plot.signal"}""");
        var wires = (await host.OkAsync("""{"cmd":"list-wires"}""")).AsArray();
        var undone = await host.OkAsync("""{"cmd":"undo"}""");
        var afterUndo = (await host.OkAsync("""{"cmd":"list-wires"}""")).AsArray();
        await host.OkAsync("""{"cmd":"redo"}""");
        var afterRedo = (await host.OkAsync("""{"cmd":"list-wires"}""")).AsArray();
        var badPort = await host.ErrorAsync("""{"cmd":"connect","from":"Src.nope","to":"Plot.signal"}""");
        var malformed = await host.ErrorAsync("""{"cmd":"connect","from":"Src","to":"Plot.signal"}""");

        // Assert
        var wire = Assert.Single(wires);
        Assert.Equal("Src.value", wire!["source"]!.GetValue<string>());
        Assert.Equal("Plot.signal", wire["target"]!.GetValue<string>());
        Assert.True(undone["performed"]!.GetValue<bool>());
        Assert.Empty(afterUndo);
        Assert.Single(afterRedo);
        Assert.Contains("nope", badPort, StringComparison.Ordinal);
        Assert.Contains("Node.port", malformed, StringComparison.Ordinal);
    });

    /// <summary>
    ///     Validates that a port name that exists in both directions is disambiguated by from/to semantics.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task AutomationDispatcher_Connect_SharedPortName_UsesOutputForFromAndInputForTo() => GuiTest.RunAsync(async host =>
    {
        // Arrange: the flow meter has both an input "flow" and an output "flow"
        await host.OkAsync("""{"cmd":"add-node","type":"demo.plumbing.FlowMeter","x":0,"y":0,"name":"M"}""");
        await host.OkAsync("""{"cmd":"add-node","type":"demo.plumbing.Valve","x":300,"y":0,"name":"V"}""");

        // Act
        await host.OkAsync("""{"cmd":"connect","from":"M.flow","to":"V.in"}""");
        var wire = (await host.OkAsync("""{"cmd":"list-wires"}""")).AsArray().Single()!;
        var input = await host.OkAsync("""{"cmd":"geometry","port":"M.flow","direction":"in"}""");
        var output = await host.OkAsync("""{"cmd":"geometry","port":"M.flow","direction":"out"}""");

        // Assert
        Assert.Equal("M.flow", wire["source"]!.GetValue<string>());
        Assert.Equal("in", input["port"]!["direction"]!.GetValue<string>());
        Assert.Equal("out", output["port"]!["direction"]!.GetValue<string>());
        Assert.NotEqual(input["port"]!["anchor"]!["diagram"]!["x"]!.GetValue<double>(), output["port"]!["anchor"]!["diagram"]!["x"]!.GetValue<double>());
    });

    /// <summary>
    ///     Validates selection commands, and that delete accepts names and ids and is undoable.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task AutomationDispatcher_SelectionAndDelete_Work() => GuiTest.RunAsync(async host =>
    {
        // Arrange
        await host.OkAsync("""{"cmd":"add-node","type":"demo.audio.Filter","x":0,"y":0,"name":"A"}""");
        var b = await host.OkAsync("""{"cmd":"add-node","type":"demo.audio.Filter","x":200,"y":0,"name":"B"}""");
        await host.OkAsync("""{"cmd":"add-node","type":"demo.audio.Filter","x":400,"y":0,"name":"C"}""");

        // Act
        await host.OkAsync("""{"cmd":"clear-selection"}""");
        var selectAll = await host.OkAsync("""{"cmd":"select-all"}""");
        await host.OkAsync("""{"cmd":"select","names":["A"]}""");
        var added = await host.OkAsync($$"""{"cmd":"select","ids":["{{b["id"]}}"],"add":true}""");
        await host.OkAsync("""{"cmd":"delete","names":["A","C"]}""");
        var remaining = NodeNames(host);
        await host.OkAsync("""{"cmd":"undo"}""");

        // Assert
        Assert.Equal(3, selectAll["selection"]!.AsArray().Count);
        Assert.Equal(["A", "B"], added["selection"]!.AsArray().Select(s => s!["name"]!.GetValue<string>()));
        Assert.Equal(["B"], remaining);
        Assert.Equal(["A", "B", "C"], NodeNames(host).Order());
        Assert.Contains("'ids' or 'names'", await host.ErrorAsync("""{"cmd":"delete"}"""), StringComparison.Ordinal);
    });

    /// <summary>
    ///     Validates copy, paste at a position, cut and duplicate through the clipboard of the editor.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task AutomationDispatcher_ClipboardCommands_CopyPasteCutDuplicate() => GuiTest.RunAsync(async host =>
    {
        // Arrange
        await host.OkAsync("""{"cmd":"add-node","type":"demo.math.Constant","x":0,"y":0,"name":"Src"}""");

        // Act
        var copied = await host.OkAsync("""{"cmd":"copy"}""");
        var pasted = await host.OkAsync("""{"cmd":"paste","x":300,"y":150}""");
        var duplicate = await host.OkAsync("""{"cmd":"duplicate"}""");
        var cut = await host.OkAsync("""{"cmd":"cut"}""");
        var half = await host.ErrorAsync("""{"cmd":"paste","x":5}""");

        // Assert
        Assert.Equal(1, copied["copied"]!.GetValue<int>());
        Assert.NotNull(host.Clipboard.Text);
        Assert.True(pasted["success"]!.GetValue<bool>());
        Assert.Single(pasted["ids"]!.AsArray());
        Assert.Single(duplicate["ids"]!.AsArray());
        Assert.Equal(1, cut["cut"]!.GetValue<int>());
        Assert.Equal(2, NodeNames(host).Length);
        Assert.Contains("both", half, StringComparison.Ordinal);
        var pastedNode = host.Session.Diagram.FindElement(pasted["ids"]![0]!.GetValue<string>());
        Assert.Equal(300, pastedNode!.GetBounds().X);
        Assert.Equal(150, pastedNode.GetBounds().Y);
    });

    /// <summary>
    ///     Validates that a platform clipboard failure is reported with its HRESULT and a cause, not with the (possibly stale) COM message.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task AutomationDispatcher_Copy_ClipboardLocked_ReportsHResultNotStaleComText() => GuiTest.RunAsync(async host =>
    {
        // Arrange: the OS clipboard fails like on a locked desktop, carrying an unrelated stale error text
        host.Editor.Clipboard = new LockedClipboard();
        await host.OkAsync("""{"cmd":"add-node","type":"demo.math.Constant","x":0,"y":0,"name":"Src"}""");

        // Act
        var error = await host.ErrorAsync("""{"cmd":"copy"}""");

        // Assert
        Assert.Contains("The clipboard is not available (error 0x800401D0)", error, StringComparison.Ordinal);
        Assert.DoesNotContain("oldValue", error, StringComparison.Ordinal);
    });

    /// <summary>
    ///     Validates that a paste of non-Plexus clipboard text is reported as data and in the status message.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task AutomationDispatcher_Paste_GarbageClipboard_ReportsFailure() => GuiTest.RunAsync(async host =>
    {
        // Arrange
        host.Clipboard.Text = "just some text";

        // Act
        var result = await host.OkAsync("""{"cmd":"paste"}""");

        // Assert
        Assert.False(result["success"]!.GetValue<bool>());
        Assert.False(string.IsNullOrEmpty(result["error"]!.GetValue<string>()));
        Assert.StartsWith("Paste failed", host.Session.Message, StringComparison.Ordinal);
        Assert.Empty(NodeNames(host));
    });

    /// <summary>
    ///     Validates the deliberate failure path: a 'basic' instance refuses a paste that needs types it lacks and names them.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task AutomationDispatcher_Paste_MissingNodeTypes_AreListedInResultAndStatus() => GuiTest.RunAsync(
        async host =>
        {
            // Arrange: what a full instance would have put on the clipboard
            var full = new Diagram(DemoNodeTypes.CreateRegistry());
            var node = full.Pages.Add("Main").AddNode(DemoNodeTypes.Mixer, 0, 0);
            host.Clipboard.Text = PlexusClipboard.Copy([node]);

            // Act
            var result = await host.OkAsync("""{"cmd":"paste"}""");

            // Assert
            Assert.False(result["success"]!.GetValue<bool>());
            Assert.Equal(["demo.audio.Mixer"], result["missingNodeTypes"]!.AsArray().Select(t => t!.GetValue<string>()));
            Assert.Equal("Paste failed: this application does not know the node type(s) demo.audio.Mixer.", host.Session.Message);
            Assert.Empty(NodeNames(host));
            Assert.Equal("basic", (await host.OkAsync("""{"cmd":"info"}"""))["typeSet"]!.GetValue<string>());
        },
        NodeTypeSet.Basic);

    /// <summary>
    ///     Validates that the basic type set really is a subset, so the failure path above is meaningful.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task AutomationDispatcher_BasicTypeSet_RefusesFullOnlyTypes() => GuiTest.RunAsync(
        async host =>
        {
            // Act
            var refused = await host.ErrorAsync("""{"cmd":"add-node","type":"demo.audio.Mixer","x":0,"y":0}""");
            var refusedShort = await host.ErrorAsync("""{"cmd":"add-node","type":"Mixer","x":0,"y":0}""");
            var accepted = await host.OkAsync("""{"cmd":"add-node","type":"demo.math.Scope","x":0,"y":0}""");

            // Assert
            Assert.NotEmpty(refused);
            Assert.NotEmpty(refusedShort);
            Assert.NotNull(accepted["id"]);
        },
        NodeTypeSet.Basic);

    /// <summary>
    ///     Validates grouping, collapsing and ungrouping, by title as well as id.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task AutomationDispatcher_GroupCommands_GroupCollapseUngroup() => GuiTest.RunAsync(async host =>
    {
        // Arrange
        await host.OkAsync("""{"cmd":"add-node","type":"demo.audio.Filter","x":0,"y":0,"name":"A"}""");
        await host.OkAsync("""{"cmd":"add-node","type":"demo.audio.Filter","x":200,"y":0,"name":"B"}""");

        // Act
        var group = await host.OkAsync("""{"cmd":"group","names":["A","B"],"title":"Voices"}""");
        await host.OkAsync("""{"cmd":"collapse","group":"Voices","collapsed":true}""");
        var collapsed = (await host.OkAsync("""{"cmd":"list-groups"}""")).AsArray().Single()!;
        var hidden = (await host.OkAsync("""{"cmd":"list-nodes"}""")).AsArray().Select(n => n!["visible"]!.GetValue<bool>()).ToArray();
        await host.OkAsync("""{"cmd":"collapse","group":"Voices","collapsed":false}""");
        var ungrouped = await host.OkAsync("""{"cmd":"ungroup","group":"Voices"}""");

        // Assert
        Assert.Equal("Voices", group["title"]!.GetValue<string>());
        Assert.Equal(2, collapsed["members"]!.AsArray().Count);
        Assert.True(collapsed["collapsed"]!.GetValue<bool>());
        Assert.Equal([false, false], hidden);
        Assert.Equal(1, ungrouped["ungrouped"]!.GetValue<int>());
        Assert.Empty((await host.OkAsync("""{"cmd":"list-groups"}""")).AsArray());
        Assert.Contains("not a group", await host.ErrorAsync("""{"cmd":"ungroup","group":"A"}"""), StringComparison.Ordinal);
    });

    /// <summary>
    ///     Validates the page commands: add, select by name and index, rename, move elements, delete and the last-page rule.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task AutomationDispatcher_PageCommands_ManagePages() => GuiTest.RunAsync(async host =>
    {
        // Arrange
        await host.OkAsync("""{"cmd":"add-node","type":"demo.audio.Filter","x":0,"y":0,"name":"A"}""");

        // Act
        var added = await host.OkAsync("""{"cmd":"add-page","name":"Second"}""");
        var afterAdd = await host.OkAsync("""{"cmd":"info"}""");
        await host.OkAsync("""{"cmd":"select-page","page":0}""");
        await host.OkAsync("""{"cmd":"move-to-page","names":["A"],"page":"Second"}""");
        var movedPages = (await host.OkAsync("""{"cmd":"list-pages"}""")).AsArray();
        await host.OkAsync("""{"cmd":"rename-page","page":"Second","name":"Moved"}""");
        await host.OkAsync("""{"cmd":"select-page","page":"Moved"}""");
        var onMoved = (await host.OkAsync("""{"cmd":"list-nodes"}""")).AsArray();
        var missing = await host.ErrorAsync("""{"cmd":"select-page","page":"Nope"}""");
        await host.OkAsync("""{"cmd":"delete-page","page":"Page 1"}""");
        var last = await host.ErrorAsync("""{"cmd":"delete-page","page":"Moved"}""");

        // Assert
        Assert.Equal(1, added["index"]!.GetValue<int>());
        Assert.Equal(1, afterAdd["currentPage"]!.GetValue<int>());
        Assert.Equal(["Page 1", "Second"], afterAdd["pages"]!.AsArray().Select(p => p!.GetValue<string>()));
        Assert.Equal(0, movedPages[0]!["nodes"]!.GetValue<int>());
        Assert.Equal(1, movedPages[1]!["nodes"]!.GetValue<int>());
        Assert.Equal("A", onMoved.Single()!["name"]!.GetValue<string>());
        Assert.Contains("Nope", missing, StringComparison.Ordinal);
        Assert.Contains("at least one page", last, StringComparison.Ordinal);
        Assert.Equal(["Moved"], host.Session.Diagram.Pages.Select(p => p.Name));
        Assert.Equal(["Moved"], host.Session.Diagram.Pages.Select(p => p.Name));
    });

    /// <summary>
    ///     Validates that undoing a page deletion brings the page back, in the tab list too.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task AutomationDispatcher_DeletePage_UndoRestoresPageAndTabItem() => GuiTest.RunAsync(async host =>
    {
        // Arrange
        await host.OkAsync("""{"cmd":"add-page","name":"Second"}""");

        // Act
        await host.OkAsync("""{"cmd":"delete-page","page":"Second"}""");
        var afterDelete = host.Session.Diagram.Pages.Select(p => p.Name).ToArray();
        var shown = host.Session.CurrentPage!.Name;
        await host.OkAsync("""{"cmd":"undo"}""");

        // Assert
        Assert.Equal(["Page 1"], afterDelete);
        Assert.Equal("Page 1", shown);
        Assert.Equal(["Page 1", "Second"], host.Session.Diagram.Pages.Select(p => p.Name));
    });

    /// <summary>
    ///     Validates that new, save and open round-trip a document and manage the file name and dirty state.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task AutomationDispatcher_SaveOpenNew_RoundTripsDocument() => GuiTest.RunAsync(async host =>
    {
        // Arrange
        using var temp = new TempDirectory();
        var path = System.IO.Path.Join(temp.Create(), "doc.json");
        await host.OkAsync("""{"cmd":"add-node","type":"demo.audio.Mixer","x":30,"y":30,"name":"Mix"}""");
        var noFile = await host.ErrorAsync("""{"cmd":"save"}""");

        // Act
        await host.OkAsync($$"""{"cmd":"save","path":"{{path.Replace("\\", "\\\\", StringComparison.Ordinal)}}"}""");
        var saved = await host.OkAsync("""{"cmd":"info"}""");
        await host.OkAsync("""{"cmd":"new"}""");
        var blank = NodeNames(host);
        var opened = await host.OkAsync($$"""{"cmd":"open","path":"{{path.Replace("\\", "\\\\", StringComparison.Ordinal)}}"}""");

        // Assert
        Assert.Contains("path", noFile, StringComparison.Ordinal);
        Assert.True(File.Exists(path));
        Assert.Equal("Plexus Demo - doc.json", saved["title"]!.GetValue<string>());
        Assert.False(saved["dirty"]!.GetValue<bool>());
        Assert.Empty(blank);
        Assert.Equal(["Mix"], NodeNames(host));
        Assert.Equal(path, opened["file"]!.GetValue<string>());
        Assert.False(opened["dirty"]!.GetValue<bool>());
        Assert.NotEmpty(await host.ErrorAsync("""{"cmd":"open","path":"C:\\definitely\\missing.json"}"""));
    });

    /// <summary>
    ///     Validates that a document that needs types this instance lacks is refused and leaves the current document alone.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task AutomationDispatcher_Open_MissingNodeTypes_KeepsCurrentDocument() => GuiTest.RunAsync(
        async host =>
        {
            // Arrange
            using var temp = new TempDirectory();
            var path = System.IO.Path.Join(temp.Create(), "full.json");
            DemoDocument.Save(DemoSamples.Audio(), path);
            await host.OkAsync("""{"cmd":"add-node","type":"demo.math.Scope","x":0,"y":0,"name":"Keep"}""");

            // Act
            var error = await host.ErrorAsync($$"""{"cmd":"open","path":"{{path.Replace("\\", "\\\\", StringComparison.Ordinal)}}"}""");

            // Assert
            Assert.NotEmpty(error);
            Assert.Equal(["Keep"], NodeNames(host));
        },
        NodeTypeSet.Basic);

    /// <summary>
    ///     Validates that render writes a real PNG of the shown page.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task AutomationDispatcher_Render_WritesPng() => GuiTest.RunAsync(async host =>
    {
        // Arrange
        using var temp = new TempDirectory();
        var path = System.IO.Path.Join(temp.Path, "sub", "page.png");
        await host.OkAsync("""{"cmd":"add-node","type":"demo.audio.Mixer","x":30,"y":30}""");

        // Act
        var result = await host.OkAsync($$"""{"cmd":"render","path":"{{path.Replace("\\", "\\\\", StringComparison.Ordinal)}}","scale":1.5}""");
        var badScale = await host.ErrorAsync($$"""{"cmd":"render","path":"{{path.Replace("\\", "\\\\", StringComparison.Ordinal)}}","scale":0}""");

        // Assert
        var bytes = File.ReadAllBytes(path);
        Assert.Equal([0x89, 0x50, 0x4E, 0x47], bytes[..4]);
        Assert.Equal(bytes.Length, result["bytes"]!.GetValue<long>());
        Assert.Contains("scale", badScale, StringComparison.Ordinal);
    });

    /// <summary>
    ///     Validates that annotations are listed and that opening a sample gives the full showcase to query.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task AutomationDispatcher_ListCommands_DescribeSampleContent() => GuiTest.RunAsync(async host =>
    {
        // Arrange
        host.Session.OpenSample("showcase");
        host.Flush();

        // Act
        var pages = (await host.OkAsync("""{"cmd":"list-pages"}""")).AsArray();
        var groupsPage = pages.First(p => p!["groups"]!.GetValue<int>() > 0)!;
        var groups = (await host.OkAsync($$"""{"cmd":"list-groups","page":{{groupsPage["index"]}}}""")).AsArray();
        await host.OkAsync($$"""{"cmd":"select-page","page":{{groupsPage["index"]}}}""");
        host.Session.RequirePage().AddAnnotation("Hello", new Geometry.Rect(0, 0, 100, 40));
        var annotations = (await host.OkAsync("""{"cmd":"list-annotations"}""")).AsArray();

        // Assert
        Assert.Equal(7, pages.Count);
        Assert.NotEmpty(groups);
        Assert.NotNull(groups[0]!["title"]);
        Assert.Contains(annotations, a => a!["text"]!.GetValue<string>() == "Hello");
    });

    /// <summary>
    ///     Validates that bring-into-view and zoom-to switch to the element's page and report the view.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task AutomationDispatcher_NavigationCommands_BringIntoViewAndZoomTo() => GuiTest.RunAsync(async host =>
    {
        // Arrange: the showcase; the logic page is the third one
        host.Session.OpenSample("showcase");
        host.Flush();

        // Act
        var shown = await host.OkAsync("""{"cmd":"bring-into-view","name":"AllLamp"}""");
        await host.OkAsync("""{"cmd":"select-page","page":"Math"}""");
        var fitted = await host.OkAsync("""{"cmd":"zoom-to","names":["SwitchA","AllLamp"],"margin":20}""");
        var unknown = await host.ErrorAsync("""{"cmd":"bring-into-view","name":"Nobody"}""");

        // Assert
        Assert.Equal(2, shown["page"]!.GetValue<int>());
        Assert.Equal(1.0, shown["zoom"]!.GetValue<double>());
        Assert.Equal(2, fitted["page"]!.GetValue<int>());
        Assert.Equal("Logic", host.Editor.Page!.Name);
        Assert.Contains("Nobody", unknown, StringComparison.Ordinal);
    });

    /// <summary>
    ///     Validates that the tooltip command returns node, port and wire tooltips, including live logic state.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task AutomationDispatcher_TooltipCommand_DescribesNodesPortsAndWires() => GuiTest.RunAsync(async host =>
    {
        // Arrange
        host.Session.OpenSample("logic");
        host.Flush();
        var wire = host.Session.Diagram.Pages[0].Wires.First(w => w.Target.ToString() == "All.c");

        // Act
        var node = await host.OkAsync("""{"cmd":"tooltip","node":"SwitchA"}""");
        var port = await host.OkAsync("""{"cmd":"tooltip","port":"All.c"}""");
        var wireTip = await host.OkAsync($$"""{"cmd":"tooltip","element":"{{wire.Id}}"}""");

        // Assert
        Assert.Contains("Now: on", node["text"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal("c (Boolean)\nGate input c.", port["text"]!.GetValue<string>());
        Assert.Equal("SwitchC.out → All.c", wireTip["text"]!.GetValue<string>());
    });

    /// <summary>
    ///     Validates that the tooltip command of the real window reports an oscillator's thumbnail tooltip as its text and a
    ///     preview flag, and other tooltips as text without one.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task AutomationDispatcher_TooltipCommand_OscillatorReportsTextAndPreview() => GuiTest.RunAsync(async () =>
    {
        // Arrange: the real window, whose tooltip handler gives oscillators, envelopes and filters a plot thumbnail
        using var host = new MainWindowHost(MainWindowHost.Options("--sample", "audio"));
        var commands = new Gui.Automation.AutomationDispatcher(host.Session);

        // Act
        var osc = JsonNode.Parse((await commands.ExecuteAsync("""{"cmd":"tooltip","node":"Osc1"}""")).Json)!["result"]!;
        var port = JsonNode.Parse((await commands.ExecuteAsync("""{"cmd":"tooltip","port":"Osc1.out"}""")).Json)!["result"]!;
        var shape = JsonNode.Parse((await commands.ExecuteAsync("""{"cmd":"tooltip","node":"Shape"}""")).Json)!["result"]!;
        var tone = JsonNode.Parse((await commands.ExecuteAsync("""{"cmd":"tooltip","node":"Tone"}""")).Json)!["result"]!;

        // Assert: the oscillator, envelope and filter tooltips carry a plot; the port tooltip adds the port description
        Assert.Equal("Osc1\nSaw, 220 Hz", osc["text"]!.GetValue<string>());
        Assert.True(osc["preview"]!.GetValue<bool>());
        Assert.Equal("out (Audio)\nThe generated wave.", port["text"]!.GetValue<string>());
        Assert.False(port["preview"]!.GetValue<bool>());
        Assert.Equal("Shape\nA 10 ms · D 120 ms · S 70 % · R 300 ms", shape["text"]!.GetValue<string>());
        Assert.True(shape["preview"]!.GetValue<bool>());
        Assert.Equal("Tone\nLow-pass, 900 Hz", tone["text"]!.GetValue<string>());
        Assert.True(tone["preview"]!.GetValue<bool>());
    });

    /// <summary>
    ///     Validates that setting a gate's Inputs property through automation changes the ports list-nodes reports.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task AutomationDispatcher_SetProperty_GateInputs_ChangesPorts() => GuiTest.RunAsync(async host =>
    {
        // Arrange
        await host.OkAsync("""{"cmd":"add-node","type":"demo.logic.And","x":0,"y":0,"name":"G"}""");

        // Act
        await host.OkAsync("""{"cmd":"set-property","node":"G","name":"Inputs","value":4}""");
        var nodes = (await host.OkAsync("""{"cmd":"list-nodes"}""")).AsArray();

        // Assert
        var ports = nodes[0]!["ports"]!.AsArray().Select(p => p!["name"]!.GetValue<string>());
        Assert.Equal(["a", "b", "c", "d", "out"], ports);
    });
}

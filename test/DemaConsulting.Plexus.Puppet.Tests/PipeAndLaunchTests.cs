using System.Text.Json.Nodes;
using DemaConsulting.Plexus.Puppet;
using DemaConsulting.Plexus.Puppet.Automation;

namespace DemaConsulting.Plexus.Puppet.Tests;

/// <summary>
///     Tests for pipe naming, request building, reply parsing, launch arguments and text formatting (no UI, no pipe).
/// </summary>
public class PipeAndLaunchTests
{
    /// <summary>
    ///     Validates which aliases are usable.
    /// </summary>
    /// <param name="alias">The alias.</param>
    /// <param name="valid">Whether it is valid.</param>
    [Theory]
    [InlineData("main", true)]
    [InlineData("a", true)]
    [InlineData("copy_2-b", true)]
    [InlineData("", false)]
    [InlineData("a b", false)]
    [InlineData("a\\b", false)]
    [InlineData("../x", false)]
    public void PipeNames_IsValidAlias_ChecksCharacters(string alias, bool valid) =>
        Assert.Equal(valid, PipeNames.IsValidAlias(alias));

    /// <summary>
    ///     Validates that null is not a valid alias.
    /// </summary>
    [Fact]
    public void PipeNames_IsValidAlias_Null_IsFalse() => Assert.False(PipeNames.IsValidAlias(null));

    /// <summary>
    ///     Validates that the pipe name is the prefix plus the alias.
    /// </summary>
    [Fact]
    public void PipeNames_ForAlias_AddsPrefix() => Assert.Equal("plexus-demo-b", PipeNames.ForAlias("b"));

    /// <summary>
    ///     Validates that aliases are recovered from pipe names and foreign pipes are ignored.
    /// </summary>
    [Fact]
    public void PipeNames_AliasOf_RecognizesOnlyAutomationPipes()
    {
        Assert.Multiple(
            () => Assert.Equal("a", PipeNames.AliasOf("plexus-demo-a")),
            () => Assert.Equal("main", PipeNames.AliasOf(@"\\.\pipe\plexus-demo-main")),
            () => Assert.Null(PipeNames.AliasOf("plexus-demo-")),
            () => Assert.Null(PipeNames.AliasOf("other-pipe")),
            () => Assert.Null(PipeNames.AliasOf("plexus-demo-a b")));
    }

    /// <summary>
    ///     Validates that a request is one JSON object with the command and the merged arguments.
    /// </summary>
    [Fact]
    public void PipeClient_BuildRequest_MergesCommandAndArguments()
    {
        // Arrange
        var arguments = new JsonObject { ["names"] = new JsonArray("A", "B"), ["add"] = true };

        // Act
        var request = JsonNode.Parse(PipeClient.BuildRequest("select", arguments))!;

        // Assert
        Assert.Multiple(
            () => Assert.Equal("select", request["cmd"]!.GetValue<string>()),
            () => Assert.Equal(2, request["names"]!.AsArray().Count),
            () => Assert.True(request["add"]!.GetValue<bool>()),
            () => Assert.DoesNotContain('\n', PipeClient.BuildRequest("ping", null)));
    }

    /// <summary>
    ///     Validates that a successful reply exposes its result.
    /// </summary>
    [Fact]
    public void PipeReply_Parse_Success_HasResult()
    {
        // Arrange / Act
        var reply = PipeReply.Parse("""{"ok":true,"result":{"pong":true}}""");

        // Assert
        Assert.Multiple(
            () => Assert.True(reply.Ok),
            () => Assert.True(reply.Result!["pong"]!.GetValue<bool>()),
            () => Assert.Null(reply.Error));
    }

    /// <summary>
    ///     Validates that a failed reply exposes its error.
    /// </summary>
    [Fact]
    public void PipeReply_Parse_Failure_HasError()
    {
        // Arrange / Act
        var reply = PipeReply.Parse("""{"ok":false,"error":"no such node"}""");

        // Assert
        Assert.Multiple(
            () => Assert.False(reply.Ok),
            () => Assert.Equal("no such node", reply.Error),
            () => Assert.Null(reply.Result));
    }

    /// <summary>
    ///     Validates that malformed replies are rejected.
    /// </summary>
    /// <param name="line">The reply line.</param>
    [Theory]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    [InlineData("""{"result":1}""")]
    public void PipeReply_Parse_Malformed_Throws(string line) =>
        Assert.Throws<FormatException>(() => PipeReply.Parse(line));

    /// <summary>
    ///     Validates the demo arguments built for a sample launch.
    /// </summary>
    [Fact]
    public void LaunchOptions_TryBuild_SampleAndTypes_BuildsDemoArguments()
    {
        // Arrange
        var parsed = CommandLineParser.Parse(["launch", "--instance", "c", "--sample", "math", "--types", "basic"]);

        // Act
        var ok = LaunchOptions.TryBuild(parsed, out var arguments, out var error);

        // Assert
        Assert.Multiple(
            () => Assert.True(ok, error),
            () => Assert.Equal(["--sample", "math", "--types", "basic", "--automation", "c"], arguments));
    }

    /// <summary>
    ///     Validates that the default launch is only the automation pipe of the default alias.
    /// </summary>
    [Fact]
    public void LaunchOptions_TryBuild_NoOptions_UsesMainAlias()
    {
        // Arrange
        var parsed = CommandLineParser.Parse(["launch"]);

        // Act
        var ok = LaunchOptions.TryBuild(parsed, out var arguments, out _);

        // Assert
        Assert.Multiple(
            () => Assert.True(ok),
            () => Assert.Equal(["--automation", "main"], arguments));
    }

    /// <summary>
    ///     Validates that an opened file is passed as a full path.
    /// </summary>
    [Fact]
    public void LaunchOptions_TryBuild_Open_PassesFullPath()
    {
        // Arrange
        var parsed = CommandLineParser.Parse(["launch", "--open", "diagram.json"]);

        // Act
        var ok = LaunchOptions.TryBuild(parsed, out var arguments, out _);

        // Assert
        Assert.Multiple(
            () => Assert.True(ok),
            () => Assert.Equal("--open", arguments[0]),
            () => Assert.True(Path.IsPathRooted(arguments[1])),
            () => Assert.EndsWith("diagram.json", arguments[1], StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates how launch arguments are quoted for the Windows command line.
    /// </summary>
    [Fact]
    public void LaunchOptions_Quote_QuotesOnlyWhenNeeded()
    {
        Assert.Multiple(
            () => Assert.Equal("math", LaunchOptions.Quote("math")),
            () => Assert.Equal("\"C:\\My Files\\a.json\"", LaunchOptions.Quote(@"C:\My Files\a.json")),
            () => Assert.Equal("\"\"", LaunchOptions.Quote(string.Empty)),
            () => Assert.Equal("\"say \\\"hi\\\"\"", LaunchOptions.Quote("say \"hi\"")),
            () => Assert.Equal("\"C:\\dir with space\\\\\"", LaunchOptions.Quote(@"C:\dir with space\")),
            () => Assert.Equal("--sample math \"a b\"", LaunchOptions.JoinArguments(["--sample", "math", "a b"])));
    }

    /// <summary>
    ///     Validates the launch option combinations that are rejected.
    /// </summary>
    /// <param name="commandLine">The command line.</param>
    [Theory]
    [InlineData("launch --sample math --open x.json")]
    [InlineData("launch --types huge")]
    [InlineData("launch --instance bad/alias")]
    [InlineData("launch --sample")]
    public void LaunchOptions_TryBuild_BadOptions_AreRejected(string commandLine)
    {
        // Arrange
        var parsed = CommandLineParser.Parse(commandLine.Split(' '));

        // Act
        var ok = LaunchOptions.TryBuild(parsed, out _, out var error);

        // Assert
        Assert.Multiple(
            () => Assert.False(ok),
            () => Assert.NotEmpty(error));
    }

    /// <summary>
    ///     Validates that pipe arguments parse as a JSON object, also with the backslash-escaped quotes some shells leave in.
    /// </summary>
    [Fact]
    public void Commands_ParseArguments_AcceptsPlainAndBackslashEscapedJson()
    {
        // Arrange / Act
        var plain = Commands.ParseArguments("""{"names":["A"]}""");
        var escaped = Commands.ParseArguments("""{\"names\":[\"A\"]}""");

        // Assert
        Assert.Multiple(
            () => Assert.Equal("A", plain["names"]![0]!.GetValue<string>()),
            () => Assert.Equal("A", escaped["names"]![0]!.GetValue<string>()));
    }

    /// <summary>
    ///     Validates that text that is not a JSON object is a usage error.
    /// </summary>
    /// <param name="text">The argument text.</param>
    [Theory]
    [InlineData("[1]")]
    [InlineData("nonsense")]
    public void Commands_ParseArguments_NotAnObject_IsUsageError(string text)
    {
        // Arrange / Act
        var failure = Assert.Throws<PuppetException>(() => Commands.ParseArguments(text));

        // Assert
        Assert.Equal(ExitCodes.UsageError, failure.ExitCode);
    }

    /// <summary>
    ///     Validates the scalar formatting rules.
    /// </summary>
    [Fact]
    public void PipeFormat_Value_QuotesOnlyWhenNeeded()
    {
        Assert.Multiple(
            () => Assert.Equal("Mixer", PipeFormat.Value(JsonValue.Create("Mixer"))),
            () => Assert.Equal("\"Level Mixer\"", PipeFormat.Value(JsonValue.Create("Level Mixer"))),
            () => Assert.Equal("\"\"", PipeFormat.Value(JsonValue.Create(string.Empty))),
            () => Assert.Equal("42", PipeFormat.Value(JsonValue.Create(42))),
            () => Assert.Equal("true", PipeFormat.Value(JsonValue.Create(true))),
            () => Assert.Equal("null", PipeFormat.Value(null)),
            () => Assert.Equal("[1,2]", PipeFormat.Value(new JsonArray(1, 2))));
    }

    /// <summary>
    ///     Validates that list results become one line per item and drop bulky members.
    /// </summary>
    [Fact]
    public void PipeFormat_Result_NodeList_OneLinePerNodeWithoutPorts()
    {
        // Arrange
        var result = JsonNode.Parse("""[{"id":"n1","name":"Mixer 1","ports":[{"name":"x"}]},{"id":"n2","name":"B","ports":[]}]""");

        // Act
        var text = PipeFormat.Result("list-nodes", result);
        var verbose = PipeFormat.Result("list-nodes", result, verbose: true);

        // Assert
        Assert.Multiple(
            () => Assert.Equal("id=n1 name=\"Mixer 1\"\nid=n2 name=B", text),
            () => Assert.Contains("ports=", verbose, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that an object result becomes one line per member and an empty list says so.
    /// </summary>
    [Fact]
    public void PipeFormat_Result_ObjectAndEmptyList()
    {
        Assert.Multiple(
            () => Assert.Equal("pid=12\ndirty=false", PipeFormat.Result("info", JsonNode.Parse("""{"pid":12,"dirty":false}"""))),
            () => Assert.Equal("(none)", PipeFormat.Result("list-wires", JsonNode.Parse("[]"))));
    }

    /// <summary>
    ///     Validates that menu paths split, map to demo AutomationIds and match header text.
    /// </summary>
    [Fact]
    public void MenuNavigator_Helpers_SplitMapAndMatch()
    {
        // Arrange
        var segments = MenuNavigator.Split("Page/Move Selection To/1");

        // Act / Assert
        Assert.Multiple(
            () => Assert.Equal(["Page", "Move Selection To", "1"], segments),
            () => Assert.Equal("Menu_Page", MenuNavigator.IdOf(segments, 1)),
            () => Assert.Equal("Menu_Page_MoveSelectionTo_1", MenuNavigator.IdOf(segments, 3)),
            () => Assert.True(MenuNavigator.NameMatches("_Undo", "undo")),
            () => Assert.True(MenuNavigator.NameMatches("Open Sample...", "Open Sample")),
            () => Assert.True(MenuNavigator.NameMatches("Zoom To _Fit", "zoomtofit")),
            () => Assert.False(MenuNavigator.NameMatches("Undo Move", "Undo")),
            () => Assert.False(MenuNavigator.NameMatches(string.Empty, string.Empty)));
    }

    /// <summary>
    ///     Validates that misspelled AutomationIds get the closest existing ones as suggestions.
    /// </summary>
    [Fact]
    public void ElementFinder_Suggest_ReturnsClosestIdsFirst()
    {
        // Arrange
        string[] ids = ["Menu_Edit_Undo", "Menu_Edit_Redo", "Status_Text", "Editor"];

        // Act
        var suggestions = ElementFinder.Suggest("menu_edit_undu", ids);

        // Assert
        Assert.Multiple(
            () => Assert.Equal("Menu_Edit_Undo", suggestions[0]),
            () => Assert.Equal(3, suggestions.Count),
            () => Assert.Equal(0, ElementFinder.Distance("Abc", "aBC")),
            () => Assert.Equal(3, ElementFinder.Distance("", "abc")));
    }
}

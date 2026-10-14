using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DemaConsulting.Plexus.Puppet;

/// <content>
///     Pipe commands: <c>pipe</c> and the friendly wrappers <c>info</c>, <c>pages</c>, <c>nodes</c>, <c>wires</c>,
///     <c>select</c>, <c>add-node</c>, <c>connect</c>, <c>undo</c>, <c>redo</c> and <c>render</c>.
/// </content>
internal static partial class Commands
{
    /// <summary>
    ///     Sends any automation pipe command and prints the raw JSON result.
    /// </summary>
    /// <param name="parsed">The parsed command: the pipe command and optional JSON arguments.</param>
    /// <returns>The exit code.</returns>
    internal static int Pipe(ParsedCommand parsed)
    {
        RequireArgs(parsed, 1, "plexus-puppet pipe <cmd> [<json-args>] [--instance <alias>]");
        JsonObject? arguments = null;
        if (parsed.Positional.Count > 1)
        {
            arguments = ParseArguments(parsed.Positional[1]);
        }

        var result = Call(ValidAlias(parsed), parsed.Positional[0], arguments);
        return Ok(result?.ToJsonString() ?? "null");
    }

    /// <summary>
    ///     Parses the JSON arguments of <c>pipe</c>, forgiving the backslash-escaped quotes some shells leave in.
    /// </summary>
    /// <remarks>
    ///     Depending on the shell and its native-argument settings, <c>'{\"names\":[\"A\"]}'</c> reaches the Puppet with the
    ///     backslashes intact. Such text can never be valid JSON on its own, so it is retried with the escapes removed.
    /// </remarks>
    /// <param name="text">The argument text.</param>
    /// <returns>The arguments object.</returns>
    /// <exception cref="PuppetException">Thrown when the text is not a JSON object.</exception>
    internal static JsonObject ParseArguments(string text)
    {
        try
        {
            return JsonNode.Parse(text) as JsonObject
                ?? throw PuppetException.Usage("The pipe arguments must be a JSON object, for example '{\"names\":[\"A\"]}'.");
        }
        catch (JsonException) when (text.Contains("\\\"", StringComparison.Ordinal))
        {
            return ParseArguments(text.Replace("\\\"", "\"", StringComparison.Ordinal));
        }
        catch (JsonException ex)
        {
            throw PuppetException.Usage($"The pipe arguments are not valid JSON: {ex.Message}");
        }
    }

    /// <summary>
    ///     Prints the instance's state (title, file, dirty flag, undo texts, pages, zoom, selection, status).
    /// </summary>
    /// <param name="parsed">The parsed command.</param>
    /// <returns>The exit code.</returns>
    internal static int Info(ParsedCommand parsed) => Wrapped(parsed, "info", null);

    /// <summary>
    ///     Lists the pages with their element counts.
    /// </summary>
    /// <param name="parsed">The parsed command.</param>
    /// <returns>The exit code.</returns>
    internal static int Pages(ParsedCommand parsed) => Wrapped(parsed, "list-pages", null);

    /// <summary>
    ///     Lists the nodes of a page.
    /// </summary>
    /// <param name="parsed">The parsed command: <c>--page</c>, <c>--ports</c>.</param>
    /// <returns>The exit code.</returns>
    internal static int Nodes(ParsedCommand parsed) => Wrapped(parsed, "list-nodes", PageArgument(parsed));

    /// <summary>
    ///     Lists the wires of a page.
    /// </summary>
    /// <param name="parsed">The parsed command: <c>--page</c>, <c>--ports</c> (keep the routes).</param>
    /// <returns>The exit code.</returns>
    internal static int Wires(ParsedCommand parsed) => Wrapped(parsed, "list-wires", PageArgument(parsed));

    /// <summary>
    ///     Selects nodes (or groups) by name.
    /// </summary>
    /// <param name="parsed">The parsed command: the names and <c>--add</c>.</param>
    /// <returns>The exit code.</returns>
    internal static int Select(ParsedCommand parsed)
    {
        RequireArgs(parsed, 1, "plexus-puppet select <name>... [--add] [--instance <alias>]");
        var arguments = new JsonObject { ["names"] = new JsonArray([.. parsed.Positional.Select(n => (JsonNode?)JsonValue.Create(n))]) };
        if (parsed.HasOption("add"))
        {
            arguments["add"] = true;
        }

        return Wrapped(parsed, "select", arguments);
    }

    /// <summary>
    ///     Adds a node at diagram coordinates.
    /// </summary>
    /// <param name="parsed">The parsed command: the type key, x, y and an optional name.</param>
    /// <returns>The exit code.</returns>
    internal static int AddNode(ParsedCommand parsed)
    {
        RequireArgs(parsed, 3, "plexus-puppet add-node <type, e.g. Mixer or demo.audio.Mixer> <x> <y> [name] [--instance <alias>]");
        var type = parsed.Positional[0];
        var arguments = new JsonObject
        {
            ["type"] = type,
            ["x"] = Number(parsed.Positional[1], "x"),
            ["y"] = Number(parsed.Positional[2], "y"),
        };
        if (parsed.Positional.Count > 3)
        {
            arguments["name"] = parsed.Positional[3];
        }

        return Wrapped(parsed, "add-node", arguments);
    }

    /// <summary>
    ///     Connects two ports by name through the model (no mouse).
    /// </summary>
    /// <param name="parsed">The parsed command: <c>Node.port</c> twice.</param>
    /// <returns>The exit code.</returns>
    internal static int Connect(ParsedCommand parsed)
    {
        RequireArgs(parsed, 2, "plexus-puppet connect <Node.port> <Node.port> [--instance <alias>]");
        return Wrapped(parsed, "connect", new JsonObject { ["from"] = parsed.Positional[0], ["to"] = parsed.Positional[1] });
    }

    /// <summary>
    ///     Undoes the last change (or cancels the gesture or inline edit in flight), like Ctrl+Z.
    /// </summary>
    /// <param name="parsed">The parsed command.</param>
    /// <returns>The exit code.</returns>
    internal static int Undo(ParsedCommand parsed) => Wrapped(parsed, "undo", null);

    /// <summary>
    ///     Redoes the last undone change.
    /// </summary>
    /// <param name="parsed">The parsed command.</param>
    /// <returns>The exit code.</returns>
    internal static int Redo(ParsedCommand parsed) => Wrapped(parsed, "redo", null);

    /// <summary>
    ///     Renders a page to a PNG through the library's renderer.
    /// </summary>
    /// <param name="parsed">The parsed command: the output path, <c>--page</c>, <c>--scale</c>.</param>
    /// <returns>The exit code.</returns>
    internal static int Render(ParsedCommand parsed)
    {
        RequireArgs(parsed, 1, "plexus-puppet render <path.png> [--page <index|name>] [--scale <s>] [--instance <alias>]");
        var path = Path.GetFullPath(parsed.Positional[0]);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? Directory.GetCurrentDirectory());
        var arguments = PageArgument(parsed) ?? [];
        arguments["path"] = path;
        if (parsed.GetOption("scale") is { } scale)
        {
            arguments["scale"] = Number(scale, "--scale");
        }

        return Wrapped(parsed, "render", arguments);
    }

    /// <summary>
    ///     Sends a pipe command and prints its result as plain text.
    /// </summary>
    /// <param name="parsed">The parsed command (for the instance and <c>--ports</c>).</param>
    /// <param name="command">The pipe command.</param>
    /// <param name="arguments">The arguments, or <see langword="null"/>.</param>
    /// <returns>The exit code.</returns>
    private static int Wrapped(ParsedCommand parsed, string command, JsonObject? arguments)
    {
        var result = Call(ValidAlias(parsed), command, arguments);
        return Ok(PipeFormat.Result(command, result, parsed.HasOption("ports")));
    }

    /// <summary>
    ///     Builds the <c>page</c> argument from <c>--page</c> (an index or a name).
    /// </summary>
    /// <param name="parsed">The parsed command.</param>
    /// <returns>The arguments, or <see langword="null"/> without <c>--page</c>.</returns>
    private static JsonObject? PageArgument(ParsedCommand parsed)
    {
        if (parsed.GetOption("page") is not { } page)
        {
            return null;
        }

        return new JsonObject
        {
            ["page"] = int.TryParse(page, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) ? JsonValue.Create(index) : JsonValue.Create(page),
        };
    }
}

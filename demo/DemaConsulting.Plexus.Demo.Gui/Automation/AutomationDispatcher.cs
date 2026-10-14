using System.Text.Json;
using System.Text.Json.Nodes;

namespace DemaConsulting.Plexus.Demo.Gui.Automation;

/// <summary>
///     The reply to one automation request.
/// </summary>
/// <param name="Json">The one-line JSON reply (<c>{"ok":true,"result":...}</c> or <c>{"ok":false,"error":"..."}</c>).</param>
/// <param name="Quit">True when the application should exit after the reply has been sent.</param>
internal sealed record AutomationResponse(string Json, bool Quit);

/// <summary>
///     Executes automation commands against a <see cref="DemoSession"/>; the transport-independent half of the automation endpoint.
/// </summary>
/// <remarks>
///     DEMO ONLY. A request is one JSON object <c>{"cmd":"...", ...arguments}</c>; the reply is one
///     JSON line. The dispatcher knows nothing about pipes, which keeps it testable: tests call
///     <see cref="ExecuteAsync"/> directly. All commands must run on the UI thread (the model and
///     the controls are single-threaded); the pipe server marshals each request there.
///     Errors never escape: any failure becomes an <c>{"ok":false}</c> reply.
/// </remarks>
internal sealed partial class AutomationDispatcher
{
    /// <summary>
    ///     A command implementation: reads arguments from the request and returns the result tree.
    /// </summary>
    /// <param name="request">The request object.</param>
    /// <returns>A task producing the result, which may be null for "no result".</returns>
    private delegate Task<JsonNode?> Command(JsonObject request);

    /// <summary>
    ///     How replies are written: compact (one line) and without escaping quotes and other harmless characters, so messages stay readable.
    /// </summary>
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    ///     The commands by name.
    /// </summary>
    private readonly Dictionary<string, Command> _commands = new(StringComparer.Ordinal);

    /// <summary>
    ///     Initializes a new instance of the <see cref="AutomationDispatcher"/> class.
    /// </summary>
    /// <param name="session">The session the commands operate on.</param>
    public AutomationDispatcher(DemoSession session)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        RegisterQueryCommands();
        RegisterPageCommands();
        RegisterEditCommands();
        RegisterFileCommands();
        RegisterViewCommands();
    }

    /// <summary>
    ///     Gets the session the commands operate on.
    /// </summary>
    public DemoSession Session { get; }

    /// <summary>
    ///     Gets the names of all commands, sorted.
    /// </summary>
    public IReadOnlyList<string> CommandNames => [.. _commands.Keys.Order(StringComparer.Ordinal)];

    /// <summary>
    ///     Executes one request line. Call on the UI thread.
    /// </summary>
    /// <param name="requestLine">The request: one JSON object.</param>
    /// <returns>A task producing the reply; never faults.</returns>
    public async Task<AutomationResponse> ExecuteAsync(string requestLine)
    {
        try
        {
            if (JsonNode.Parse(requestLine) is not JsonObject request)
            {
                return Failure("The request must be a JSON object.");
            }

            var name = AutomationJson.RequireString(request, "cmd");
            if (name == "quit")
            {
                return new AutomationResponse(Success(new JsonObject { ["quitting"] = true }), Quit: true);
            }

            if (!_commands.TryGetValue(name, out var command))
            {
                return Failure($"Unknown command '{name}'. Known commands: {string.Join(", ", CommandNames)}, quit.");
            }

            return new AutomationResponse(Success(await command(request).ConfigureAwait(true)), Quit: false);
        }
        catch (JsonException ex)
        {
            return Failure($"Invalid JSON: {ex.Message}");
        }
        catch (System.Runtime.InteropServices.ExternalException ex)
        {
            // The operating system clipboard can be locked by another process or by a locked session
            return Failure(ClipboardMessage.Describe(ex));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException
                                       or KeyNotFoundException or InvalidCastException or NotSupportedException or SnapshotException
                                       or FormatException or OverflowException)
        {
            // Expected failures: bad arguments, rejected model operations, file problems. They are the caller's to fix.
            return Failure(ex.Message);
        }
        catch (Exception ex)
        {
            // The endpoint must never take the application down, whatever the model or a control throws
            return Failure($"{ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    ///     Builds a success reply.
    /// </summary>
    /// <param name="result">The result tree (may be null).</param>
    /// <returns>The reply line.</returns>
    private static string Success(JsonNode? result) =>
        new JsonObject { ["ok"] = true, ["result"] = result }.ToJsonString(WriteOptions);

    /// <summary>
    ///     Builds a failure reply.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <returns>The response.</returns>
    private static AutomationResponse Failure(string message) =>
        new(new JsonObject { ["ok"] = false, ["error"] = message }.ToJsonString(WriteOptions), Quit: false);

    /// <summary>
    ///     Registers a synchronous command.
    /// </summary>
    /// <param name="name">The command name.</param>
    /// <param name="handler">The implementation.</param>
    private void Add(string name, Func<JsonObject, JsonNode?> handler) =>
        _commands[name] = request => Task.FromResult(handler(request));

    /// <summary>
    ///     Registers an asynchronous command.
    /// </summary>
    /// <param name="name">The command name.</param>
    /// <param name="handler">The implementation.</param>
    private void AddAsync(string name, Func<JsonObject, Task<JsonNode?>> handler) =>
        _commands[name] = request => handler(request);

    /// <summary>
    ///     Finds an element by id, then by node name, then by group title.
    /// </summary>
    /// <param name="reference">An element id, a node name or a group title.</param>
    /// <returns>The element.</returns>
    /// <exception cref="ArgumentException">Thrown when nothing matches, or a title is ambiguous.</exception>
    private DiagramElement ResolveElement(string reference)
    {
        var diagram = Session.Diagram;
        if (diagram.FindElement(reference) is { } byId)
        {
            return byId;
        }

        if (diagram.FindNode(reference) is { } byName)
        {
            return byName;
        }

        var groups = diagram.Pages.SelectMany(p => p.Groups).Where(g => g.Title == reference).ToList();
        return groups.Count switch
        {
            1 => groups[0],
            0 => throw new ArgumentException($"No element with id or name '{reference}'."),
            _ => throw new ArgumentException($"The group title '{reference}' is ambiguous; use the group id."),
        };
    }

    /// <summary>
    ///     Resolves a required node argument (an id or a name).
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="argument">The argument name.</param>
    /// <returns>The node.</returns>
    /// <exception cref="ArgumentException">Thrown when the argument is missing or is not a node.</exception>
    private Node ResolveNode(JsonObject request, string argument = "node")
    {
        var reference = AutomationJson.RequireString(request, argument);
        return ResolveElement(reference) as Node ?? throw new ArgumentException($"'{reference}' is not a node.");
    }

    /// <summary>
    ///     Resolves the elements named by the <c>ids</c> and <c>names</c> arguments (and the single-value forms <c>id</c> and <c>name</c>).
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>The elements, without duplicates, at least one.</returns>
    /// <exception cref="ArgumentException">Thrown when none are given or one cannot be found.</exception>
    private List<DiagramElement> ResolveElements(JsonObject request)
    {
        var references = AutomationJson.StringList(request, "ids")
            .Concat(AutomationJson.StringList(request, "names"))
            .Concat(AutomationJson.StringList(request, "id"))
            .Concat(AutomationJson.StringList(request, "name"))
            .ToList();
        if (references.Count == 0)
        {
            throw new ArgumentException("Give 'ids' or 'names'.");
        }

        return [.. references.Select(ResolveElement).Distinct()];
    }

    /// <summary>
    ///     Makes sure the elements are on the page the editor shows, switching to their page when they are all on another one.
    /// </summary>
    /// <param name="elements">The elements.</param>
    /// <exception cref="ArgumentException">Thrown when the elements are on different pages.</exception>
    private void ShowPageOf(IReadOnlyList<DiagramElement> elements)
    {
        var pages = elements.Select(e => e.Page).Distinct().ToList();
        if (pages.Count != 1 || pages[0] is not { } page)
        {
            throw new ArgumentException("The elements must all be on one page.");
        }

        if (!ReferenceEquals(page, Session.CurrentPage))
        {
            Session.SelectPage(page);
        }
    }

    /// <summary>
    ///     Resolves the elements of a request, shows their page and makes them the editor selection.
    /// </summary>
    /// <param name="request">The request.</param>
    private void SelectRequested(JsonObject request)
    {
        var elements = ResolveElements(request);
        ShowPageOf(elements);
        Session.Editor.Select(elements);
    }

    /// <summary>
    ///     Resolves an optional <c>page</c> argument (index or name), defaulting to the shown page.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="argument">The argument name.</param>
    /// <returns>The page.</returns>
    private DiagramPage ResolvePage(JsonObject request, string argument = "page") =>
        AutomationJson.OptionalString(request, argument) is { } selector ? Session.FindPage(selector) : Session.RequirePage();

    /// <summary>
    ///     Resolves a node type reference: a full key (<c>demo.audio.Mixer</c>) or its short name (<c>Mixer</c>).
    /// </summary>
    /// <param name="reference">The reference.</param>
    /// <returns>
    ///     The full key of the single registered type the reference names; the reference unchanged when it
    ///     names none or several, so that the usual "unknown node type" failure reports it.
    /// </returns>
    private string ResolveTypeKey(string reference)
    {
        var types = Session.Diagram.Types;
        if (types.Contains(reference))
        {
            return reference;
        }

        var matches = types.Where(t => string.Equals(AutomationIds.ShortName(t.Key), reference, StringComparison.OrdinalIgnoreCase)).ToList();
        return matches.Count == 1 ? matches[0].Key : reference;
    }

    /// <summary>
    ///     Resolves a port reference <c>"Node.port"</c> (the node is a name or an id) through the library's port lookup.
    /// </summary>
    /// <param name="reference">The reference.</param>
    /// <param name="preferOutput">Whether an output wins when both directions share the port name.</param>
    /// <param name="direction">An explicit direction (<c>in</c> or <c>out</c>) that overrides the preference, or null.</param>
    /// <returns>The port.</returns>
    /// <exception cref="ArgumentException">Thrown when the direction is invalid or the reference names no port.</exception>
    private Port ResolvePort(string reference, bool preferOutput, string? direction = null)
    {
        PortDirection? wanted = direction?.ToLowerInvariant() switch
        {
            "in" or "input" => PortDirection.Input,
            "out" or "output" => PortDirection.Output,
            null => null,
            _ => throw new ArgumentException("The port direction must be 'in' or 'out'."),
        };

        // An explicit direction is exact; otherwise prefer one side and fall back to the other
        var preferred = wanted ?? (preferOutput ? PortDirection.Output : PortDirection.Input);
        return Session.Diagram.FindPort(reference, preferred)
            ?? (wanted is null ? Session.Diagram.FindPort(reference) : null)
            ?? throw new ArgumentException($"'{reference}' does not name a {direction ?? "matching"} port; use \"Node.port\".");
    }
    /// <summary>
    ///     Gets the kind name of an element.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <returns><c>node</c>, <c>wire</c>, <c>annotation</c> or <c>group</c>.</returns>
    private static string KindOf(DiagramElement element) => element switch
    {
        Node => "node",
        Wire => "wire",
        Annotation => "annotation",
        Group => "group",
        _ => "element",
    };
}

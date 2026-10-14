using System.IO.Pipes;
using System.Text;
using System.Text.Json.Nodes;

namespace DemaConsulting.Plexus.Puppet;

/// <summary>
///     The reply of the demo's automation endpoint to one request.
/// </summary>
/// <param name="Ok">Whether the command succeeded.</param>
/// <param name="Result">The <c>result</c> value when it succeeded.</param>
/// <param name="Error">The <c>error</c> text when it failed.</param>
internal sealed record PipeReply(bool Ok, JsonNode? Result, string? Error)
{
    /// <summary>
    ///     Parses a reply line.
    /// </summary>
    /// <param name="line">The JSON line, <c>{"ok":true,"result":...}</c> or <c>{"ok":false,"error":"..."}</c>.</param>
    /// <returns>The reply.</returns>
    /// <exception cref="FormatException">Thrown when the line is not a reply object.</exception>
    internal static PipeReply Parse(string line)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(line);
        }
        catch (System.Text.Json.JsonException ex)
        {
            throw new FormatException($"The reply is not JSON: {ex.Message}", ex);
        }

        if (node is not JsonObject reply || reply["ok"] is not JsonValue ok || !ok.TryGetValue<bool>(out var success))
        {
            throw new FormatException("The reply is not an object with an 'ok' flag.");
        }

        return new PipeReply(success, reply["result"]?.DeepClone(), reply["error"]?.GetValue<string>());
    }
}

/// <summary>
///     A client of the demo's automation named pipe: one connection, one request line, one reply line.
/// </summary>
internal static class PipeClient
{
    /// <summary>
    ///     How long to wait for the pipe to accept a connection.
    /// </summary>
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(3);

    /// <summary>
    ///     How long to wait for a reply (a render or paste can take a moment).
    /// </summary>
    private static readonly TimeSpan ReplyTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    ///     Builds the request line of a command.
    /// </summary>
    /// <param name="command">The command name, for example <c>list-nodes</c>.</param>
    /// <param name="arguments">The arguments to merge into the request, or <see langword="null"/>.</param>
    /// <returns>The compact JSON request.</returns>
    internal static string BuildRequest(string command, JsonObject? arguments)
    {
        var request = new JsonObject { ["cmd"] = command };
        if (arguments is not null)
        {
            foreach (var (name, value) in arguments)
            {
                request[name] = value?.DeepClone();
            }
        }

        return request.ToJsonString();
    }

    /// <summary>
    ///     Sends one command to an instance and waits for the reply.
    /// </summary>
    /// <param name="alias">The instance alias.</param>
    /// <param name="command">The command name.</param>
    /// <param name="arguments">The command arguments, or <see langword="null"/>.</param>
    /// <returns>The reply.</returns>
    /// <exception cref="PipeUnavailableException">Thrown when the pipe does not exist or does not answer in time.</exception>
    internal static PipeReply Send(string alias, string command, JsonObject? arguments = null)
    {
        using var pipe = new NamedPipeClientStream(".", PipeNames.ForAlias(alias), PipeDirection.InOut, PipeOptions.CurrentUserOnly);
        try
        {
            pipe.Connect((int)ConnectTimeout.TotalMilliseconds);
        }
        catch (TimeoutException ex)
        {
            throw new PipeUnavailableException($"Instance '{alias}' is not running (no pipe {PipeNames.ForAlias(alias)}).", ex);
        }
        catch (IOException ex)
        {
            throw new PipeUnavailableException($"Instance '{alias}' is not reachable: {ex.Message}", ex);
        }

        using var cancel = new CancellationTokenSource(ReplyTimeout);
        try
        {
            var line = Exchange(pipe, BuildRequest(command, arguments), cancel.Token);
            return PipeReply.Parse(line);
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or FormatException)
        {
            throw new PipeUnavailableException($"Instance '{alias}' did not answer '{command}': {ex.Message}", ex);
        }
    }

    /// <summary>
    ///     Writes the request line and reads the reply line.
    /// </summary>
    /// <param name="pipe">The connected pipe.</param>
    /// <param name="request">The request line.</param>
    /// <param name="token">Cancels the wait for the reply.</param>
    /// <returns>The reply line.</returns>
    private static string Exchange(NamedPipeClientStream pipe, string request, CancellationToken token)
    {
        var utf8 = new UTF8Encoding(false);
        using var writer = new StreamWriter(pipe, utf8, 4096, leaveOpen: true) { AutoFlush = true, NewLine = "\n" };
        using var reader = new StreamReader(pipe, utf8, false, 4096, leaveOpen: true);
        writer.WriteLine(request);
        var read = reader.ReadLineAsync(token).AsTask();
        return read.GetAwaiter().GetResult() ?? throw new IOException("The pipe closed without a reply.");
    }
}

/// <summary>
///     Raised when an instance's automation pipe is missing, refuses the connection or does not answer.
/// </summary>
public sealed class PipeUnavailableException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="PipeUnavailableException"/> class.
    /// </summary>
    /// <param name="message">What went wrong.</param>
    /// <param name="inner">The underlying failure.</param>
    internal PipeUnavailableException(string message, Exception inner)
        : base(message, inner)
    {
    }
}

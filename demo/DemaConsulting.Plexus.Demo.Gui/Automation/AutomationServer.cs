using System.IO.Pipes;
using System.Text;

namespace DemaConsulting.Plexus.Demo.Gui.Automation;

/// <summary>
///     The named-pipe transport of the automation endpoint (DEMO ONLY).
/// </summary>
/// <remarks>
///     Serves the pipe <c>plexus-demo-{name}</c>: local machine only and restricted to the current
///     user (<see cref="PipeOptions.CurrentUserOnly"/> makes .NET apply a current-user-only security
///     descriptor, and Windows pipes are never reachable from other machines through this API).
///     Protocol: newline-delimited UTF-8 JSON; each request line is answered by exactly one reply line.
///     Clients may connect one after another; one client is served at a time. The transport only moves
///     lines: executing a command is delegated to the <c>executor</c>, which in the application hops
///     to the UI thread, and in tests can call the dispatcher directly.
/// </remarks>
internal sealed class AutomationServer : IDisposable
{
    /// <summary>
    ///     The prefix every automation pipe name starts with.
    /// </summary>
    public const string PipePrefix = "plexus-demo-";

    /// <summary>
    ///     Cancels the accept loop on disposal.
    /// </summary>
    private readonly CancellationTokenSource _cancel = new();

    /// <summary>
    ///     Runs one request line and produces the reply.
    /// </summary>
    private readonly Func<string, Task<AutomationResponse>> _executor;

    /// <summary>
    ///     Called after a reply that asked the application to quit has been sent.
    /// </summary>
    private readonly Action _onQuit;

    /// <summary>
    ///     The accept loop, or null before <see cref="Start"/>.
    /// </summary>
    private Task? _loop;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AutomationServer"/> class.
    /// </summary>
    /// <param name="name">The automation name; the pipe is <c>plexus-demo-{name}</c>.</param>
    /// <param name="executor">Executes one request line (on the right thread) and produces the reply.</param>
    /// <param name="onQuit">Called after a quit reply was sent.</param>
    public AutomationServer(string name, Func<string, Task<AutomationResponse>> executor, Action onQuit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        PipeName = PipePrefix + name;
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _onQuit = onQuit ?? throw new ArgumentNullException(nameof(onQuit));
    }

    /// <summary>
    ///     Gets the pipe name (without the <c>\\.\pipe\</c> prefix).
    /// </summary>
    public string PipeName { get; }

    /// <summary>
    ///     Starts accepting connections in the background.
    /// </summary>
    public void Start() => _loop ??= Task.Run(() => AcceptLoopAsync(_cancel.Token), _cancel.Token);

    /// <summary>
    ///     Stops the server and waits briefly for the accept loop to finish.
    /// </summary>
    public void Dispose()
    {
        _cancel.Cancel();
        try
        {
            _loop?.Wait(TimeSpan.FromSeconds(2), CancellationToken.None);
        }
        catch (AggregateException)
        {
            // The loop ended by cancellation or an I/O failure; either way the server is stopped
        }

        _cancel.Dispose();
    }

    /// <summary>
    ///     Accepts clients one after another until cancelled.
    /// </summary>
    /// <param name="token">The cancellation token.</param>
    /// <returns>A task that completes when the server stops.</returns>
    private async Task AcceptLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                // A fresh server instance per client; the current-user-only option restricts who may connect
                await using var pipe = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(token).ConfigureAwait(false);
                if (await ServeAsync(pipe, token).ConfigureAwait(false))
                {
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (IOException)
            {
                // A client went away mid-request; the next client may connect
            }
        }
    }

    /// <summary>
    ///     Serves one connected client until it disconnects.
    /// </summary>
    /// <param name="pipe">The connected pipe.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>True when a quit request ended the session.</returns>
    private async Task<bool> ServeAsync(NamedPipeServerStream pipe, CancellationToken token)
    {
        var utf8 = new UTF8Encoding(false);
        using var reader = new StreamReader(pipe, utf8, false, 4096, leaveOpen: true);
        using var writer = new StreamWriter(pipe, utf8, 4096, leaveOpen: true) { AutoFlush = true, NewLine = "\n" };
        while (await reader.ReadLineAsync(token).ConfigureAwait(false) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var response = await _executor(line).ConfigureAwait(false);
            await writer.WriteLineAsync(response.Json.AsMemory(), token).ConfigureAwait(false);
            if (response.Quit)
            {
                _onQuit();
                return true;
            }
        }

        return false;
    }
}

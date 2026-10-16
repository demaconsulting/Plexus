using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using DemaConsulting.Plexus.Avalonia;
using DemaConsulting.Plexus.Demo.Gui;
using DemaConsulting.Plexus.Demo.Gui.Automation;

namespace DemaConsulting.Plexus.Demo.Tests;

/// <summary>
///     Runs GUI test bodies on the headless Avalonia UI thread.
/// </summary>
/// <remarks>
///     Avalonia controls may only be touched on the UI thread. This helper owns one headless session
///     for the whole test run and executes each body on that session's UI thread, rethrowing
///     failures so xUnit reports them normally. It stands in for <c>[AvaloniaFact]</c> of
///     Avalonia.Headless.XUnit, which does not work with xunit.v3 4.x.
/// </remarks>
internal static class GuiTest
{
    /// <summary>
    ///     The shared headless session.
    /// </summary>
    private static readonly HeadlessUnitTestSession Session = HeadlessUnitTestSession.StartNew(typeof(GuiTestAppBuilder));

    /// <summary>
    ///     Runs a synchronous body on the UI thread.
    /// </summary>
    /// <param name="body">The test body.</param>
    /// <returns>A task that completes when the body has finished; return it from the test.</returns>
    public static Task Run(Action body) => Session.Dispatch(body, CancellationToken.None);

    /// <summary>
    ///     Runs an asynchronous body on the UI thread (for bodies that build their own window, such as a
    ///     <c>MainWindowHost</c>).
    /// </summary>
    /// <param name="body">The test body.</param>
    /// <returns>A task that completes when the body has finished; return it from the test.</returns>
    public static Task RunAsync(Func<Task> body) => Session.Dispatch(
        async () =>
        {
            await body().ConfigureAwait(true);
            return 0;
        },
        CancellationToken.None);

    /// <summary>
    ///     Runs an asynchronous body on the UI thread with a fresh <see cref="GuiHost"/>.
    /// </summary>
    /// <param name="body">The test body; the host is disposed afterwards.</param>
    /// <param name="typeSet">The node type set of the host.</param>
    /// <returns>A task that completes when the body has finished; return it from the test.</returns>
    public static Task RunAsync(Func<GuiHost, Task> body, NodeTypeSet typeSet = NodeTypeSet.Full) => Session.Dispatch(
        async () =>
        {
            using var host = new GuiHost(typeSet);
            await body(host).ConfigureAwait(true);
            return 0;
        },
        CancellationToken.None);
}

/// <summary>
///     A clipboard that keeps its content in memory, so tests do not depend on the machine's clipboard.
/// </summary>
public sealed class MemoryClipboard : IDiagramClipboard
{
    /// <summary>
    ///     Gets or sets the text the clipboard holds.
    /// </summary>
    public string? Text { get; set; }

    /// <inheritdoc />
    public Task SetAsync(string json)
    {
        Text = json;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<string?> GetAsync() => Task.FromResult(Text);
}

/// <summary>
///     A demo session and automation dispatcher around a real <see cref="DiagramEditor"/> in a headless window.
/// </summary>
/// <remarks>
///     The editor fills an 800x600 window, so editor coordinates are window coordinates.
///     The clipboard is private to the host (a <see cref="MemoryClipboard"/>).
/// </remarks>
internal sealed class GuiHost : IDisposable
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="GuiHost"/> class.
    /// </summary>
    /// <param name="typeSet">The node type set.</param>
    public GuiHost(NodeTypeSet typeSet)
    {
        Clipboard = new MemoryClipboard();
        Editor = new DiagramEditor { Clipboard = Clipboard };
        Window = new Window { Width = 800, Height = 600, Content = Editor };
        Window.Show();
        Session = new DemoSession(Editor, typeSet);
        Commands = new AutomationDispatcher(Session);
        Flush();
    }

    /// <summary>
    ///     Gets the in-memory clipboard.
    /// </summary>
    public MemoryClipboard Clipboard { get; }

    /// <summary>
    ///     Gets the editor.
    /// </summary>
    public DiagramEditor Editor { get; }

    /// <summary>
    ///     Gets the window holding the editor.
    /// </summary>
    public Window Window { get; }

    /// <summary>
    ///     Gets the document session.
    /// </summary>
    public DemoSession Session { get; }

    /// <summary>
    ///     Gets the automation dispatcher under test.
    /// </summary>
    public AutomationDispatcher Commands { get; }

    /// <summary>
    ///     Lets pending layout, input and rendering work finish.
    /// </summary>
    public void Flush()
    {
        Window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }

    /// <summary>
    ///     Sends a request and parses the reply.
    /// </summary>
    /// <param name="json">The request line.</param>
    /// <returns>The reply object.</returns>
    public async Task<JsonObject> SendAsync(string json)
    {
        var response = await Commands.ExecuteAsync(json).ConfigureAwait(true);
        Flush();
        return (JsonObject)JsonNode.Parse(response.Json)!;
    }

    /// <summary>
    ///     Sends a request that must succeed and returns its result.
    /// </summary>
    /// <param name="json">The request line.</param>
    /// <returns>The <c>result</c> node.</returns>
    public async Task<JsonNode> OkAsync(string json)
    {
        var reply = await SendAsync(json).ConfigureAwait(true);
        Assert.True(reply["ok"]!.GetValue<bool>(), $"{json} failed: {reply["error"]}");
        return reply["result"]!;
    }

    /// <summary>
    ///     Sends a request that must fail and returns the error text.
    /// </summary>
    /// <param name="json">The request line.</param>
    /// <returns>The <c>error</c> text.</returns>
    public async Task<string> ErrorAsync(string json)
    {
        var reply = await SendAsync(json).ConfigureAwait(true);
        Assert.False(reply["ok"]!.GetValue<bool>(), $"{json} unexpectedly succeeded");
        return reply["error"]!.GetValue<string>();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Window.Close();
        GC.SuppressFinalize(this);
    }
}

using System.Diagnostics;
using DemaConsulting.Plexus.Puppet.Automation;

namespace DemaConsulting.Plexus.Puppet;

/// <content>
///     Instance management: <c>launch</c>, <c>instances</c> and <c>quit</c>.
/// </content>
internal static partial class Commands
{
    /// <summary>
    ///     How long a launched instance gets to answer its pipe and show its window.
    /// </summary>
    private static readonly TimeSpan LaunchTimeout = TimeSpan.FromSeconds(45);

    /// <summary>
    ///     How long a quit instance gets to exit.
    /// </summary>
    private static readonly TimeSpan QuitTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    ///     Starts a <c>plexus-demo</c> instance with an automation pipe and waits until it is ready.
    /// </summary>
    /// <param name="parsed">The parsed command: <c>--instance</c>, <c>--sample</c> or <c>--open</c>, <c>--types</c>, <c>--exe</c>, <c>--allow-existing</c>.</param>
    /// <returns>The exit code.</returns>
    internal static int Launch(ParsedCommand parsed)
    {
        if (!LaunchOptions.TryBuild(parsed, out var arguments, out var optionError))
        {
            throw PuppetException.Usage(optionError);
        }

        var alias = parsed.Instance;
        if (!parsed.HasOption("allow-existing") && DemoProcess.TryGetPid(alias, out var existing))
        {
            throw new PuppetException(ExitCodes.LaunchFailed, "LAUNCH_FAILED", $"Instance '{alias}' is already running (pid={existing}). Use quit first, another --instance alias, or --allow-existing.");
        }

        if (!DemoProcess.TryResolveExePath(parsed.GetOption("exe"), out var exe, out var resolveError))
        {
            throw new PuppetException(ExitCodes.LaunchFailed, "LAUNCH_FAILED", resolveError);
        }

        // Shell execution gives the demo no inherited standard handles: a caller that captures the Puppet's output
        // (a pipeline, a build log) would otherwise wait for the demo to exit before it saw the end of the output.
        var startInfo = new ProcessStartInfo(exe)
        {
            UseShellExecute = true,
            WorkingDirectory = Directory.GetCurrentDirectory(),
            Arguments = LaunchOptions.JoinArguments(arguments),
        };
        using var process = Process.Start(startInfo)
            ?? throw new PuppetException(ExitCodes.LaunchFailed, "LAUNCH_FAILED", $"Starting '{exe}' returned no process.");

        // The id is read once: FlaUI takes over (and may dispose) the Process object it attaches to
        var pid = process.Id;
        try
        {
            var title = WaitUntilReady(alias, process, pid);
            return Ok($"OK instance={alias} pid={pid} exe=\"{exe}\" title=\"{title}\"");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // A half-started instance would hold the alias and a window; do not leave it behind
            KillQuietly(pid);
            throw;
        }
    }

    /// <summary>
    ///     Lists the instances whose automation pipe exists, with what they answer to <c>info</c>.
    /// </summary>
    /// <returns>The exit code (always success; an empty list is not an error).</returns>
    internal static int Instances()
    {
        var aliases = DemoProcess.ListPipeAliases();
        if (aliases.Count == 0)
        {
            return Ok("NO_INSTANCES");
        }

        foreach (var alias in aliases)
        {
            try
            {
                var info = PipeClient.Send(alias, "info");
                var result = info.Result;
                Console.WriteLine(
                    $"INSTANCE alias={alias} pid={PipeFormat.Value(result?["pid"])} title={PipeFormat.Value(result?["title"])} " +
                    $"file={PipeFormat.Value(result?["file"])} dirty={PipeFormat.Value(result?["dirty"])} types={PipeFormat.Value(result?["typeSet"])}");
            }
            catch (PipeUnavailableException ex)
            {
                Console.WriteLine($"INSTANCE alias={alias} UNRESPONSIVE reason=\"{ex.Message}\"");
            }
        }

        return ExitCodes.Success;
    }

    /// <summary>
    ///     Asks an instance to quit and waits for the process to exit.
    /// </summary>
    /// <param name="parsed">The parsed command: <c>--instance</c>, <c>--kill</c>.</param>
    /// <returns>The exit code.</returns>
    internal static int Quit(ParsedCommand parsed)
    {
        var alias = ValidAlias(parsed);
        if (!DemoProcess.TryGetPid(alias, out var pid))
        {
            throw new PuppetException(ExitCodes.NotRunning, "NOT_RUNNING", $"Instance '{alias}' is not running.");
        }

        _ = Call(alias, "quit");
        if (WaitForExit(pid, QuitTimeout))
        {
            return Ok($"OK quit instance={alias} pid={pid}");
        }

        if (parsed.HasOption("kill"))
        {
            KillQuietly(pid);
            return Ok($"OK killed instance={alias} pid={pid}");
        }

        throw new PuppetException(ExitCodes.Timeout, "TIMEOUT", $"pid={pid} did not exit after the quit request. Use --kill to end it.");
    }

    /// <summary>
    ///     Waits until the instance answers <c>ping</c> and shows its window.
    /// </summary>
    /// <param name="alias">The instance alias.</param>
    /// <param name="process">The launched process (to notice an early exit).</param>
    /// <param name="pid">The process id.</param>
    /// <returns>The window title.</returns>
    /// <exception cref="PuppetException">Thrown when the process exits early or the time runs out.</exception>
    private static string WaitUntilReady(string alias, Process process, int pid)
    {
        var deadline = DateTime.UtcNow + LaunchTimeout;
        while (DateTime.UtcNow < deadline)
        {
            if (process.HasExited)
            {
                throw new PuppetException(ExitCodes.LaunchFailed, "LAUNCH_FAILED", $"plexus-demo exited with code {process.ExitCode} before it was ready.");
            }

            if (TryPing(alias) && TryReadTitle(alias, pid) is { } title)
            {
                return title;
            }

            Thread.Sleep(250);
        }

        throw new PuppetException(ExitCodes.LaunchFailed, "LAUNCH_FAILED", $"Instance '{alias}' (pid={pid}) was not ready within {LaunchTimeout.TotalSeconds:0}s.");
    }

    /// <summary>
    ///     Reads the main window title once the window is visible.
    /// </summary>
    /// <param name="alias">The instance alias.</param>
    /// <param name="pid">The process id.</param>
    /// <returns>The title, or <see langword="null"/> while the window is not there or not visible yet.</returns>
    private static string? TryReadTitle(string alias, int pid)
    {
        // A fresh Process object per attempt: FlaUI disposes the one it is given
        using var process = Process.GetProcessById(pid);
        using var context = AttachContext.TryAttach(alias, process, out _);
        if (context is null || UiActions.Safe(() => context.MainWindow.Properties.IsOffscreen.ValueOrDefault, true))
        {
            return null;
        }

        return context.MainWindow.Title;
    }

    /// <summary>
    ///     Tests whether an instance answers <c>ping</c>.
    /// </summary>
    /// <param name="alias">The instance alias.</param>
    /// <returns><see langword="true"/> when it answers.</returns>
    private static bool TryPing(string alias)
    {
        try
        {
            return PipeClient.Send(alias, "ping").Ok;
        }
        catch (PipeUnavailableException)
        {
            return false;
        }
    }

    /// <summary>
    ///     Waits for a process to exit.
    /// </summary>
    /// <param name="pid">The process id.</param>
    /// <param name="timeout">How long to wait.</param>
    /// <returns><see langword="true"/> when the process is gone.</returns>
    private static bool WaitForExit(int pid, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                if (process.HasExited)
                {
                    return true;
                }
            }
            catch (ArgumentException)
            {
                return true;
            }

            Thread.Sleep(100);
        }

        return false;
    }

    /// <summary>
    ///     Ends a process, ignoring the case that it is already gone.
    /// </summary>
    /// <param name="pid">The process id.</param>
    private static void KillQuietly(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or System.ComponentModel.Win32Exception)
        {
            // Already exited: that is what was wanted
        }
    }
}

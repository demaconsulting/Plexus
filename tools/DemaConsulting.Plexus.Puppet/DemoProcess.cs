using System.Diagnostics;

namespace DemaConsulting.Plexus.Puppet;

/// <summary>
///     Builds the command line of <c>plexus-demo</c> for the Puppet's <c>launch</c> command.
/// </summary>
internal static class LaunchOptions
{
    /// <summary>
    ///     Translates the options of <c>launch</c> into the arguments of <c>plexus-demo</c>.
    /// </summary>
    /// <param name="parsed">The parsed <c>launch</c> command.</param>
    /// <param name="arguments">The demo's arguments, ending with <c>--automation &lt;alias&gt;</c>.</param>
    /// <param name="error">Why the options were rejected when the result is <see langword="false"/>.</param>
    /// <returns><see langword="true"/> when the options are valid.</returns>
    internal static bool TryBuild(ParsedCommand parsed, out IReadOnlyList<string> arguments, out string error)
    {
        var list = new List<string>();
        arguments = list;
        error = string.Empty;

        if (!PipeNames.IsValidAlias(parsed.Instance))
        {
            error = $"'{parsed.Instance}' is not a valid instance alias (letters, digits, '-' and '_').";
            return false;
        }

        var sample = parsed.GetOption("sample");
        var open = parsed.GetOption("open");
        if (parsed.HasOption("sample") && parsed.HasOption("open"))
        {
            error = "Give --sample or --open, not both.";
            return false;
        }

        if ((parsed.HasOption("sample") && string.IsNullOrWhiteSpace(sample)) || (parsed.HasOption("open") && string.IsNullOrWhiteSpace(open)))
        {
            error = "--sample and --open need a value.";
            return false;
        }

        if (sample is not null)
        {
            list.AddRange(["--sample", sample]);
        }

        if (open is not null)
        {
            list.AddRange(["--open", Path.GetFullPath(open)]);
        }

        if (parsed.HasOption("types"))
        {
            var types = parsed.GetOption("types");
            if (types is not ("full" or "basic"))
            {
                error = "--types must be 'full' or 'basic'.";
                return false;
            }

            list.AddRange(["--types", types]);
        }

        list.AddRange(["--automation", parsed.Instance]);
        return true;
    }

    /// <summary>
    ///     Joins arguments into one Windows command-line string, quoting those that need it.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The command-line text.</returns>
    internal static string JoinArguments(IEnumerable<string> arguments) => string.Join(' ', arguments.Select(Quote));

    /// <summary>
    ///     Quotes one argument for a Windows command line.
    /// </summary>
    /// <param name="argument">The argument.</param>
    /// <returns>The argument, wrapped in quotes (with embedded quotes escaped) when it is empty or holds white space or quotes.</returns>
    internal static string Quote(string argument)
    {
        if (argument.Length > 0 && !argument.Any(c => char.IsWhiteSpace(c) || c == '"'))
        {
            return argument;
        }

        // Backslashes only need doubling in front of a quote (or the closing quote), as the C runtime parses them
        var builder = new System.Text.StringBuilder("\"");
        var backslashes = 0;
        foreach (var c in argument)
        {
            if (c == '\\')
            {
                backslashes++;
                continue;
            }

            builder.Append('\\', c == '"' ? (backslashes * 2) + 1 : backslashes).Append(c);
            backslashes = 0;
        }

        return builder.Append('\\', backslashes * 2).Append('"').ToString();
    }
}

/// <summary>
///     Finds <c>plexus-demo</c> builds and running instances.
/// </summary>
internal static class DemoProcess
{
    /// <summary>
    ///     The file name of the demo executable.
    /// </summary>
    private const string ExeFileName = "plexus-demo.exe";

    /// <summary>
    ///     The solution file that marks the repository root.
    /// </summary>
    private const string SolutionFileName = "Plexus.slnx";

    /// <summary>
    ///     Lists the aliases that have an automation pipe, sorted.
    /// </summary>
    /// <returns>The aliases.</returns>
    internal static IReadOnlyList<string> ListPipeAliases()
    {
        var aliases = new List<string>();
        try
        {
            foreach (var path in Directory.EnumerateFiles(@"\\.\pipe\", PipeNames.Prefix + "*"))
            {
                if (PipeNames.AliasOf(Path.GetFileName(path)) is { } alias)
                {
                    aliases.Add(alias);
                }
            }
        }
        catch (IOException)
        {
            // Pipe enumeration can race with an instance exiting; what was found so far is still right
        }

        aliases.Sort(StringComparer.OrdinalIgnoreCase);
        return aliases;
    }

    /// <summary>
    ///     Asks an instance for its process id.
    /// </summary>
    /// <param name="alias">The instance alias.</param>
    /// <param name="pid">The process id when the result is <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the instance answered.</returns>
    internal static bool TryGetPid(string alias, out int pid)
    {
        pid = 0;
        try
        {
            var reply = PipeClient.Send(alias, "info");
            if (reply.Ok && reply.Result?["pid"] is { } node)
            {
                pid = node.GetValue<int>();
            }
        }
        catch (PipeUnavailableException)
        {
            return false;
        }

        return pid != 0;
    }

    /// <summary>
    ///     Gets the process of an instance.
    /// </summary>
    /// <param name="alias">The instance alias.</param>
    /// <returns>The process, or <see langword="null"/> when the instance is not running.</returns>
    internal static Process? TryGetProcess(string alias)
    {
        if (!TryGetPid(alias, out var pid))
        {
            return null;
        }

        try
        {
            return Process.GetProcessById(pid);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    ///     Finds the demo executable.
    /// </summary>
    /// <param name="explicitPath">The <c>--exe</c> value, or <see langword="null"/> to search the repository's build output.</param>
    /// <param name="path">The full path when the result is <see langword="true"/>.</param>
    /// <param name="error">Why it was not found when the result is <see langword="false"/>.</param>
    /// <returns><see langword="true"/> when an executable was found.</returns>
    internal static bool TryResolveExePath(string? explicitPath, out string path, out string error)
    {
        path = string.Empty;
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            path = Path.GetFullPath(explicitPath);
            error = File.Exists(path) ? string.Empty : $"Executable not found: '{path}'.";
            return error.Length == 0;
        }

        // The newest build wins: a stale build in another configuration must not be launched by mistake.
        var candidate = CandidateExePaths().Where(File.Exists).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
        if (candidate is null)
        {
            error = $"Could not locate {ExeFileName}. Build the demo first (dotnet build Plexus.slnx) or pass --exe <path>.";
            return false;
        }

        path = candidate;
        error = string.Empty;
        return true;
    }

    /// <summary>
    ///     Enumerates the places a demo build can be.
    /// </summary>
    /// <returns>The candidate paths (they may not exist).</returns>
    private static IEnumerable<string> CandidateExePaths()
    {
        var roots = new List<string>();
        AddRoot(roots, Directory.GetCurrentDirectory());
        AddRoot(roots, AppContext.BaseDirectory);
        foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var configuration in new[] { "Release", "Debug" })
            {
                yield return Path.Combine(root, "demo", "DemaConsulting.Plexus.Demo.Gui", "bin", configuration, "net10.0", ExeFileName);
            }
        }

        // Built together into one artifacts folder, the demo sits beside the Puppet
        yield return Path.Combine(AppContext.BaseDirectory, ExeFileName);
    }

    /// <summary>
    ///     Adds the repository root above a folder, when there is one.
    /// </summary>
    /// <param name="roots">The roots found so far.</param>
    /// <param name="start">The folder to start climbing from.</param>
    private static void AddRoot(List<string> roots, string start)
    {
        for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, SolutionFileName)))
            {
                roots.Add(dir.FullName);
                return;
            }
        }
    }
}

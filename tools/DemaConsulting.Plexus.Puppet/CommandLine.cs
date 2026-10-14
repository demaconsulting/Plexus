namespace DemaConsulting.Plexus.Puppet;

/// <summary>
///     A parsed command line: the command name, its options and its positional arguments.
/// </summary>
internal sealed class ParsedCommand
{
    /// <summary>
    ///     Gets the command name (the first argument), or an empty string when there was none.
    /// </summary>
    internal string CommandName { get; init; } = string.Empty;

    /// <summary>
    ///     Gets the <c>--name [value]</c> options; a flag has a <see langword="null"/> value.
    /// </summary>
    internal IReadOnlyDictionary<string, string?> Options { get; init; } =
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Gets the arguments that are not options, in order.
    /// </summary>
    internal IReadOnlyList<string> Positional { get; init; } = [];

    /// <summary>
    ///     Gets the instance alias the command addresses.
    /// </summary>
    internal string Instance => GetOption("instance") ?? PipeNames.DefaultAlias;

    /// <summary>
    ///     Gets the value of an option.
    /// </summary>
    /// <param name="name">The option name without the leading dashes.</param>
    /// <returns>The value, or <see langword="null"/> when the option is absent or a flag.</returns>
    internal string? GetOption(string name) => Options.TryGetValue(name, out var value) ? value : null;

    /// <summary>
    ///     Tests whether an option (or flag) was given.
    /// </summary>
    /// <param name="name">The option name without the leading dashes.</param>
    /// <returns><see langword="true"/> when it was given.</returns>
    internal bool HasOption(string name) => Options.ContainsKey(name);
}

/// <summary>
///     Splits a command line into a <see cref="ParsedCommand"/>.
/// </summary>
internal static class CommandLineParser
{
    /// <summary>
    ///     The options that never take a value, so a positional argument after them is not swallowed.
    /// </summary>
    private static readonly HashSet<string> Flags = new(StringComparer.OrdinalIgnoreCase)
    {
        "allow-existing", "shift", "ctrl", "kill", "trace", "add", "ports", "double", "work-area", "screen",
    };

    /// <summary>
    ///     Parses the arguments.
    /// </summary>
    /// <remarks>
    ///     The first argument is the command. After it, <c>--name value</c> is an option, <c>--flag</c> is a known
    ///     value-less flag (or an option at the end of the line) and everything else is positional. A value may start
    ///     with a single dash (<c>-40</c>, <c>-3,4</c>) so negative numbers work.
    /// </remarks>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The parsed command; an empty one for no arguments.</returns>
    internal static ParsedCommand Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Length == 0)
        {
            return new ParsedCommand();
        }

        var options = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var positional = new List<string>();
        var i = 1;
        while (i < args.Length)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
            {
                positional.Add(args[i]);
                i++;
                continue;
            }

            var name = args[i][2..];
            var takesValue = !Flags.Contains(name) && i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal);
            options[name] = takesValue ? args[i + 1] : null;
            i += takesValue ? 2 : 1;
        }

        return new ParsedCommand { CommandName = args[0], Options = options, Positional = positional };
    }
}

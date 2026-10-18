using System.Text.RegularExpressions;
using DemaConsulting.Plexus.Demo.Model;

namespace DemaConsulting.Plexus.Demo.Gui;

/// <summary>
///     Which set of node types a demo instance registers.
/// </summary>
public enum NodeTypeSet
{
    /// <summary>Every demo node type.</summary>
    Full,

    /// <summary>Only the basic subset, so that pasting from a full instance demonstrates the missing-type failure path.</summary>
    Basic,
}

/// <summary>
///     The parsed command line of <c>plexus-demo</c>.
/// </summary>
/// <remarks>
///     Parsing is separate from starting the application so that argument handling can be tested
///     without any UI. Use <see cref="TryParse"/>; the record itself performs no validation.
/// </remarks>
/// <param name="OpenFile">The diagram JSON file to open at startup, or null.</param>
/// <param name="Sample">The sample to open at startup, or null.</param>
/// <param name="Types">The node type set to register.</param>
/// <param name="AutomationName">The automation pipe name suffix, or null when the endpoint is disabled.</param>
/// <param name="ShowHelp">True when only the usage text is shown.</param>
internal sealed partial record GuiOptions(
    string? OpenFile,
    string? Sample,
    NodeTypeSet Types,
    string? AutomationName,
    bool ShowHelp)
{
    /// <summary>
    ///     The sample opened when neither a file nor a sample is requested.
    /// </summary>
    public const string DefaultSample = "showcase";

    /// <summary>
    ///     The usage text shown by <c>--help</c> and after usage errors.
    /// </summary>
    public const string Usage =
        """
        Usage:
          plexus-demo [--open <file.json> | --sample <name>] [--types full|basic] [--automation <name>]

        Options:
          --open <file.json>     Open a Plexus diagram JSON file at startup.
          --sample <name>        Open a built-in sample (math, plumbing, logic, audio, workflow, groups, wiring, showcase). Default: showcase.
          --types full|basic     Register every demo node type (default) or only a small subset.
                                 Pasting a diagram fragment copied from a 'full' instance into a 'basic'
                                 one reports the missing node types in the status bar.
          --automation <name>    DEMO ONLY: serve the automation command pipe 'plexus-demo-<name>'
                                 (local machine, current user only).
          -h, --help             Show this help.
        """;

    /// <summary>
    ///     Gets a value indicating whether the user asked for a specific document (a file or a sample).
    /// </summary>
    public bool HasExplicitDocument => OpenFile is not null || Sample is not null;

    /// <summary>
    ///     Parses command-line arguments.
    /// </summary>
    /// <param name="args">The arguments, without the program name.</param>
    /// <param name="options">The parsed options when parsing succeeds; otherwise null.</param>
    /// <param name="error">A message describing the problem when parsing fails; otherwise null.</param>
    /// <returns>True when the arguments are valid.</returns>
    public static bool TryParse(IReadOnlyList<string> args, out GuiOptions? options, out string? error)
    {
        ArgumentNullException.ThrowIfNull(args);
        options = null;
        string? file = null, sample = null, automation = null;
        var types = NodeTypeSet.Full;
        var help = false;

        // Consume the arguments in order; every option except help takes the argument that follows it
        var pending = new Queue<string>(args);
        while (pending.TryDequeue(out var arg))
        {
            if (arg is "-h" or "--help")
            {
                help = true;
                continue;
            }

            if (arg is not ("--open" or "--sample" or "--types" or "--automation"))
            {
                error = $"Unknown argument '{arg}'.";
                return false;
            }

            if (!pending.TryDequeue(out var value) || string.IsNullOrWhiteSpace(value))
            {
                error = $"Option '{arg}' needs a value.";
                return false;
            }

            if (!TryApplyOption(arg, value, ref file, ref sample, ref types, ref automation, out error))
            {
                return false;
            }
        }

        if (file is not null && sample is not null)
        {
            error = "Use either --open or --sample, not both.";
            return false;
        }

        error = null;
        options = new GuiOptions(file, sample, types, automation, help);
        return true;
    }

    /// <summary>
    ///     Applies the value of one option to the parsed state.
    /// </summary>
    /// <param name="option">The option name.</param>
    /// <param name="value">The option's value.</param>
    /// <param name="file">The file to open; updated by <c>--open</c>.</param>
    /// <param name="sample">The sample to open; updated by <c>--sample</c>.</param>
    /// <param name="types">The node type set; updated by <c>--types</c>.</param>
    /// <param name="automation">The automation name; updated by <c>--automation</c>.</param>
    /// <param name="error">A message describing the problem when the value is invalid; otherwise null.</param>
    /// <returns>True when the value is valid.</returns>
    private static bool TryApplyOption(string option, string value, ref string? file, ref string? sample, ref NodeTypeSet types, ref string? automation, out string? error)
    {
        error = null;
        switch (option)
        {
            case "--open":
                file = value;
                return true;
            case "--sample":
                sample = DemoSamples.Names.FirstOrDefault(n => string.Equals(n, value, StringComparison.OrdinalIgnoreCase));
                if (sample is null)
                {
                    error = $"Unknown sample '{value}'. Available: {string.Join(", ", DemoSamples.Names)}.";
                    return false;
                }

                return true;
            case "--types":
                if (!Enum.TryParse(value, ignoreCase: true, out types) || !Enum.IsDefined(types))
                {
                    error = $"Unknown type set '{value}'. Use 'full' or 'basic'.";
                    return false;
                }

                return true;
            default:
                if (!PipeNamePattern().IsMatch(value))
                {
                    error = $"Automation name '{value}' may only contain letters, digits, '-' and '_'.";
                    return false;
                }

                automation = value;
                return true;
        }
    }

    /// <summary>
    ///     Gets the pattern an automation name must match (it becomes part of a pipe name).
    /// </summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex PipeNamePattern();
}

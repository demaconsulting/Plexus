using System.Globalization;

namespace DemaConsulting.Plexus.Demo.Cli;

/// <summary>
///     The parsed command line of <c>plexus-demo-render</c>.
/// </summary>
/// <remarks>
///     Parsing is separate from running so that argument handling can be tested without touching
///     the file system. Use <see cref="TryParse"/>; the record itself performs no validation.
/// </remarks>
/// <param name="InputFile">The diagram JSON file to render, or null when a sample is used.</param>
/// <param name="Sample">The sample name to render, or null when a file is used.</param>
/// <param name="OutputDirectory">The directory PNG files are written to, or null when none are requested.</param>
/// <param name="Page">The page selector (index or name), or null for every page.</param>
/// <param name="Scale">The render scale.</param>
/// <param name="ShowGrid">True when the dot grid is drawn.</param>
/// <param name="WriteJson">The file to also write the diagram JSON to, or null.</param>
/// <param name="ListSamples">True when only the sample names are listed.</param>
/// <param name="ShowHelp">True when only the usage text is shown.</param>
internal sealed record CliOptions(
    string? InputFile,
    string? Sample,
    string? OutputDirectory,
    string? Page,
    double Scale,
    bool ShowGrid,
    string? WriteJson,
    bool ListSamples,
    bool ShowHelp)
{
    /// <summary>
    ///     The usage text shown by <c>--help</c> and after usage errors.
    /// </summary>
    public const string Usage =
        """
        Usage:
          plexus-demo-render (<file.json> | --sample <name>) --out <dir> [options]
          plexus-demo-render --list-samples
          plexus-demo-render --help

        Renders each page (or the selected page) of a Plexus demo diagram to <dir>/<page-name>.png.

        Options:
          --sample <name>      Render a built-in sample instead of a file (see --list-samples).
          --out <dir>          Output directory for the PNG files (created if missing).
          --page <index|name>  Render only this page (zero-based index or page name).
          --scale <s>          Pixels per diagram unit, a number greater than zero (default 1).
          --no-grid            Do not draw the dot grid.
          --write-json <file>  Also save the diagram as snapshot JSON.
          --list-samples       List the built-in sample names and exit.
          -h, --help           Show this help.
        """;

    /// <summary>
    ///     Parses command-line arguments.
    /// </summary>
    /// <param name="args">The arguments, without the program name.</param>
    /// <param name="options">The parsed options when parsing succeeds; otherwise null.</param>
    /// <param name="error">A message describing the problem when parsing fails; otherwise null.</param>
    /// <returns>True when the arguments are valid.</returns>
    public static bool TryParse(IReadOnlyList<string> args, out CliOptions? options, out string? error)
    {
        ArgumentNullException.ThrowIfNull(args);
        options = null;
        var state = new ParseState();

        // Consume the arguments in order; value options take the argument that follows them
        var pending = new Queue<string>(args);
        while (pending.TryDequeue(out var arg))
        {
            if (!state.Consume(arg, pending))
            {
                error = state.Error;
                return false;
            }
        }

        // Help and listing stand alone; everything else needs exactly one input and an output
        if (!state.Validate())
        {
            error = state.Error;
            return false;
        }

        error = null;
        options = new CliOptions(state.InputFile, state.Sample, state.Output, state.Page, state.Scale, state.ShowGrid, state.WriteJson, state.ListSamples, state.ShowHelp);
        return true;
    }

    /// <summary>
    ///     The mutable state accumulated while the arguments are consumed one by one.
    /// </summary>
    private sealed class ParseState
    {
        /// <summary>
        ///     Gets the message describing the problem when a step fails.
        /// </summary>
        public string? Error { get; private set; }

        /// <summary>
        ///     Gets the input file argument, or null.
        /// </summary>
        public string? InputFile { get; private set; }

        /// <summary>
        ///     Gets the sample name, or null.
        /// </summary>
        public string? Sample { get; private set; }

        /// <summary>
        ///     Gets the output directory, or null.
        /// </summary>
        public string? Output { get; private set; }

        /// <summary>
        ///     Gets the page selector, or null.
        /// </summary>
        public string? Page { get; private set; }

        /// <summary>
        ///     Gets the JSON output file, or null.
        /// </summary>
        public string? WriteJson { get; private set; }

        /// <summary>
        ///     Gets the render scale.
        /// </summary>
        public double Scale { get; private set; } = 1.0;

        /// <summary>
        ///     Gets a value indicating whether the dot grid is drawn.
        /// </summary>
        public bool ShowGrid { get; private set; } = true;

        /// <summary>
        ///     Gets a value indicating whether only the sample names are listed.
        /// </summary>
        public bool ListSamples { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether only the usage text is shown.
        /// </summary>
        public bool ShowHelp { get; private set; }

        /// <summary>
        ///     Consumes one argument, and the value that follows it when the option takes one.
        /// </summary>
        /// <param name="arg">The argument.</param>
        /// <param name="pending">The remaining arguments.</param>
        /// <returns>True when the argument is valid; otherwise false with <see cref="Error"/> set.</returns>
        public bool Consume(string arg, Queue<string> pending)
        {
            switch (arg)
            {
                case "-h" or "--help":
                    ShowHelp = true;
                    return true;
                case "--list-samples":
                    ListSamples = true;
                    return true;
                case "--no-grid":
                    ShowGrid = false;
                    return true;
                case "--sample" or "--out" or "--page" or "--scale" or "--write-json":
                    if (!pending.TryDequeue(out var value))
                    {
                        Error = $"Option '{arg}' needs a value.";
                        return false;
                    }

                    return ApplyValue(arg, value);
                default:
                    return ApplyPositional(arg);
            }
        }

        /// <summary>
        ///     Checks that the combination of arguments is complete.
        /// </summary>
        /// <returns>True when valid; otherwise false with <see cref="Error"/> set.</returns>
        public bool Validate()
        {
            if (ShowHelp || ListSamples)
            {
                return true;
            }

            if (InputFile is null == Sample is null)
            {
                Error = InputFile is null ? "Specify an input file or --sample <name>." : "Specify either an input file or --sample, not both.";
                return false;
            }

            if (Output is null && WriteJson is null)
            {
                Error = "Specify --out <dir> (and/or --write-json <file>).";
                return false;
            }

            return true;
        }

        /// <summary>
        ///     Applies the value of a value option.
        /// </summary>
        /// <param name="option">The option.</param>
        /// <param name="value">Its value.</param>
        /// <returns>True when the value is valid; otherwise false with <see cref="Error"/> set.</returns>
        private bool ApplyValue(string option, string value)
        {
            switch (option)
            {
                case "--sample":
                    Sample = value;
                    return true;
                case "--out":
                    Output = value;
                    return true;
                case "--page":
                    Page = value;
                    return true;
                case "--write-json":
                    WriteJson = value;
                    return true;
                default:
                    if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var scale) || !double.IsFinite(scale) || scale <= 0)
                    {
                        Error = $"Invalid scale '{value}'; expected a number greater than zero.";
                        return false;
                    }

                    Scale = scale;
                    return true;
            }
        }

        /// <summary>
        ///     Applies an argument that is not a known option: the input file, or an error.
        /// </summary>
        /// <param name="arg">The argument.</param>
        /// <returns>True when accepted; otherwise false with <see cref="Error"/> set.</returns>
        private bool ApplyPositional(string arg)
        {
            if (arg.StartsWith('-'))
            {
                Error = $"Unknown option '{arg}'.";
                return false;
            }

            if (InputFile is not null)
            {
                Error = $"Unexpected argument '{arg}'; only one input file is allowed.";
                return false;
            }

            InputFile = arg;
            return true;
        }
    }
}

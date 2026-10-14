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
        string? inputFile = null, sample = null, output = null, page = null, writeJson = null;
        var scale = 1.0;
        bool showGrid = true, listSamples = false, showHelp = false;

        // Consume the arguments in order; value options take the argument that follows them
        var pending = new Queue<string>(args);
        while (pending.TryDequeue(out var arg))
        {
            switch (arg)
            {
                case "-h" or "--help":
                    showHelp = true;
                    break;
                case "--list-samples":
                    listSamples = true;
                    break;
                case "--no-grid":
                    showGrid = false;
                    break;
                case "--sample" or "--out" or "--page" or "--scale" or "--write-json":
                    if (!pending.TryDequeue(out var value))
                    {
                        error = $"Option '{arg}' needs a value.";
                        return false;
                    }
                    switch (arg)
                    {
                        case "--sample":
                            sample = value;
                            break;
                        case "--out":
                            output = value;
                            break;
                        case "--page":
                            page = value;
                            break;
                        case "--write-json":
                            writeJson = value;
                            break;
                        default:
                            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out scale) || !double.IsFinite(scale) || scale <= 0)
                            {
                                error = $"Invalid scale '{value}'; expected a number greater than zero.";
                                return false;
                            }

                            break;
                    }

                    break;
                default:
                    if (arg.StartsWith('-'))
                    {
                        error = $"Unknown option '{arg}'.";
                        return false;
                    }

                    if (inputFile is not null)
                    {
                        error = $"Unexpected argument '{arg}'; only one input file is allowed.";
                        return false;
                    }

                    inputFile = arg;
                    break;
            }
        }

        // Help and listing stand alone; everything else needs exactly one input and an output
        if (!showHelp && !listSamples)
        {
            if (inputFile is null == sample is null)
            {
                error = inputFile is null ? "Specify an input file or --sample <name>." : "Specify either an input file or --sample, not both.";
                return false;
            }

            if (output is null && writeJson is null)
            {
                error = "Specify --out <dir> (and/or --write-json <file>).";
                return false;
            }
        }

        error = null;
        options = new CliOptions(inputFile, sample, output, page, scale, showGrid, writeJson, listSamples, showHelp);
        return true;
    }
}

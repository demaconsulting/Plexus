using System.Globalization;
using System.Text.Json;
using DemaConsulting.Plexus.Demo.Model;
using DemaConsulting.Plexus.Rendering;

namespace DemaConsulting.Plexus.Demo.Cli;

/// <summary>
///     Runs <c>plexus-demo-render</c>: loads (or builds) a diagram and renders its pages to PNG files.
/// </summary>
/// <remarks>
///     Output goes to the supplied writers rather than the console so tests can capture it.
///     Exit codes: 0 success, 1 the work failed (unreadable file, unknown page, I/O error),
///     2 the command line was invalid.
/// </remarks>
internal static class CliRunner
{
    /// <summary>
    ///     The exit code reported for success.
    /// </summary>
    public const int Success = 0;

    /// <summary>
    ///     The exit code reported when loading or rendering fails.
    /// </summary>
    public const int Failure = 1;

    /// <summary>
    ///     The exit code reported for an invalid command line.
    /// </summary>
    public const int UsageError = 2;

    /// <summary>
    ///     Runs the tool.
    /// </summary>
    /// <param name="args">The command-line arguments, without the program name.</param>
    /// <param name="output">Receives normal output.</param>
    /// <param name="error">Receives error messages.</param>
    /// <returns>The process exit code.</returns>
    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        // Invalid command lines get the message plus usage, so mistakes are self-explanatory
        if (!CliOptions.TryParse(args, out var options, out var problem) || options is null)
        {
            error.WriteLine($"error: {problem}");
            error.WriteLine();
            error.WriteLine(CliOptions.Usage);
            return UsageError;
        }

        if (options.ShowHelp)
        {
            output.WriteLine(CliOptions.Usage);
            return Success;
        }

        if (options.ListSamples)
        {
            foreach (var name in DemoSamples.Names)
            {
                output.WriteLine(name);
            }

            return Success;
        }

        try
        {
            return Render(options, output, error);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or SnapshotException or ArgumentException)
        {
            // Expected failures (bad file, bad JSON, bad path) are reported, not shown as crashes
            error.WriteLine($"error: {ex.Message}");
            return Failure;
        }
    }

    /// <summary>
    ///     Loads the diagram and renders the selected pages.
    /// </summary>
    /// <param name="options">The validated options.</param>
    /// <param name="output">Receives normal output.</param>
    /// <param name="error">Receives error messages.</param>
    /// <returns>The process exit code.</returns>
    private static int Render(CliOptions options, TextWriter output, TextWriter error)
    {
        var diagram = options.Sample is { } sample ? DemoSamples.Create(sample) : DemoDocument.Load(options.InputFile!);
        if (options.WriteJson is { } jsonPath)
        {
            DemoDocument.Save(diagram, jsonPath);
            output.WriteLine($"Wrote {jsonPath}");
        }

        if (options.OutputDirectory is not { } directory)
        {
            return Success;
        }

        // Resolve the page selection before writing anything, so a typo does not leave partial output
        var pages = SelectPages(diagram, options.Page);
        if (pages.Count == 0)
        {
            error.WriteLine(options.Page is null
                ? "error: the diagram has no pages."
                : $"error: no page matches '{options.Page}' (pages: {string.Join(", ", diagram.Pages.Select(p => $"{diagram.Pages.IndexOf(p)}={p.Name}"))}).");
            return Failure;
        }

        Directory.CreateDirectory(directory);
        var renderOptions = new RenderOptions { Scale = options.Scale, ShowGrid = options.ShowGrid };
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var page in pages)
        {
            var path = Path.Join(directory, UniqueFileName(page.Name, diagram.Pages.IndexOf(page), used));
            PlexusRenderer.SavePng(page, path, renderOptions);
            output.WriteLine($"Wrote {path}");
        }

        return Success;
    }

    /// <summary>
    ///     Picks the pages to render.
    /// </summary>
    /// <param name="diagram">The diagram.</param>
    /// <param name="selector">A page name or zero-based index, or null for every page.</param>
    /// <returns>The selected pages; empty when the selector matches nothing.</returns>
    internal static IReadOnlyList<DiagramPage> SelectPages(Diagram diagram, string? selector)
    {
        if (selector is null)
        {
            return [.. diagram.Pages];
        }

        // A name wins over an index so pages literally called "2" stay reachable by name
        var byName = diagram.Pages.FirstOrDefault(p => string.Equals(p.Name, selector, StringComparison.OrdinalIgnoreCase));
        if (byName is not null)
        {
            return [byName];
        }

        return int.TryParse(selector, NumberStyles.None, CultureInfo.InvariantCulture, out var index) && index < diagram.Pages.Count
            ? [diagram.Pages[index]]
            : [];
    }

    /// <summary>
    ///     Characters replaced in page names when they become file names: everything Windows rejects (which includes
    ///     the only characters Linux and macOS reject), so a page gets the same file name on every platform.
    /// </summary>
    private const string ReservedFileNameChars = "<>:\"/\\|?*";

    /// <summary>
    ///     Turns a page name into a file name that is valid and unique among the files written so far.
    /// </summary>
    /// <remarks>
    ///     The replaced set is fixed rather than <see cref="Path.GetInvalidFileNameChars"/>, which is platform-specific
    ///     (on Linux only <c>/</c> and NUL): rendered output is often shared between machines, so names must be portable.
    /// </remarks>
    /// <param name="pageName">The page name.</param>
    /// <param name="index">The page's index, used to disambiguate duplicates.</param>
    /// <param name="used">The file names already taken; updated with the returned name.</param>
    /// <returns>The file name including the <c>.png</c> extension.</returns>
    internal static string UniqueFileName(string pageName, int index, ISet<string> used)
    {
        // Replace reserved and control characters; an empty result falls back to the index
        var stem = string.Concat(pageName.Select(c => char.IsControl(c) || ReservedFileNameChars.Contains(c) ? '_' : c)).Trim();
        if (stem.Length == 0)
        {
            stem = $"page{index}";
        }

        var name = $"{stem}.png";
        if (!used.Add(name))
        {
            name = $"{stem}-{index}.png";
            used.Add(name);
        }

        return name;
    }
}

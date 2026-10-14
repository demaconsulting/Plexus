using DemaConsulting.Plexus.Scene;
using PlexusSize = DemaConsulting.Plexus.Geometry.Size;

namespace DemaConsulting.Plexus.Avalonia;

/// <summary>
///     Measures text with Avalonia's own text engine so scene layout matches what the control paints.
/// </summary>
/// <remarks>
///     <see cref="DiagramEditor"/> uses an instance of this class automatically. Hosts only need it
///     when they build scenes themselves (for example <c>SceneBuilder.Build(page, new SceneOptions
///     { TextMeasurer = new AvaloniaTextMeasurer() })</c>) and want those scenes to agree with the
///     editor. Results are cached per text and style. Like all Avalonia text services the measurer
///     must be used on the UI thread.
/// </remarks>
/// <example>
///     Build a scene whose name and label areas match what the editor paints, for example to hit test in a custom overlay.
///     <code>
///     using DemaConsulting.Plexus;
///     using DemaConsulting.Plexus.Avalonia;
///     using DemaConsulting.Plexus.Scene;
///
///     var diagram = new Diagram(new NodeTypeRegistry());
///     var page = diagram.Pages.Add("Main");
///     var scene = SceneBuilder.Build(page, new SceneOptions { TextMeasurer = new AvaloniaTextMeasurer() });
///     Console.WriteLine(scene.Primitives.Count);
///     </code>
/// </example>
public sealed class AvaloniaTextMeasurer : ITextMeasurer
{
    /// <summary>
    ///     The most results kept before the cache is emptied, which bounds memory for ever-changing text.
    /// </summary>
    private const int MaxCacheEntries = 4096;

    /// <summary>
    ///     Sizes already measured.
    /// </summary>
    private readonly Dictionary<(string Text, TextStyle Style), PlexusSize> _cache = [];

    /// <summary>
    ///     Initializes a new instance of the <see cref="AvaloniaTextMeasurer"/> class with an empty measurement cache.
    /// </summary>
    public AvaloniaTextMeasurer()
    {
        // Nothing to initialize: the cache fills on demand
    }

    /// <summary>
    ///     Measures a single line of text.
    /// </summary>
    /// <param name="text">The text to measure.</param>
    /// <param name="style">The font family, size and weight to measure with.</param>
    /// <returns>The size of the text in diagram units.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> or <paramref name="style"/> is null.</exception>
    public PlexusSize Measure(string text, TextStyle style)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(style);

        // Colors do not affect size, so normalize them away to share cache entries between styles
        var key = (text, style with { Color = default });
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        // Avalonia reports the empty string as a single space; its height is wanted, its width is not
        var formatted = TextFormats.Create(text, style);
        var size = new PlexusSize(text.Length == 0 ? 0 : formatted.WidthIncludingTrailingWhitespace, formatted.Height);

        // Start over rather than evicting individually: the working set normally refills almost at once
        if (_cache.Count >= MaxCacheEntries)
        {
            _cache.Clear();
        }

        _cache[key] = size;
        return size;
    }
}

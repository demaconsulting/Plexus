namespace DemaConsulting.Plexus;

/// <summary>
///     Describes a visual that can be drawn on a node, supplied as SVG text.
/// </summary>
/// <remarks>
///     The icon owns only the SVG text and a stable <see cref="Key"/> (a content hash) that painters
///     use to cache decoded images. Loading from a file or resource happens eagerly so the icon is
///     immutable and safe to share after creation. Two icons with the same SVG text are equal.
///     <para>
///         SVG guidance: the icon is scaled to fit a small square slot (about the height of the node header), keeping its aspect ratio,
///         so give the document a <c>viewBox</c> (for example <c>0 0 24 24</c>) and keep it simple: basic shapes and paths with explicit
///         solid <c>fill</c> and <c>stroke</c> colors, groups and transforms. Scripts, external references, filters and text are not
///         guaranteed to render, and the two painters (Avalonia and the PNG renderer) use different SVG engines. The node is drawn on a
///         dark fill, so use light colors such as <c>#fff</c>. A malformed SVG never throws while painting; the icon is simply left out.
///     </para>
/// </remarks>
/// <example>
///     Create an icon from inline SVG text, a file, or an embedded resource.
///     <code>
///     using System.Reflection;
///     using DemaConsulting.Plexus;
///
///     // 1. Inline SVG text
///     var circle = IconSource.FromSvg(
///         "&lt;svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"&gt;" +
///         "&lt;circle cx=\"12\" cy=\"12\" r=\"8\" fill=\"none\" stroke=\"#fff\" stroke-width=\"2\"/&gt;&lt;/svg&gt;");
///
///     // 2. A file, read immediately (throws IOException when it cannot be read)
///     // var fromFile = IconSource.FromSvgFile(Path.Combine(AppContext.BaseDirectory, "icons", "valve.svg"));
///
///     // 3. An embedded resource. The name is "{DefaultNamespace}.{folder path with dots}.{file name}", for a project with
///     //    <EmbeddedResource Include="Icons\valve.svg" /> and root namespace MyApp it is "MyApp.Icons.valve.svg".
///     //    Check the exact names with Assembly.GetManifestResourceNames().
///     // var fromResource = IconSource.FromSvgResource(typeof(Program).Assembly, "MyApp.Icons.valve.svg");
///
///     var type = new NodeType("demo.Valve") { Icon = circle };
///     Console.WriteLine(circle.Equals(IconSource.FromSvg(circle.SvgText)));   // True: equality is by content
///     </code>
/// </example>
public sealed class IconSource : IEquatable<IconSource>
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="IconSource"/> class.
    /// </summary>
    /// <param name="svgText">The SVG document text.</param>
    private IconSource(string svgText)
    {
        SvgText = svgText;
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(svgText));
        Key = "svg:" + Convert.ToHexString(hash, 0, 12);
    }

    /// <summary>
    ///     Gets the SVG document text.
    /// </summary>
    public string SvgText { get; }

    /// <summary>
    ///     Gets a stable key derived from the content, suitable for painter caches.
    /// </summary>
    public string Key { get; }

    /// <summary>
    ///     Creates an icon from SVG text.
    /// </summary>
    /// <param name="svg">The SVG document text.</param>
    /// <returns>The icon.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="svg"/> is null, empty or whitespace.</exception>
    public static IconSource FromSvg(string svg)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(svg);
        return new IconSource(svg);
    }

    /// <summary>
    ///     Creates an icon by reading an SVG file immediately.
    /// </summary>
    /// <param name="path">Path of the SVG file.</param>
    /// <returns>The icon.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is null, empty or whitespace.</exception>
    /// <exception cref="IOException">Thrown when the file cannot be read.</exception>
    public static IconSource FromSvgFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return FromSvg(File.ReadAllText(path));
    }

    /// <summary>
    ///     Creates an icon from an embedded SVG resource.
    /// </summary>
    /// <remarks>
    ///     The resource name is the manifest name MSBuild gives the embedded file: the project's root namespace, then the
    ///     folder path with dots, then the file name (<c>MyApp.Icons.valve.svg</c> for <c>Icons\valve.svg</c> in project <c>MyApp</c>),
    ///     unless the item sets <c>LogicalName</c>. List the real names with <c>assembly.GetManifestResourceNames()</c>.
    /// </remarks>
    /// <param name="assembly">The assembly containing the resource.</param>
    /// <param name="resourceName">The manifest resource name, for example <c>MyApp.Icons.valve.svg</c>.</param>
    /// <returns>The icon.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="assembly"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the resource does not exist.</exception>
    public static IconSource FromSvgResource(System.Reflection.Assembly assembly, string resourceName)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);

        // A missing resource is a host mistake that should surface immediately, not as a blank icon
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new ArgumentException($"Resource '{resourceName}' was not found in assembly '{assembly.GetName().Name}'.", nameof(resourceName));
        using var reader = new StreamReader(stream);
        return FromSvg(reader.ReadToEnd());
    }

    /// <summary>
    ///     Determines whether another icon has the same content key (the same SVG content).
    /// </summary>
    /// <param name="other">The icon to compare with, or null.</param>
    /// <returns><see langword="true"/> when both icons have the same key.</returns>
    public bool Equals(IconSource? other) => other is not null && Key == other.Key;

    /// <summary>
    ///     Determines whether an object is an <see cref="IconSource"/> with the same SVG content.
    /// </summary>
    /// <param name="obj">The object to compare with, or null.</param>
    /// <returns><see langword="true"/> when <paramref name="obj"/> is an icon with the same key.</returns>
    public override bool Equals(object? obj) => Equals(obj as IconSource);

    /// <summary>
    ///     Returns a hash code derived from the content key, consistent with <see cref="Equals(IconSource?)"/>.
    /// </summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => Key.GetHashCode(StringComparison.Ordinal);
}

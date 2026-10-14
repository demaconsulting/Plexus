namespace DemaConsulting.Plexus.Puppet;

/// <summary>
///     The AutomationIds of the library's node palette, and the rule that turns a node type key into the id of its entry.
/// </summary>
/// <remarks>
///     <para>
///         The Puppet is a UI driver and deliberately does not reference the Avalonia control library, so the
///         rule is restated here. The single source of truth is the library's
///         <c>NodePalette.GetAutomationId</c>: <c>NodePalette_</c> followed by the type key with every
///         character other than an ASCII letter, digit or underscore replaced by an underscore. The Puppet's
///         unit tests reference the library and assert that both implementations give the same id for a
///         range of keys, so they cannot drift apart unnoticed.
///     </para>
/// </remarks>
internal static class PaletteIds
{
    /// <summary>
    ///     The AutomationId of the palette control itself.
    /// </summary>
    internal const string Palette = "NodePalette";

    /// <summary>
    ///     The AutomationId of the palette's built-in search box.
    /// </summary>
    internal const string Filter = "NodePalette_Filter";

    /// <summary>
    ///     The prefix of every palette entry's AutomationId.
    /// </summary>
    internal const string Prefix = "NodePalette_";

    /// <summary>
    ///     Gets the AutomationId of the palette entry of a node type.
    /// </summary>
    /// <param name="typeKey">The full node type key, for example <c>demo.audio.Mixer</c>.</param>
    /// <returns>The AutomationId, for example <c>NodePalette_demo_audio_Mixer</c>.</returns>
    internal static string ForKey(string typeKey) =>
        Prefix + string.Concat(typeKey.Select(c => char.IsAsciiLetterOrDigit(c) || c == '_' ? c : '_'));

    /// <summary>
    ///     Resolves what the user typed to the AutomationId of a palette entry.
    /// </summary>
    /// <param name="type">A full type key (it contains a dot), or just its last part (for example <c>Mixer</c>).</param>
    /// <param name="existingIds">The AutomationIds that exist in the application, used to resolve a short name.</param>
    /// <returns>The AutomationId; for a short name the single existing entry that ends with it, else the id the name would have.</returns>
    /// <exception cref="PuppetException">Thrown when a short name matches several entries.</exception>
    internal static string Resolve(string type, IEnumerable<string> existingIds)
    {
        var id = ForKey(type);
        if (type.Contains('.'))
        {
            return id;
        }

        // A short name is matched against the entries that exist, so the caller need not know the namespace
        var suffix = id[Prefix.Length..];
        var matches = existingIds
            .Distinct(StringComparer.Ordinal)
            .Where(existing => existing.StartsWith(Prefix, StringComparison.Ordinal) && existing != Filter
                && (existing == id || existing.EndsWith("_" + suffix, StringComparison.Ordinal)))
            .ToList();
        return matches.Count switch
        {
            0 => id,
            1 => matches[0],
            _ => throw PuppetException.Usage($"'{type}' matches several palette entries ({string.Join(", ", matches)}); give the full type key."),
        };
    }
}

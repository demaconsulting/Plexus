using System.Text.RegularExpressions;

namespace DemaConsulting.Plexus.Puppet;

/// <summary>
///     The naming rules of the demo's automation pipes: <c>\\.\pipe\plexus-demo-&lt;alias&gt;</c>.
/// </summary>
internal static partial class PipeNames
{
    /// <summary>
    ///     The prefix every automation pipe name starts with.
    /// </summary>
    internal const string Prefix = "plexus-demo-";

    /// <summary>
    ///     The alias used when a command does not name an instance.
    /// </summary>
    internal const string DefaultAlias = "main";

    /// <summary>
    ///     Tests whether an alias is usable in a pipe name (letters, digits, dash and underscore).
    /// </summary>
    /// <param name="alias">The alias.</param>
    /// <returns><see langword="true"/> when the alias is valid.</returns>
    internal static bool IsValidAlias(string? alias) => !string.IsNullOrEmpty(alias) && AliasPattern().IsMatch(alias);

    /// <summary>
    ///     Builds the pipe name (without the <c>\\.\pipe\</c> prefix) of an alias.
    /// </summary>
    /// <param name="alias">The instance alias.</param>
    /// <returns>The pipe name.</returns>
    internal static string ForAlias(string alias) => Prefix + alias;

    /// <summary>
    ///     Extracts the alias from a pipe name.
    /// </summary>
    /// <param name="pipeName">A pipe name, with or without the <c>\\.\pipe\</c> prefix.</param>
    /// <returns>The alias, or <see langword="null"/> when the pipe is not an automation pipe.</returns>
    internal static string? AliasOf(string pipeName)
    {
        var name = pipeName.StartsWith(@"\\.\pipe\", StringComparison.OrdinalIgnoreCase) ? pipeName[9..] : pipeName;
        if (!name.StartsWith(Prefix, StringComparison.Ordinal) || name.Length == Prefix.Length)
        {
            return null;
        }

        var alias = name[Prefix.Length..];
        return IsValidAlias(alias) ? alias : null;
    }

    /// <summary>
    ///     The pattern an alias must match.
    /// </summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex("^[A-Za-z0-9_-]+$")]
    private static partial Regex AliasPattern();
}

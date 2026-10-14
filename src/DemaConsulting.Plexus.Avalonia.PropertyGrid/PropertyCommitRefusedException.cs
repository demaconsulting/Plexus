namespace DemaConsulting.Plexus.Avalonia.PropertyGrid;

/// <summary>
///     Thrown by an <see cref="IPropertyCommitScope"/> (or a host's commit scope) to refuse a commit because of one
///     property, so the edit session records the reason as that property's error and a grid shows it on that row.
/// </summary>
/// <remarks>
///     The session treats any other exception of a commit scope as a failure of the whole commit (the message is
///     reported, but no row is marked). Use this exception when a rule that can only be checked at commit time, such as a
///     host veto of a rename, rejects a value.
/// </remarks>
/// <example>
///     <code><![CDATA[
///     using DemaConsulting.Plexus;
///     using DemaConsulting.Plexus.Avalonia.PropertyGrid;
///
///     public static class RenameCommitSample
///     {
///         // The body of a commit scope (IPropertyCommitScope.RunCommit) of a node adapter: refuse the rename with the reason on the row
///         public static void ApplyRename(Diagram diagram, Node node, string proposedName)
///         {
///             if (!diagram.TryRename(node, proposedName, out string? reason))
///             {
///                 throw new PropertyCommitRefusedException("Name", reason ?? "The name was refused.");
///             }
///         }
///     }
///     ]]></code>
/// </example>
public sealed class PropertyCommitRefusedException : InvalidOperationException
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="PropertyCommitRefusedException"/> class.
    /// </summary>
    /// <param name="path">The <see cref="PropertyItem.Path"/> of the refused property.</param>
    /// <param name="message">The reason, written for the user.</param>
    /// <exception cref="ArgumentException"><paramref name="path"/> is <see langword="null"/> or white space.</exception>
    public PropertyCommitRefusedException(string path, string message)
        : base(message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Path = path;
    }

    /// <summary>
    ///     Gets the path of the refused property.
    /// </summary>
    public string Path { get; }
}

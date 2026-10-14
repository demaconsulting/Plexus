namespace DemaConsulting.Plexus;

/// <summary>
///     The outcome of <see cref="PlexusClipboard.Paste"/>.
/// </summary>
/// <remarks>
///     Paste never throws for bad clipboard content: <see cref="Success"/> is false, <see cref="Error"/>
///     says why, and nothing was changed. A successful paste may still carry <see cref="Warnings"/>
///     (for example property values replaced by defaults).
/// </remarks>
public sealed class PasteResult
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="PasteResult"/> class.
    /// </summary>
    /// <param name="success">Whether the content was pasted.</param>
    /// <param name="elements">The created elements.</param>
    /// <param name="missingNodeTypes">Missing node type keys.</param>
    /// <param name="warnings">Non-fatal findings.</param>
    /// <param name="error">The failure reason, or null.</param>
    private PasteResult(bool success, IReadOnlyList<DiagramElement> elements, IReadOnlyList<string> missingNodeTypes, IReadOnlyList<string> warnings, string? error)
    {
        Success = success;
        Elements = elements;
        MissingNodeTypes = missingNodeTypes;
        Warnings = warnings;
        Error = error;
    }

    /// <summary>
    ///     Gets a value indicating whether the content was pasted.
    /// </summary>
    public bool Success { get; }

    /// <summary>
    ///     Gets every element created by the paste in z-order (group members and wires included); empty on failure.
    /// </summary>
    public IReadOnlyList<DiagramElement> Elements { get; }

    /// <summary>
    ///     Gets every node type key of the clipboard content that the target diagram does not know; empty unless unknown types caused the failure.
    /// </summary>
    public IReadOnlyList<string> MissingNodeTypes { get; }

    /// <summary>
    ///     Gets non-fatal findings, such as property values that did not fit the target definition and were reset to defaults.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>
    ///     Gets the reason the paste failed, or null on success.
    /// </summary>
    public string? Error { get; }

    /// <summary>
    ///     Gets a ready-to-show sentence: a summary of what was pasted, or why nothing was (including the missing node types).
    /// </summary>
    /// <remarks>
    ///     Success reads like <c>Pasted 4 element(s).</c> and appends any <see cref="Warnings"/>; failure reads like
    ///     <c>Paste failed: this application does not know the node type(s) demo.Extra.</c> or <c>Paste failed:</c> followed by <see cref="Error"/>.
    ///     Hosts put it in a status bar instead of formatting <see cref="Error"/> and <see cref="MissingNodeTypes"/> themselves.
    /// </remarks>
    /// <example>
    ///     <code>
    ///     var result = PlexusClipboard.Paste(page, text, at: null);   // page: a DiagramPage; text: the clipboard text
    ///     statusBar.Text = result.Message;                            // whether it worked or not
    ///     </code>
    /// </example>
    public string Message
    {
        get
        {
            if (Success)
            {
                var summary = $"Pasted {Elements.Count} element(s).";
                return Warnings.Count == 0 ? summary : $"{summary} Warnings: {string.Join("; ", Warnings)}";
            }

            return MissingNodeTypes.Count > 0
                ? $"Paste failed: this application does not know the node type(s) {string.Join(", ", MissingNodeTypes)}."
                : $"Paste failed: {Error ?? "the clipboard does not hold Plexus data."}";
        }
    }

    /// <summary>
    ///     Creates a successful result.
    /// </summary>
    /// <param name="elements">The created elements.</param>
    /// <param name="warnings">Non-fatal findings.</param>
    /// <returns>The result.</returns>
    internal static PasteResult Succeeded(IReadOnlyList<DiagramElement> elements, IReadOnlyList<string> warnings) =>
        new(true, elements, [], warnings, null);

    /// <summary>
    ///     Creates a failed result.
    /// </summary>
    /// <param name="error">Why the paste failed.</param>
    /// <param name="missingNodeTypes">Missing node type keys, if that is the reason.</param>
    /// <returns>The result.</returns>
    internal static PasteResult Failed(string error, IReadOnlyList<string>? missingNodeTypes = null) =>
        new(false, [], missingNodeTypes ?? [], [], error);
}

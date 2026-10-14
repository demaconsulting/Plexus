namespace DemaConsulting.Plexus.Avalonia;

/// <summary>
///     Describes a paste that Plexus rejected, so the host can tell the user why.
/// </summary>
/// <remarks>
///     Pasting never throws for bad clipboard content (it is untrusted by nature): the page is left
///     untouched and this event carries the reason, including any node types the target diagram
///     does not know about. An empty or non-Plexus clipboard is reported the same way.
/// </remarks>
/// <example>
///     Report a failed paste in a status line.
///     <code><![CDATA[
///     using Avalonia.Controls;
///     using DemaConsulting.Plexus.Avalonia;
///
///     static class PasteSample
///     {
///         public static void Wire(DiagramEditor editor, TextBlock status)
///         {
///             editor.PasteFailed += (_, e) =>
///             {
///                 var missing = e.Result.MissingNodeTypes.Count > 0 ? $" (unknown types: {string.Join(", ", e.Result.MissingNodeTypes)})" : "";
///                 status.Text = $"Paste failed: {e.Result.Error}{missing}";
///             };
///         }
///     }
///     ]]></code>
/// </example>
public sealed class PasteFailedEventArgs : EventArgs
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="PasteFailedEventArgs"/> class.
    /// </summary>
    /// <param name="result">The failed paste result.</param>
    public PasteFailedEventArgs(PasteResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        Result = result;
    }

    /// <summary>
    ///     Gets the failed paste result, whose <see cref="PasteResult.Error"/> and <see cref="PasteResult.MissingNodeTypes"/> explain the failure.
    /// </summary>
    public PasteResult Result { get; }
}

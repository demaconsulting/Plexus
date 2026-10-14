namespace DemaConsulting.Plexus.Avalonia;

/// <summary>
///     Describes a clipboard command started by a keyboard shortcut that failed in the platform clipboard.
/// </summary>
/// <remarks>
///     Ctrl+C, Ctrl+X and Ctrl+V start asynchronous clipboard work from a synchronous key handler,
///     so there is no caller to receive an exception. Instead of dropping the failure silently the
///     editor raises <see cref="DiagramEditor.ClipboardFailed"/>, and the host can tell the user
///     (for example in a status bar). Typical causes are another process holding the clipboard or a
///     locked desktop session. The exception's <see cref="Exception.Message"/> can be misleading for
///     COM failures, because Windows may attach an unrelated, stale error description from the same
///     thread; rely on <see cref="Exception.HResult"/> to identify the failure. The event is raised only for the shortcuts; the public
///     <c>CopyAsync</c>, <c>CutAsync</c> and <c>PasteAsync</c> methods throw instead, so a menu handler needs its own try/catch.
/// </remarks>
/// <example>
///     Show the failure of a Ctrl+C, Ctrl+X or Ctrl+V in a status line.
///     <code><![CDATA[
///     using Avalonia.Controls;
///     using DemaConsulting.Plexus.Avalonia;
///
///     static class ClipboardSample
///     {
///         public static void Wire(DiagramEditor editor, TextBlock status)
///         {
///             editor.ClipboardFailed += (_, e) => status.Text = $"Clipboard failed (0x{e.Error.HResult:X8}): {e.Error.GetType().Name}";
///         }
///     }
///     ]]></code>
/// </example>
public sealed class ClipboardFailedEventArgs : EventArgs
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ClipboardFailedEventArgs"/> class.
    /// </summary>
    /// <param name="error">The exception thrown by the platform clipboard.</param>
    public ClipboardFailedEventArgs(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        Error = error;
    }

    /// <summary>
    ///     Gets the exception thrown by the platform clipboard.
    /// </summary>
    public Exception Error { get; }
}

namespace DemaConsulting.Plexus.Demo.Gui;

/// <summary>
///     Builds the text shown when the operating system clipboard cannot be used.
/// </summary>
/// <remarks>
///     The text names the HRESULT and deliberately omits the exception message for COM failures:
///     Windows attaches the last error description of the thread, which can belong to an unrelated
///     earlier call (observed: a UI Automation "oldValue has incorrect type for this property" text
///     on a plain <c>CLIPBRD_E_CANT_OPEN</c>), so the message alone would point at the wrong cause.
/// </remarks>
internal static class ClipboardMessage
{
    /// <summary>
    ///     The HRESULT Windows reports when the clipboard is held by another process or the session is locked.
    /// </summary>
    private const int ClipboardCantOpen = unchecked((int)0x800401D0);

    /// <summary>
    ///     Describes a clipboard failure for the status bar and automation replies.
    /// </summary>
    /// <param name="error">The exception thrown by the platform clipboard.</param>
    /// <returns>A one-line, human-readable explanation that starts with "The clipboard is not available".</returns>
    public static string Describe(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        if (error is System.Runtime.InteropServices.ExternalException)
        {
            var reason = error.HResult == ClipboardCantOpen
                ? "another process is using it or the desktop session is locked"
                : "the platform refused the request";
            return $"The clipboard is not available (error 0x{error.HResult:X8}): {reason}.";
        }

        return $"The clipboard is not available: {error.Message}";
    }
}

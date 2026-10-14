namespace DemaConsulting.Plexus.Puppet;

/// <summary>
///     The process exit codes of <c>plexus-puppet</c>; every command ends with exactly one of them.
/// </summary>
internal static class ExitCodes
{
    /// <summary>
    ///     The command did what was asked.
    /// </summary>
    internal const int Success = 0;

    /// <summary>
    ///     An element, node or other target was not found.
    /// </summary>
    internal const int NotFound = 1;

    /// <summary>
    ///     A wait ran out of time.
    /// </summary>
    internal const int Timeout = 2;

    /// <summary>
    ///     The addressed <c>plexus-demo</c> instance is not running (or does not answer).
    /// </summary>
    internal const int NotRunning = 3;

    /// <summary>
    ///     Launching an instance failed or was refused.
    /// </summary>
    internal const int LaunchFailed = 4;

    /// <summary>
    ///     The command line was wrong.
    /// </summary>
    internal const int UsageError = 5;

    /// <summary>
    ///     The addressed element is disabled.
    /// </summary>
    internal const int Disabled = 6;

    /// <summary>
    ///     No usable keyboard focus.
    /// </summary>
    internal const int NoFocus = 7;

    /// <summary>
    ///     The input had no effect.
    /// </summary>
    internal const int NoEffect = 8;

    /// <summary>
    ///     The element is read-only.
    /// </summary>
    internal const int ReadOnly = 9;

    /// <summary>
    ///     A dialog is open (reported by <c>dialogs</c>).
    /// </summary>
    internal const int BlockedOnDialog = 10;

    /// <summary>
    ///     Real mouse input was refused because the target point is not on the instance's window (covered, off screen or
    ///     the window cannot be brought to the foreground, for example on a locked desktop).
    /// </summary>
    internal const int Obscured = 11;

    /// <summary>
    ///     The application rejected a pipe command (<c>ok:false</c>); the reason is printed.
    /// </summary>
    internal const int PipeError = 12;

    /// <summary>
    ///     An unexpected error.
    /// </summary>
    internal const int Error = 99;
}

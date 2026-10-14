namespace DemaConsulting.Plexus.Puppet;

/// <summary>
///     A failure that ends a command with a specific exit code and a one-line explanation.
/// </summary>
public sealed class PuppetException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="PuppetException"/> class.
    /// </summary>
    /// <param name="exitCode">The exit code the process ends with.</param>
    /// <param name="label">The upper-case label printed before the message, such as <c>NOT_FOUND</c>.</param>
    /// <param name="message">What went wrong.</param>
    internal PuppetException(int exitCode, string label, string message)
        : base(message)
    {
        ExitCode = exitCode;
        Label = label;
    }

    /// <summary>
    ///     Gets the exit code the process ends with.
    /// </summary>
    internal int ExitCode { get; }

    /// <summary>
    ///     Gets the label printed before the message.
    /// </summary>
    internal string Label { get; }

    /// <summary>
    ///     Creates the failure for a wrong command line.
    /// </summary>
    /// <param name="usage">The correct usage, or the reason.</param>
    /// <returns>The exception.</returns>
    internal static PuppetException Usage(string usage) => new(ExitCodes.UsageError, "USAGE", usage);

    /// <summary>
    ///     Creates the failure for a target that is not on the instance's window.
    /// </summary>
    /// <param name="message">Why the input was refused.</param>
    /// <returns>The exception.</returns>
    internal static PuppetException Obscured(string message) => new(ExitCodes.Obscured, "OBSCURED", message);
}

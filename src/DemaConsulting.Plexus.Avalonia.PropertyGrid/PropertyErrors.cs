using System.Reflection;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid;

/// <summary>
///     Converts exceptions raised by user-supplied code (property accessors, type converters, validation attributes)
///     into error messages, so they can be reported as <see cref="PropertyResult"/> failures.
/// </summary>
internal static class PropertyErrors
{
    /// <summary>
    ///     Reports whether an exception must not be swallowed.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <returns><see langword="true"/> for out-of-memory and similar process-level failures.</returns>
    internal static bool IsFatal(Exception exception) =>
        exception is OutOfMemoryException or StackOverflowException or AccessViolationException or ThreadAbortException;

    /// <summary>
    ///     Gets the user-facing message of the innermost reflection-wrapped exception.
    /// </summary>
    /// <remarks>
    ///     The " (Parameter 'value')" suffix that <see cref="ArgumentException"/> appends to its message is removed, because it names
    ///     an implementation detail a user cannot act on.
    /// </remarks>
    /// <param name="exception">The exception.</param>
    /// <returns>A non-empty message.</returns>
    internal static string Message(Exception exception)
    {
        var current = exception;
        while (current is TargetInvocationException { InnerException: { } inner })
        {
            current = inner;
        }

        var message = current.Message;
        if (current is ArgumentException { ParamName: { Length: > 0 } parameter })
        {
            message = message.Replace($" (Parameter '{parameter}')", string.Empty, StringComparison.Ordinal);
        }

        return string.IsNullOrWhiteSpace(message) ? current.GetType().Name : message;
    }

    /// <summary>
    ///     Runs an action, converting a non-fatal exception into a failed result.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <returns>The result of the action.</returns>
    internal static PropertyResult Run(Action action)
    {
        try
        {
            action();
            return PropertyResult.Success;
        }
        catch (Exception ex) when (!IsFatal(ex))
        {
            return PropertyResult.Failure(Message(ex));
        }
    }
}

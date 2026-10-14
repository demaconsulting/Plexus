namespace DemaConsulting.Plexus.Avalonia.PropertyGrid;

/// <summary>
///     The outcome of reading, converting, validating or writing a property value. Failures are reported as a result
///     instead of an exception so a user interface can show the message next to the property.
/// </summary>
/// <remarks>
///     Exceptions raised by property getters and setters, by <see cref="System.ComponentModel.TypeConverter"/>
///     implementations and by validation attributes are caught by the <c>Try...</c> members of
///     <see cref="PropertyItem"/> and <see cref="PropertyEditSession"/> and converted into a failed result whose
///     <see cref="Error"/> is the (unwrapped) exception message.
/// </remarks>
/// <param name="Succeeded"><see langword="true"/> when the operation succeeded.</param>
/// <param name="Error">The human-readable error message when the operation failed; otherwise <see langword="null"/>.</param>
/// <example>
///     <code><![CDATA[
///     using DemaConsulting.Plexus.Avalonia.PropertyGrid;
///
///     var settings = new Settings();
///     using var source = new ReflectionPropertySource(settings);
///     PropertyItem item = source.GetItems().First(i => i.Name == "Retries");
///
///     PropertyResult result = item.TrySetText("not a number");
///     if (!result.Succeeded)
///     {
///         Console.WriteLine(result.Error);                  // show it, for example in a status bar
///     }
///
///     public class Settings
///     {
///         public int Retries { get; set; } = 3;
///     }
///     ]]></code>
/// </example>
public readonly record struct PropertyResult(bool Succeeded, string? Error)
{
    /// <summary>
    ///     Gets a successful result.
    /// </summary>
    public static PropertyResult Success => new(true, null);

    /// <summary>
    ///     Creates a failed result.
    /// </summary>
    /// <param name="error">The error message.</param>
    /// <returns>A failed result carrying <paramref name="error"/>.</returns>
    public static PropertyResult Failure(string error) => new(false, error);
}

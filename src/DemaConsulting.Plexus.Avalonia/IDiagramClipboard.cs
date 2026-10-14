namespace DemaConsulting.Plexus.Avalonia;

/// <summary>
///     The clipboard the <see cref="DiagramEditor"/> copies to and pastes from.
/// </summary>
/// <remarks>
///     By default the editor uses the operating system clipboard of the window it is shown in, so
///     this interface only needs implementing by hosts that want a different store: a private
///     in-app clipboard, a test double, or a remote session. Implementations exchange the JSON text
///     produced by <see cref="PlexusClipboard.Copy"/>; the default implementation additionally tags
///     the data with <see cref="PlexusClipboard.FormatId"/> and also offers it as plain text, so
///     pasting between separate application instances works.
/// </remarks>
/// <example>
///     A private in-app clipboard (also a good test double): the text never touches the operating system clipboard.
///     <code><![CDATA[
///     using DemaConsulting.Plexus.Avalonia;
///
///     var editor = new DiagramEditor
///     {
///         Clipboard = new InMemoryClipboard(),               // copy and paste now stay inside this application
///     };
///
///     public sealed class InMemoryClipboard : IDiagramClipboard
///     {
///         private string? _json;
///
///         public Task SetAsync(string json)
///         {
///             _json = json;
///             return Task.CompletedTask;
///         }
///
///         public Task<string?> GetAsync() => Task.FromResult(_json);   // null: nothing to paste
///     }
///     ]]></code>
/// </example>
public interface IDiagramClipboard
{
    /// <summary>
    ///     Replaces the clipboard content with Plexus clipboard JSON.
    /// </summary>
    /// <param name="json">The JSON produced by <see cref="PlexusClipboard.Copy"/>.</param>
    /// <returns>A task that completes when the clipboard has been written.</returns>
    Task SetAsync(string json);

    /// <summary>
    ///     Reads Plexus clipboard JSON, if the clipboard holds any.
    /// </summary>
    /// <returns>A task producing the JSON (or, for text-only clipboards, whatever text is there), or null when the clipboard is empty.</returns>
    Task<string?> GetAsync();
}

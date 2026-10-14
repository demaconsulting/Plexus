using Avalonia.Input;
using Avalonia.Input.Platform;

namespace DemaConsulting.Plexus.Avalonia;

/// <summary>
///     The default <see cref="IDiagramClipboard"/>: the operating system clipboard of the editor's window.
/// </summary>
/// <remarks>
///     Data is written in two formats inside one item: an application-specific format named
///     <see cref="PlexusClipboard.FormatId"/> and plain text holding the very same JSON. Readers
///     prefer the specific format, then fall back to text, which is what lets a paste work even
///     when the other application only forwards text (remote desktops, terminals, chat windows).
/// </remarks>
internal sealed class SystemDiagramClipboard : IDiagramClipboard
{
    /// <summary>
    ///     The application format Plexus data is tagged with.
    /// </summary>
    private static readonly DataFormat<string> PlexusFormat = DataFormat.CreateStringApplicationFormat(PlexusClipboard.FormatId);

    /// <summary>
    ///     The platform clipboard.
    /// </summary>
    private readonly IClipboard _clipboard;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SystemDiagramClipboard"/> class.
    /// </summary>
    /// <param name="clipboard">The platform clipboard to wrap.</param>
    public SystemDiagramClipboard(IClipboard clipboard)
    {
        _clipboard = clipboard;
    }

    /// <inheritdoc />
    public async Task SetAsync(string json)
    {
        // One item carrying both representations keeps them together as a single clipboard entry
        var item = new DataTransferItem();
        item.Set(PlexusFormat, json);
        item.SetText(json);
        var transfer = new DataTransfer();
        transfer.Add(item);
        await _clipboard.SetDataAsync(transfer).ConfigureAwait(true);
    }

    /// <inheritdoc />
    public async Task<string?> GetAsync()
    {
        // Prefer the exact format; plain text is the cross-application fallback
        var exact = await _clipboard.TryGetValueAsync(PlexusFormat).ConfigureAwait(true);
        return exact ?? await _clipboard.TryGetTextAsync().ConfigureAwait(true);
    }
}

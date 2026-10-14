using Avalonia.Controls;
using AvaloniaPoint = Avalonia.Point;
using PlexusPoint = DemaConsulting.Plexus.Geometry.Point;

namespace DemaConsulting.Plexus.Avalonia;

/// <content>
///     Clipboard and selection commands: copy, cut, paste, delete and duplicate.
/// </content>
public sealed partial class DiagramEditor
{

    /// <summary>
    ///     Occurs when a paste is rejected; <see cref="PasteFailedEventArgs.Result"/> explains why.
    /// </summary>
    /// <remarks>
    ///     Raised for the Ctrl+V shortcut and for <see cref="PasteAsync()"/> alike, with a failed <c>PasteResult</c>: show its <c>Message</c> (a ready-made sentence that names the missing node types or gives the reason) in a status bar. <c>Error</c> is the bare reason (for example "The clipboard text is empty." or "Unknown node type(s): demo.Extra.") and <c>MissingNodeTypes</c> lists the types this
    ///     diagram does not know. An empty or foreign clipboard counts as a failure too, so hosts that do not want to bother the user about it can
    ///     ignore failures whose error mentions an empty clipboard. Show the message in a status bar rather than a modal dialog.
    /// </remarks>
    public event EventHandler<PasteFailedEventArgs>? PasteFailed;

    /// <summary>
    ///     Occurs when a clipboard command started by a keyboard shortcut (Ctrl+C, Ctrl+X, Ctrl+V) fails in the platform clipboard.
    /// </summary>
    /// <remarks>
    ///     The public <see cref="CopyAsync"/>, <see cref="CutAsync"/> and <see cref="PasteAsync()"/> methods
    ///     let platform exceptions reach their caller; only shortcut-started commands, which have no
    ///     caller, raise this event. See <see cref="ClipboardFailedEventArgs"/>.
    /// </remarks>
    public event EventHandler<ClipboardFailedEventArgs>? ClipboardFailed;

    /// <summary>
    ///     Gets or sets the clipboard used by copy, cut and paste; null (the default) uses the operating system clipboard.
    /// </summary>
    /// <remarks>
    ///     Assign an <see cref="IDiagramClipboard"/> to keep the clipboard private to the application,
    ///     or to substitute a test double.
    /// </remarks>
    public IDiagramClipboard? Clipboard { get; set; }

    /// <summary>
    ///     Deletes the selected elements as one undoable step.
    /// </summary>
    /// <remarks>
    ///     Deleting a node removes its wires. Deleting an expanded group removes only its frame
    ///     (members stay), while a collapsed group is deleted together with its hidden members, since
    ///     removing just the box would silently resurrect elements the user could not see.
    ///     Does nothing when the editor is read-only.
    /// </remarks>
    public void DeleteSelection()
    {
        if (IsReadOnly || LivePage() is null || _selection.Count == 0)
        {
            return;
        }

        Delete([.. _selection]);

        // The model events already pruned the selection; this makes it explicit for elements removed silently
        ClearSelection();
    }

    /// <summary>
    ///     Deletes the given elements as one undoable step, without touching the selection.
    /// </summary>
    /// <remarks>
    ///     The same rules as <see cref="DeleteSelection"/> apply: deleting a node removes its wires,
    ///     deleting an expanded group removes only its frame, and a collapsed group is deleted together with
    ///     its hidden members. Elements that are not on the shown page (stale references, other pages) are
    ///     ignored. Deleted elements drop out of <see cref="Selection"/> automatically; other selected
    ///     elements stay selected. Does nothing when the editor is read-only.
    /// </remarks>
    /// <param name="elements">The elements to delete.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="elements"/> is null.</exception>
    public void Delete(IEnumerable<DiagramElement> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);
        if (IsReadOnly || LivePage() is not { } page)
        {
            return;
        }

        var doomed = elements.Where(e => ReferenceEquals(e.Page, page)).Distinct().ToList();
        if (doomed.Count == 0)
        {
            return;
        }

        using (page.Diagram.History.BeginTransaction("Delete"))
        {
            // Collapsed groups take their members with them; whatever is still on the page after that goes too
            page.RemoveWithMembers(doomed.OfType<Group>().Where(g => g.IsCollapsed));
            page.Remove(doomed.Where(e => ReferenceEquals(e.Page, page)));
        }
    }

    /// <summary>
    ///     Duplicates the selected elements, offset by one grid cell, and selects the copies (one undoable step).
    /// </summary>
    /// <returns>The created elements; empty when nothing was selected or the editor is read-only.</returns>
    public IReadOnlyList<DiagramElement> DuplicateSelection()
    {
        if (IsReadOnly || LivePage() is not { } page || _selection.Count == 0)
        {
            return [];
        }

        var grid = EffectiveTheme.GridSize;
        var copies = page.Duplicate([.. _selection], grid, grid);
        SetSelection(copies);
        return copies;
    }

    /// <summary>
    ///     Copies the selection to the clipboard.
    /// </summary>
    /// <remarks>
    ///     The data is Plexus clipboard JSON, written in the <see cref="PlexusClipboard.FormatId"/>
    ///     format and as plain text, so it can be pasted into another application instance. Wires
    ///     are included when both of their nodes are. Does nothing when the selection is empty.
    ///     Failures of the operating system clipboard (another process holds it, no clipboard in the session) are NOT caught here: they
    ///     reach the caller as exceptions (typically <see cref="InvalidOperationException"/>, <see cref="IOException"/> or a COM
    ///     <see cref="System.Runtime.InteropServices.ExternalException"/>). Wrap the call in try/catch in a menu handler, especially an
    ///     <c>async void</c> one, which would otherwise crash the application. The Ctrl+C shortcut has no caller, so it reports the same failures through <see cref="ClipboardFailed"/>.
    /// </remarks>
    /// <returns>A task that completes when the clipboard has been written.</returns>
    public async Task CopyAsync()
    {
        if (_selection.Count == 0)
        {
            return;
        }

        var json = PlexusClipboard.Copy([.. _selection]);
        if (ResolveClipboard() is { } clipboard)
        {
            await clipboard.SetAsync(json).ConfigureAwait(true);
        }
    }

    /// <summary>
    ///     Copies the selection to the clipboard and deletes it.
    /// </summary>
    /// <remarks>
    ///     Like <see cref="CopyAsync"/>, platform clipboard exceptions reach the caller (nothing is deleted then). Does nothing when the editor is read-only or the selection is empty.
    /// </remarks>
    /// <returns>A task that completes when the clipboard has been written and the elements removed.</returns>
    public async Task CutAsync()
    {
        if (IsReadOnly || _selection.Count == 0)
        {
            return;
        }

        await CopyAsync().ConfigureAwait(true);
        DeleteSelection();
    }

    /// <summary>
    ///     Pastes the clipboard content onto the page and selects the pasted elements.
    /// </summary>
    /// <remarks>
    ///     When the pointer is over the editor the pasted elements are placed at the pointer
    ///     (snapped to the grid); otherwise (for example when pasting from a menu, where the pointer is over the menu) they are offset from the originals by Core's default (one grid step).
    ///     The paste is one undoable step. Clipboard content is untrusted: a failure (not Plexus
    ///     data, an empty clipboard, unknown node types, too large, and so on) never throws, it raises
    ///     <see cref="PasteFailed"/> and leaves the page untouched, and the returned result has <c>Success</c> false.
    ///     Failures of the operating system clipboard itself are different: they are not caught here and reach the caller as exceptions
    ///     (see <see cref="CopyAsync"/>); the Ctrl+V shortcut reports them through <see cref="ClipboardFailed"/>.
    /// </remarks>
    /// <returns>A task producing the paste result (<c>Task&lt;PasteResult?&gt;</c>: the result is null when the editor is read-only, shows no page, or has no clipboard).</returns>
    public Task<PasteResult?> PasteAsync() => PasteCoreAsync(useAt: false, at: default);

    /// <summary>
    ///     Pastes the clipboard content at a given diagram position and selects the pasted elements.
    /// </summary>
    /// <remarks>
    ///     Like <see cref="PasteAsync()"/> but the destination is chosen by the caller, which is what a
    ///     "Paste here" context-menu entry or a scripted host needs. The position is used as given
    ///     (it is not snapped to the grid).
    /// </remarks>
    /// <param name="at">The diagram position that receives the top-left of the pasted elements.</param>
    /// <returns>A task producing the paste result, or null when the editor is read-only, shows no page, or has no clipboard.</returns>
    public Task<PasteResult?> PasteAsync(PlexusPoint at) => PasteCoreAsync(useAt: true, at);

    /// <summary>
    ///     Reads the clipboard and pastes it, either where the caller says or at the default position.
    /// </summary>
    /// <param name="useAt">Whether <paramref name="at"/> is used instead of the pointer-based default.</param>
    /// <param name="at">The explicit diagram position.</param>
    /// <returns>A task producing the paste result, or null when nothing could be pasted into.</returns>
    private async Task<PasteResult?> PasteCoreAsync(bool useAt, PlexusPoint at)
    {
        if (IsReadOnly || LivePage() is not { } page || ResolveClipboard() is not { } clipboard)
        {
            return null;
        }

        // Read first: the page or pointer may change while the clipboard is awaited, so look at them afterwards
        var json = await clipboard.GetAsync().ConfigureAwait(true) ?? string.Empty;
        if (!ReferenceEquals(LivePage(), page))
        {
            return null;
        }

        var result = PlexusClipboard.Paste(page, json, useAt ? at : PastePosition());
        if (result.Success)
        {
            SetSelection(result.Elements);
        }
        else
        {
            PasteFailed?.Invoke(this, new PasteFailedEventArgs(result));
        }

        return result;
    }

    /// <summary>
    ///     Chooses where pasted elements go: at the pointer when it is over the editor.
    /// </summary>
    /// <returns>The snapped diagram position, or null to let Core offset the paste.</returns>
    private PlexusPoint? PastePosition()
    {
        if (!IsPointerOver || _pointerPosition is not { } screen)
        {
            return null;
        }

        var position = ToDiagram(screen);
        return new PlexusPoint(Snap(position.X), Snap(position.Y));
    }

    /// <summary>
    ///     Finds the clipboard to use: the host's, else the system clipboard of the window.
    /// </summary>
    /// <returns>The clipboard, or null when none is available (the editor is not in a window).</returns>
    private IDiagramClipboard? ResolveClipboard() =>
        Clipboard ?? (TopLevel.GetTopLevel(this)?.Clipboard is { } system ? new SystemDiagramClipboard(system) : null);
}

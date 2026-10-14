using System.Collections.ObjectModel;
using PlexusPage = DemaConsulting.Plexus.DiagramPage;

namespace DemaConsulting.Plexus.Avalonia;

/// <content>
///     Selection: which elements are highlighted and are the target of delete, copy, duplicate and drag.
/// </content>
public sealed partial class DiagramEditor
{
    /// <summary>
    ///     The mutable selection list.
    /// </summary>
    private readonly ObservableCollection<DiagramElement> _selection = [];

    /// <summary>
    ///     The read-only view of the selection handed to hosts.
    /// </summary>
    private readonly ReadOnlyObservableCollection<DiagramElement> _selectionView;

    /// <summary>
    ///     Occurs after the set of selected elements changed, once per change however many elements moved in or out.
    /// </summary>
    public event EventHandler? SelectionChanged;

    /// <summary>
    ///     Gets the selected elements; observable, but only the editor changes it.
    /// </summary>
    /// <remarks>
    ///     Selection is view state: it is never recorded in the undo history. Elements that leave the
    ///     page (deleted, moved to another page with <see cref="Diagram.MoveToPage"/>, or removed by undo) drop out of the selection automatically.
    /// </remarks>
    public ReadOnlyObservableCollection<DiagramElement> Selection => _selectionView;

    /// <summary>
    ///     Selects exactly one element, replacing the current selection.
    /// </summary>
    /// <param name="element">The element, which must be on the shown page.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="element"/> is null.</exception>
    public void Select(DiagramElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        SetSelection([element]);
    }

    /// <summary>
    ///     Selects elements, either replacing the current selection or adding to it.
    /// </summary>
    /// <remarks>
    ///     Elements that are not on the shown page are ignored, so stale references are harmless.
    /// </remarks>
    /// <param name="elements">The elements to select.</param>
    /// <param name="add">Optional; defaults to false. When true the elements are added to the selection; when false they replace it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="elements"/> is null.</exception>
    public void Select(IEnumerable<DiagramElement> elements, bool add = false)
    {
        ArgumentNullException.ThrowIfNull(elements);
        SetSelection(add ? _selection.Concat(elements) : elements);
    }

    /// <summary>
    ///     Clears the selection.
    /// </summary>
    public void ClearSelection() => SetSelection([]);

    /// <summary>
    ///     Selects every visible element of the page.
    /// </summary>
    /// <remarks>
    ///     Members of collapsed groups are skipped because they cannot be seen, and neither are wires
    ///     both of whose nodes are hidden.
    /// </remarks>
    public void SelectAll()
    {
        if (Page is not { } page)
        {
            return;
        }

        SetSelection(page.Elements.Where(e => e.IsVisible && !(e is Wire w && !w.Source.Node.IsVisible && !w.Target.Node.IsVisible)));
    }

    /// <summary>
    ///     Replaces the selection, raising <see cref="SelectionChanged"/> once and only when the set really changed.
    /// </summary>
    /// <param name="elements">The new selection; elements not on the shown page are dropped and duplicates ignored.</param>
    internal void SetSelection(IEnumerable<DiagramElement> elements)
    {
        // Normalize: only live elements of this page, each once, in the order given
        var page = Page;
        var seen = new HashSet<DiagramElement>();
        var wanted = elements
            .Where(e => page is not null && ReferenceEquals(e.Page, page) && seen.Add(e))
            .ToList();
        // An unchanged selection must not cost a rebuild or notify anyone
        if (wanted.Count == _selection.Count && wanted.All(_selection.Contains))
        {
            return;
        }

        _selection.Clear();
        wanted.ForEach(_selection.Add);

        InvalidateScene();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    ///     Drops selected elements that are no longer on the page (after delete or undo).
    /// </summary>
    private void PruneSelection()
    {
        if (_selection.Count > 0)
        {
            SetSelection(_selection.ToList());
        }
    }

    /// <summary>
    ///     Gets the selected elements that can be moved (everything except wires, which follow their nodes).
    /// </summary>
    /// <returns>A snapshot of the movable selected elements.</returns>
    internal List<DiagramElement> MovableSelection() => [.. _selection.Where(e => e is not Wire)];

    /// <summary>
    ///     Gets whether an element is selected.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <returns><see langword="true"/> when the element is in the selection.</returns>
    internal bool IsSelected(DiagramElement element) => _selection.Contains(element);

    /// <summary>
    ///     Gets the shown page when it is attached to its diagram, or null.
    /// </summary>
    /// <returns>The live page, or null when none is shown or it was removed from its diagram.</returns>
    internal PlexusPage? LivePage() => Page is { IsAttached: true } page ? page : null;
}

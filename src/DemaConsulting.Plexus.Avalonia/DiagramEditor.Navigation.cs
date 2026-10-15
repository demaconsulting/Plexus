using DemaConsulting.Plexus.Scene;
using PlexusPage = DemaConsulting.Plexus.DiagramPage;
using PlexusRect = DemaConsulting.Plexus.Geometry.Rect;

namespace DemaConsulting.Plexus.Avalonia;

/// <content>
///     Navigation: bringing an element into view and fitting a set of elements, switching pages when needed.
/// </content>
public sealed partial class DiagramEditor
{
    /// <summary>
    ///     The margin, in device-independent pixels, kept around an element brought into view when it has to be zoomed out to fit.
    /// </summary>
    private const double NavigationMargin = 30;

    /// <summary>
    ///     Scrolls so an element is visible, centered in the control, switching <see cref="Page"/> to the element's page when needed.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The zoom is kept unless the element's box does not fit in the control minus a margin of 30 device-independent pixels on each
    ///         side; then the editor zooms out just enough to show it (never below <see cref="MinZoom"/>). It never zooms in.
    ///         An element hidden inside a collapsed group cannot be shown, so the outermost collapsed group that hides it is brought into
    ///         view instead (for a wire, both ends' visible boxes). A wire is shown by the bounding box of its route.
    ///     </para>
    ///     <para>
    ///         Switching pages works like setting <see cref="Page"/> (it keeps a binding on it in place): the selection is cleared and any
    ///         gesture or inline edit is canceled, so select the element afterwards if wanted. Like <see cref="ZoomToFit(double)"/> it may be
    ///         called before the first layout; it is then applied once the editor has a size. A newer view request replaces a pending one, and a
    ///         pending request is dropped when the element has left the page by the time it can be applied.
    ///     </para>
    /// </remarks>
    /// <example>
    ///     A "Find node" command: look the node up by name and show it.
    ///     <code><![CDATA[
    ///     if (diagram.FindNode(name) is { } node)
    ///     {
    ///         editor.BringIntoView(node);   // switches to the node's page when needed
    ///         editor.Select(node);          // after the page switch, which clears the selection
    ///     }
    ///     ]]></code>
    /// </example>
    /// <param name="element">
    ///     The element to show: any element on an attached page of the diagram the editor shows (of any diagram when the editor shows no page).
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="element"/> is null.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when the element is not on a page of a diagram, or belongs to another diagram than the shown page. The
    ///     <see cref="Page"/>, <see cref="Zoom"/>, <see cref="Offset"/> and selection are then unchanged.
    /// </exception>
    public void BringIntoView(DiagramElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        var page = RequireNavigablePage(element, nameof(element));

        // Remember the request first: a page switch applies it as soon as the new page is shown
        _pendingView = () => TryBringIntoView(element);
        ShowPage(page);
        ApplyPendingView();
    }

    /// <summary>
    ///     Zooms and scrolls so a set of elements fits the control, centered, switching <see cref="Page"/> to their page when needed.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The elements are fitted like the whole page by <see cref="ZoomToFit(double)"/>: nodes count with their names, summaries and
    ///         port labels, and elements hidden in a collapsed group count as that group. The zoom is the one that fits the union of the
    ///         elements plus the margin on each side into the control, but at most 1 (actual size; a small set is centered rather than
    ///         magnified), then clamped to <see cref="MinZoom"/> to <see cref="MaxZoom"/>. Use it to show the
    ///         selection (<c>editor.ZoomToFit(editor.Selection)</c>; the list is copied before anything changes) or search results.
    ///     </para>
    ///     <para>
    ///         All elements should be on one page. When they span pages, the page of the first element is shown and the elements on other
    ///         pages are ignored. An empty sequence changes nothing (a pending earlier request stays pending). It may be called before the
    ///         first layout; it is then applied once the editor has a size, and the latest request of <see cref="BringIntoView"/>,
    ///         <see cref="ZoomToFit(double)"/> and this method replaces any pending one.
    ///     </para>
    /// </remarks>
    /// <example>
    ///     A "Zoom to Selection" command, and showing the nodes a search found.
    ///     <code><![CDATA[
    ///     editor.ZoomToFit(editor.Selection);   // the selection is copied before anything changes; empty does nothing
    ///
    ///     editor.ZoomToFit(page.Nodes.Where(n => n.Name.Contains(text, StringComparison.OrdinalIgnoreCase)));
    ///     ]]></code>
    /// </example>
    /// <param name="elements">The elements to show; all must be on pages of the same diagram.</param>
    /// <param name="margin">The empty space, in device-independent pixels on each side, to keep around the elements (default 30); finite and not negative.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="elements"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown, before anything changes, when <paramref name="margin"/> is negative, NaN or infinite.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when an entry is null, is not on a page of a diagram, or belongs to another diagram than the shown page or the
    ///     first element. The <see cref="Page"/>, <see cref="Zoom"/>, <see cref="Offset"/> and selection are then unchanged.
    /// </exception>
    public void ZoomToFit(IEnumerable<DiagramElement> elements, double margin = 30)
    {
        ArgumentNullException.ThrowIfNull(elements);

        // Validate the margin before anything is read or changed
        if (!double.IsFinite(margin) || margin < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(margin), margin, "The margin must be a finite number that is not negative.");
        }

        // Copy first: callers pass the live selection, which a page switch clears
        var list = elements.ToList();
        if (list.Count == 0)
        {
            return;
        }

        // Validate every entry before anything changes; the first element decides the page
        var page = RequireNavigablePage(list[0], nameof(elements));
        if (list.Exists(element => !ReferenceEquals(RequireNavigablePage(element, nameof(elements)).Diagram, page.Diagram)))
        {
            throw new ArgumentException("All elements must belong to the same diagram.", nameof(elements));
        }

        _pendingView = () => TryZoomToFitElements(page, list, margin);
        ShowPage(page);
        ApplyPendingView();
    }

    /// <summary>
    ///     Checks that an element can be navigated to and returns its page.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="paramName">The parameter name for exceptions.</param>
    /// <returns>The element's attached page.</returns>
    /// <exception cref="ArgumentException">Thrown when the element is null, not on an attached page, or belongs to another diagram than the shown page.</exception>
    private PlexusPage RequireNavigablePage(DiagramElement? element, string paramName)
    {
        if (element is null)
        {
            throw new ArgumentException("The elements contain a null entry.", paramName);
        }

        if (element.Page is not { IsAttached: true } page)
        {
            throw new ArgumentException("The element is not on a page of a diagram.", paramName);
        }

        if (Page is { } shown && !ReferenceEquals(shown.Diagram, page.Diagram))
        {
            throw new ArgumentException("The element belongs to another diagram than the page the editor shows.", paramName);
        }

        return page;
    }

    /// <summary>
    ///     Shows a page, keeping a binding on <see cref="Page"/> in place; does nothing when it is already shown.
    /// </summary>
    /// <param name="page">The page to show.</param>
    private void ShowPage(PlexusPage page)
    {
        if (!ReferenceEquals(Page, page))
        {
            SetCurrentValue(PageProperty, page);
        }
    }

    /// <summary>
    ///     Centers an element when that is possible now.
    /// </summary>
    /// <param name="element">The element to show.</param>
    /// <returns><see langword="false"/> when the editor has no usable size yet; otherwise <see langword="true"/> (also when the element has left the shown page and the request is dropped).</returns>
    private bool TryBringIntoView(DiagramElement element)
    {
        if (Page is not { } page || !ReferenceEquals(element.Page, page))
        {
            return true;
        }

        if (Bounds.Width <= 2 * NavigationMargin || Bounds.Height <= 2 * NavigationMargin)
        {
            return false;
        }

        // Zoom out only when the element does not fit at the current zoom, then center it
        var box = TargetBounds(element);
        var availableWidth = Bounds.Width - (2 * NavigationMargin);
        var availableHeight = Bounds.Height - (2 * NavigationMargin);
        if (box.Width * Zoom > availableWidth || box.Height * Zoom > availableHeight)
        {
            Zoom = Math.Min(availableWidth / Math.Max(box.Width, 1), availableHeight / Math.Max(box.Height, 1));
        }

        CenterOn(box);
        return true;
    }

    /// <summary>
    ///     Fits a set of elements when that is possible now.
    /// </summary>
    /// <param name="page">The page the request is for.</param>
    /// <param name="elements">The elements; those no longer on the page are ignored.</param>
    /// <param name="margin">The empty space to keep around them.</param>
    /// <returns><see langword="false"/> when the editor has no usable size yet; otherwise <see langword="true"/> (also when the request is dropped).</returns>
    private bool TryZoomToFitElements(PlexusPage page, IReadOnlyList<DiagramElement> elements, double margin)
    {
        if (!ReferenceEquals(Page, page))
        {
            return true;
        }

        if (Bounds.Width <= 2 * margin || Bounds.Height <= 2 * margin)
        {
            return false;
        }

        // Union of what is visible of each element still on the page
        PlexusRect? union = null;
        foreach (var element in elements.Where(e => ReferenceEquals(e.Page, page)))
        {
            var box = TargetBounds(element);
            union = union is { } current ? current.Union(box) : box;
        }

        if (union is { } fit)
        {
            FitBox(fit, margin);
        }

        return true;
    }

    /// <summary>
    ///     Computes the diagram rectangle that shows an element: its drawn extent, or the collapsed group that hides it.
    /// </summary>
    /// <param name="element">An element on the shown page.</param>
    /// <returns>The rectangle in diagram coordinates.</returns>
    private PlexusRect TargetBounds(DiagramElement element)
    {
        // A wire with a hidden end is shown by the visible boxes of both ends
        if (element is Wire wire && (!wire.Source.Node.IsVisible || !wire.Target.Node.IsVisible))
        {
            return TargetBounds(wire.Source.Node).Union(TargetBounds(wire.Target.Node));
        }

        // A hidden element is represented by the outermost collapsed group around it
        var shown = element;
        for (var group = element.Parent; group is not null; group = group.Parent)
        {
            if (group.IsCollapsed)
            {
                shown = group;
            }
        }

        return shown is Node node
            ? NodeLayout.Compute(node, EffectiveTheme, _measurer).Bounds
            : shown.GetBounds();
    }
}

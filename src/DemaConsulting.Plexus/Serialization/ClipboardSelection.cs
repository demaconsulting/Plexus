namespace DemaConsulting.Plexus;

/// <summary>
///     Expands a user selection into the exact set of elements that copy and duplicate carry along.
/// </summary>
/// <remarks>
///     Selecting a group takes its members (recursively), wires are carried only when both of their
///     nodes are carried (a selected wire alone has nothing to attach to), and the result is returned
///     in the page's z-order so pasted content stacks the way it did.
/// </remarks>
internal static class ClipboardSelection
{
    /// <summary>
    ///     Expands a selection.
    /// </summary>
    /// <param name="elements">The selected elements, all on one attached page.</param>
    /// <param name="page">Receives the page the elements are on, or null for an empty selection.</param>
    /// <returns>The elements to carry in z-order.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="elements"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when an element is null, detached, or the elements span several pages.</exception>
    public static List<DiagramElement> Expand(IEnumerable<DiagramElement> elements, out DiagramPage? page)
    {
        ArgumentNullException.ThrowIfNull(elements);
        page = null;
        var carried = new HashSet<DiagramElement>();
        foreach (var element in elements)
        {
            if (element is null)
            {
                throw new ArgumentException("The selection contains a null element.", nameof(elements));
            }

            // Every element must live on the same attached page, otherwise z-order and wires are meaningless
            if (element.Page is not { IsAttached: true } elementPage)
            {
                throw new ArgumentException("All elements must be on a page of a diagram.", nameof(elements));
            }

            if (page is not null && !ReferenceEquals(page, elementPage))
            {
                throw new ArgumentException("All elements must be on the same page.", nameof(elements));
            }

            page = elementPage;
            if (element is Wire)
            {
                continue;
            }

            carried.Add(element);
            if (element is Group group)
            {
                carried.UnionWith(group.AllMembers());
            }
        }

        if (page is null)
        {
            return [];
        }

        // A wire rides along only when both of its nodes do
        foreach (var wire in page.Wires.Where(w => carried.Contains(w.Source.Node) && carried.Contains(w.Target.Node)))
        {
            carried.Add(wire);
        }

        return [.. page.Elements.Where(carried.Contains)];
    }
}

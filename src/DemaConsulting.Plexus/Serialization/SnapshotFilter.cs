namespace DemaConsulting.Plexus;

/// <summary>
///     Removes the nodes of unknown types, and everything that depended on them, from page snapshots before they are loaded.
/// </summary>
/// <remarks>
///     This is the whole implementation of <see cref="UnknownNodeTypeHandling.Skip"/>: the snapshot is reduced to the
///     part the registry understands and then loaded through the normal, strict path, so skipping can never produce a
///     diagram that strict loading could not. Wires that touch a skipped node disappear, skipped nodes leave their groups,
///     and groups left without members disappear too (repeatedly, because a group can be the only member of another group).
///     Entries that are null (possible with untrusted JSON) are kept as they are so the strict path reports them.
/// </remarks>
internal static class SnapshotFilter
{
    /// <summary>
    ///     Removes nodes whose type is not in the registry and everything that depended on them.
    /// </summary>
    /// <param name="pages">The page snapshots.</param>
    /// <param name="types">The registry the loaded diagram will use.</param>
    /// <param name="missing">The distinct unknown type keys (already computed by the caller).</param>
    /// <param name="report">Receives what was removed.</param>
    /// <returns>The reduced pages.</returns>
    public static List<PageSnapshot> RemoveUnknownTypes(IReadOnlyList<PageSnapshot> pages, NodeTypeRegistry types, IReadOnlyList<string> missing, out LoadReport report)
    {
        var nodes = 0;
        var wires = 0;
        var groups = 0;
        var result = new List<PageSnapshot>(pages.Count);
        foreach (var page in pages)
        {
            if (page is null)
            {
                result.Add(page!);
                continue;
            }

            var reduced = Reduce(page, types, out var n, out var w, out var g);
            nodes += n;
            wires += w;
            groups += g;
            result.Add(reduced);
        }

        report = new LoadReport(missing, nodes, wires, groups);
        return result;
    }

    /// <summary>
    ///     Reduces one page.
    /// </summary>
    /// <param name="page">The page snapshot.</param>
    /// <param name="types">The registry.</param>
    /// <param name="nodes">Receives the number of nodes removed.</param>
    /// <param name="wires">Receives the number of wires removed.</param>
    /// <param name="groups">Receives the number of groups removed.</param>
    /// <returns>The reduced page; the same instance when nothing had to be removed.</returns>
    private static PageSnapshot Reduce(PageSnapshot page, NodeTypeRegistry types, out int nodes, out int wires, out int groups)
    {
        var allNodes = page.Nodes ?? [];
        var gone = new HashSet<string>(StringComparer.Ordinal);
        var keptNodes = new List<NodeSnapshot>();
        foreach (var node in allNodes)
        {
            if (node?.TypeKey is { } key && !types.Contains(key))
            {
                gone.Add(node.Id);
            }
            else
            {
                keptNodes.Add(node!);
            }
        }

        nodes = allNodes.Count - keptNodes.Count;
        wires = 0;
        groups = 0;
        if (nodes == 0)
        {
            return page;
        }

        // A wire cannot outlive either of its nodes
        var allWires = page.Wires ?? [];
        var keptWires = new List<WireSnapshot>();
        foreach (var wire in allWires)
        {
            if (wire is null || (!gone.Contains(wire.SourceNodeId) && !gone.Contains(wire.TargetNodeId)))
            {
                keptWires.Add(wire!);
            }
            else
            {
                gone.Add(wire.Id);
            }
        }

        wires = allWires.Count - keptWires.Count;

        // Members leave their groups; groups that lost all of their members leave too, which may empty their parents in turn
        var keptGroups = (page.Groups ?? []).ToList();
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var group in keptGroups.Where(g => g is not null).ToList())
            {
                var members = group.MemberIds ?? [];
                if (members.Count > 0 && members.All(gone.Contains))
                {
                    gone.Add(group.Id);
                    keptGroups.Remove(group);
                    groups++;
                    changed = true;
                }
            }
        }

        var reducedGroups = keptGroups
            .Select(g => g is null || (g.MemberIds ?? []).All(m => !gone.Contains(m)) ? g : g with { MemberIds = [.. g.MemberIds!.Where(m => !gone.Contains(m))] })
            .Cast<GroupSnapshot>().ToList();

        return page with
        {
            Nodes = keptNodes,
            Wires = keptWires,
            Groups = reducedGroups,
            ZOrder = page.ZOrder?.Where(id => !gone.Contains(id)).ToList(),
        };
    }
}

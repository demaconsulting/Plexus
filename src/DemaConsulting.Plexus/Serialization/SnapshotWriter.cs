namespace DemaConsulting.Plexus;

/// <summary>
///     Captures live elements as snapshot records; the single "read" half of the clone path used by
///     snapshots, the clipboard and duplicate.
/// </summary>
/// <remarks>
///     Capturing never mutates the model. Payloads are serialized with the node type's declared
///     mechanism; for in-process duplication, payloads of types without a serializer are carried by
///     reference instead (see <see cref="NodeSnapshot"/>).
/// </remarks>
internal static class SnapshotWriter
{
    /// <summary>
    ///     Captures a set of elements, which must be given bottom-most first.
    /// </summary>
    /// <remarks>
    ///     Groups must not reference members outside the set; callers expand selections first
    ///     (see <see cref="ClipboardSelection"/>).
    /// </remarks>
    /// <param name="elementsInZOrder">The elements, bottom-most first.</param>
    /// <param name="keepDataReferences">True to carry unserializable payloads by reference (in-process duplicate).</param>
    /// <returns>The captured lists plus the z-order.</returns>
    public static ElementContent Capture(IEnumerable<DiagramElement> elementsInZOrder, bool keepDataReferences)
    {
        var nodes = new List<NodeSnapshot>();
        var wires = new List<WireSnapshot>();
        var annotations = new List<AnnotationSnapshot>();
        var groups = new List<GroupSnapshot>();
        var zOrder = new List<string>();

        foreach (var element in elementsInZOrder)
        {
            zOrder.Add(element.Id);
            switch (element)
            {
                case Node node:
                    nodes.Add(CaptureNode(node, keepDataReferences));
                    break;
                case Wire wire:
                    wires.Add(CaptureWire(wire));
                    break;
                case Annotation annotation:
                    annotations.Add(CaptureAnnotation(annotation));
                    break;
                case Group group:
                    groups.Add(CaptureGroup(group));
                    break;
            }
        }

        return new ElementContent(nodes, wires, annotations, groups, zOrder);
    }

    /// <summary>
    ///     Captures a whole page.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <returns>The page snapshot.</returns>
    public static PageSnapshot CapturePage(DiagramPage page)
    {
        var content = Capture(page.Elements, keepDataReferences: false);
        return new PageSnapshot
        {
            Name = page.Name,
            Nodes = content.Nodes!,
            Wires = content.Wires!,
            Annotations = content.Annotations!,
            Groups = content.Groups!,
            ZOrder = content.ZOrder,
        };
    }

    /// <summary>
    ///     Captures one node.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="keepDataReference">True to carry an unserializable payload by reference.</param>
    /// <returns>The snapshot.</returns>
    private static NodeSnapshot CaptureNode(Node node, bool keepDataReference)
    {
        var json = NodeDataCodec.Serialize(node);
        return new NodeSnapshot
        {
            Id = node.Id,
            TypeKey = node.Type.Key,
            Name = node.Name,
            Bounds = RectSnapshot.From(node.Bounds),
            Properties = PropertyCodec.Write(node),
            Data = json,
            DataReference = json is null && keepDataReference && !NodeDataCodec.CanSerialize(node.Type) ? node.Data : null,
        };
    }

    /// <summary>
    ///     Captures one wire.
    /// </summary>
    /// <param name="wire">The wire.</param>
    /// <returns>The snapshot.</returns>
    private static WireSnapshot CaptureWire(Wire wire) => new()
    {
        Id = wire.Id,
        SourceNodeId = wire.Source.Node.Id,
        SourcePort = wire.Source.Name,
        TargetNodeId = wire.Target.Node.Id,
        TargetPort = wire.Target.Name,
        Route = [.. wire.Route.Select(PointSnapshot.From)],
    };

    /// <summary>
    ///     Captures one annotation.
    /// </summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns>The snapshot.</returns>
    private static AnnotationSnapshot CaptureAnnotation(Annotation annotation) => new()
    {
        Id = annotation.Id,
        Text = annotation.Text,
        Bounds = RectSnapshot.From(annotation.Bounds),
        Fill = annotation.Fill is { } fill ? PropertyCodec.FormatColor(fill) : null,
    };

    /// <summary>
    ///     Captures one group.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <returns>The snapshot.</returns>
    private static GroupSnapshot CaptureGroup(Group group) => new()
    {
        Id = group.Id,
        Title = group.Title,
        MemberIds = [.. group.Members.Select(m => m.Id)],
        IsCollapsed = group.IsCollapsed,
        CollapsedPosition = PointSnapshot.From(group.CollapsedPosition),
    };
}

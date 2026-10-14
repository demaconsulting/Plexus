using System.Text.Json.Nodes;

namespace DemaConsulting.Plexus.Demo.Gui.Automation;

/// <content>
///     Read-only commands: <c>ping</c>, <c>info</c>, <c>list-pages</c>, <c>list-nodes</c>, <c>list-wires</c>, <c>list-annotations</c>, <c>list-groups</c>.
/// </content>
internal sealed partial class AutomationDispatcher
{
    /// <summary>
    ///     Registers the read-only commands.
    /// </summary>
    private void RegisterQueryCommands()
    {
        Add("ping", _ => new JsonObject { ["pong"] = true });
        Add("info", _ => Info());
        Add("list-pages", _ => AutomationJson.ToArray(Session.Diagram.Pages.Select((p, i) => (Page: p, Index: i)), t => DescribePage(t.Page, t.Index)));
        Add("list-nodes", request => AutomationJson.ToArray(ResolvePage(request).Nodes, DescribeNode));
        Add("list-wires", request => AutomationJson.ToArray(ResolvePage(request).Wires, DescribeWire));
        Add("list-annotations", request => AutomationJson.ToArray(ResolvePage(request).Annotations, DescribeAnnotation));
        Add("list-groups", request => AutomationJson.ToArray(ResolvePage(request).Groups, DescribeGroup));
    }

    /// <summary>
    ///     Builds the <c>info</c> result: a snapshot of the application state.
    /// </summary>
    /// <returns>The result object.</returns>
    private JsonObject Info()
    {
        var diagram = Session.Diagram;
        var history = diagram.History;
        var editor = Session.Editor;
        var current = Session.CurrentPage;
        return new JsonObject
        {
            ["pid"] = Environment.ProcessId,
            ["title"] = Session.Title,
            ["file"] = Session.FilePath,
            ["dirty"] = Session.IsDirty,
            ["canUndo"] = history.CanUndo,
            ["canRedo"] = history.CanRedo,
            ["undoText"] = history.UndoDescription,
            ["redoText"] = history.RedoDescription,
            ["pages"] = AutomationJson.ToArray(diagram.Pages, p => p.Name),
            ["currentPage"] = current is null ? null : diagram.Pages.IndexOf(current),
            ["currentPageName"] = current?.Name,
            ["zoom"] = editor.Zoom,
            ["offsetX"] = editor.Offset.X,
            ["offsetY"] = editor.Offset.Y,
            ["selection"] = AutomationJson.ToArray(editor.Selection, e => e.Id),
            ["status"] = Session.Message,
            ["typeSet"] = Session.TypeSet.ToString().ToLowerInvariant(),
        };
    }

    /// <summary>
    ///     Describes a page.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <param name="index">The page's zero-based index.</param>
    /// <returns>The JSON description.</returns>
    private static JsonObject DescribePage(DiagramPage page, int index) => new()
    {
        ["index"] = index,
        ["name"] = page.Name,
        ["nodes"] = page.Nodes.Count,
        ["wires"] = page.Wires.Count,
        ["annotations"] = page.Annotations.Count,
        ["groups"] = page.Groups.Count,
    };

    /// <summary>
    ///     Describes a node.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>The JSON description.</returns>
    private JsonObject DescribeNode(Node node)
    {
        var properties = new JsonObject();
        foreach (var (name, value) in node.Properties)
        {
            properties[name] = AutomationJson.FromValue(value);
        }

        return new JsonObject
        {
            ["id"] = node.Id,
            ["name"] = node.Name,
            ["typeKey"] = node.Type.Key,
            ["bounds"] = AutomationJson.FromRect(node.Bounds),
            ["properties"] = properties,
            ["data"] = node.Data is { } data ? System.Text.Json.JsonSerializer.SerializeToNode(data, data.GetType()) : null,
            ["visible"] = node.IsVisible,
            ["page"] = Session.Diagram.Pages.IndexOf(node.Page!),
            ["ports"] = AutomationJson.ToArray(node.Ports, p => new JsonObject
            {
                ["name"] = p.Name,
                ["direction"] = p.Direction == PortDirection.Input ? "in" : "out",
                ["type"] = p.TypeKey,
            }),
        };
    }

    /// <summary>
    ///     Describes a wire.
    /// </summary>
    /// <param name="wire">The wire.</param>
    /// <returns>The JSON description.</returns>
    private static JsonObject DescribeWire(Wire wire) => new()
    {
        ["id"] = wire.Id,
        ["source"] = $"{wire.Source.Node.Name}.{wire.Source.Name}",
        ["target"] = $"{wire.Target.Node.Name}.{wire.Target.Name}",
        ["route"] = AutomationJson.ToArray(wire.Route, AutomationJson.FromPoint),
        ["visible"] = wire.IsVisible,
    };

    /// <summary>
    ///     Describes an annotation.
    /// </summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns>The JSON description.</returns>
    private static JsonObject DescribeAnnotation(Annotation annotation) => new()
    {
        ["id"] = annotation.Id,
        ["text"] = annotation.Text,
        ["bounds"] = AutomationJson.FromRect(annotation.Bounds),
        ["visible"] = annotation.IsVisible,
    };

    /// <summary>
    ///     Describes a group.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <returns>The JSON description.</returns>
    private static JsonObject DescribeGroup(Group group) => new()
    {
        ["id"] = group.Id,
        ["title"] = group.Title,
        ["collapsed"] = group.IsCollapsed,
        ["bounds"] = AutomationJson.FromRect(group.Bounds),
        ["members"] = AutomationJson.ToArray(group.Members, m => m.Id),
        ["visible"] = group.IsVisible,
    };
}

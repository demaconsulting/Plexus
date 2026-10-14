using System.Text.Json;
using System.Text.Json.Nodes;
using DemaConsulting.Plexus.Geometry;

namespace DemaConsulting.Plexus.Demo.Gui.Automation;

/// <content>
///     Editing commands: <c>add-node</c>, <c>move-node</c>, <c>set-property</c>, <c>toggle-switch</c>, <c>rename</c>, <c>connect</c>, <c>delete</c>,
///     <c>select</c>, <c>clear-selection</c>, <c>select-all</c>, <c>copy</c>, <c>cut</c>, <c>paste</c>, <c>duplicate</c>,
///     <c>undo</c>, <c>redo</c>, <c>group</c>, <c>ungroup</c>, <c>collapse</c>, <c>move-to-page</c>.
/// </content>
internal sealed partial class AutomationDispatcher
{
    /// <summary>
    ///     Registers the editing commands.
    /// </summary>
    private void RegisterEditCommands()
    {
        RegisterModelEdits();
        RegisterSelectionCommands();
        RegisterClipboardAndHistoryCommands();
        RegisterGroupCommands();
    }

    /// <summary>
    ///     Registers commands that change nodes and wires directly.
    /// </summary>
    private void RegisterModelEdits()
    {
        Add("add-node", request =>
        {
            var node = Session.AddNode(
                ResolveTypeKey(AutomationJson.RequireString(request, "type")),
                new Point(AutomationJson.RequireNumber(request, "x"), AutomationJson.RequireNumber(request, "y")),
                AutomationJson.OptionalString(request, "name"));
            return new JsonObject { ["id"] = node.Id, ["name"] = node.Name };
        });
        Add("move-node", request =>
        {
            var node = ResolveNode(request);
            var dx = AutomationJson.RequireNumber(request, "x") - node.Position.X;
            var dy = AutomationJson.RequireNumber(request, "y") - node.Position.Y;

            // DiagramPage.Move (not Node.MoveTo) so attached wires are re-routed like in a drag
            node.Page!.Move([node], dx, dy);
            return AutomationJson.FromRect(node.Bounds);
        });
        Add("set-property", request =>
        {
            var node = ResolveNode(request);
            var name = AutomationJson.RequireString(request, "name");
            node.Properties[name] = ConvertPropertyValue(node, name, request["value"]);
            return AutomationJson.FromValue(node.Properties[name]);
        });
        Add("toggle-switch", request =>
        {
            // The same command as double-clicking a switch: it flips On and so recolors the logic wires
            var node = ResolveNode(request);
            var on = DemoSession.ToggleSwitch(node);
            return new JsonObject { ["name"] = node.Name, ["on"] = on };
        });
        Add("rename", request =>
        {
            var node = ResolveNode(request);
            var name = AutomationJson.RequireString(request, "name");
            return Session.Diagram.TryRename(node, name, out var reason)
                ? new JsonObject { ["name"] = node.Name }
                : throw new InvalidOperationException(reason ?? "The name is not allowed.");
        });
        Add("connect", request =>
        {
            var from = ResolvePort(AutomationJson.RequireString(request, "from"), preferOutput: true, AutomationJson.OptionalString(request, "fromDirection"));
            var to = ResolvePort(AutomationJson.RequireString(request, "to"), preferOutput: false, AutomationJson.OptionalString(request, "toDirection"));
            if (!ReferenceEquals(from.Node.Page, to.Node.Page))
            {
                throw new InvalidOperationException("Both nodes must be on the same page.");
            }

            var wire = from.Node.Page!.Connect(from, to);
            return DescribeWire(wire);
        });
        Add("delete", request =>
        {
            // Delete takes the elements directly: the selection is neither needed nor touched
            var elements = ResolveElements(request);
            ShowPageOf(elements);
            Session.Editor.Delete(elements);
            return new JsonObject { ["deleted"] = elements.Count };
        });
    }

    /// <summary>
    ///     Registers the selection commands.
    /// </summary>
    private void RegisterSelectionCommands()
    {
        Add("select", request =>
        {
            var elements = ResolveElements(request);
            ShowPageOf(elements);
            Session.Editor.Select(elements, add: AutomationJson.OptionalBool(request, "add") ?? false);
            return SelectionResult();
        });
        Add("clear-selection", _ =>
        {
            Session.Editor.ClearSelection();
            return SelectionResult();
        });
        Add("select-all", _ =>
        {
            Session.Editor.SelectAll();
            return SelectionResult();
        });
    }

    /// <summary>
    ///     Registers the clipboard and undo/redo commands.
    /// </summary>
    private void RegisterClipboardAndHistoryCommands()
    {
        AddAsync("copy", async _ =>
        {
            var count = Session.Editor.Selection.Count;
            await Session.Editor.CopyAsync().ConfigureAwait(true);
            return new JsonObject { ["copied"] = count };
        });
        AddAsync("cut", async _ =>
        {
            var count = Session.Editor.Selection.Count;
            await Session.Editor.CutAsync().ConfigureAwait(true);
            return new JsonObject { ["cut"] = count };
        });
        AddAsync("paste", async request =>
        {
            var x = AutomationJson.OptionalNumber(request, "x");
            var y = AutomationJson.OptionalNumber(request, "y");
            if (x.HasValue != y.HasValue)
            {
                throw new ArgumentException("Give both 'x' and 'y', or neither.");
            }

            var result = x is { } px && y is { } py
                ? await Session.Editor.PasteAsync(new Point(px, py)).ConfigureAwait(true)
                : await Session.Editor.PasteAsync().ConfigureAwait(true);
            return DescribePaste(result);
        });
        Add("duplicate", _ =>
        {
            var copies = Session.Editor.DuplicateSelection();
            return new JsonObject { ["ids"] = AutomationJson.ToArray(copies, e => e.Id) };
        });
        Add("undo", _ => new JsonObject { ["performed"] = Session.Editor.Undo(), ["canUndo"] = Session.Diagram.History.CanUndo });
        Add("redo", _ => new JsonObject { ["performed"] = Session.Editor.Redo(), ["canRedo"] = Session.Diagram.History.CanRedo });
    }

    /// <summary>
    ///     Registers the group and page-move commands.
    /// </summary>
    private void RegisterGroupCommands()
    {
        Add("group", request =>
        {
            SelectRequested(request);
            var group = Session.Editor.GroupSelection(AutomationJson.OptionalString(request, "title") ?? "Group")
                ?? throw new InvalidOperationException("Select the nodes to group first.");
            return DescribeGroup(group);
        });
        Add("ungroup", request =>
        {
            var group = ResolveElement(AutomationJson.RequireString(request, "group")) as Group ?? throw new ArgumentException("The element is not a group.");
            ShowPageOf([group]);
            Session.Editor.Select(group);
            return new JsonObject { ["ungrouped"] = Session.Editor.UngroupSelection() };
        });
        Add("collapse", request =>
        {
            var group = ResolveElement(AutomationJson.RequireString(request, "group")) as Group ?? throw new ArgumentException("The element is not a group.");
            group.IsCollapsed = AutomationJson.OptionalBool(request, "collapsed") ?? true;
            return DescribeGroup(group);
        });
        Add("move-to-page", request =>
        {
            var elements = ResolveElements(request);
            var target = Session.FindPage(AutomationJson.RequireString(request, "page"));
            Session.Diagram.MoveToPage(elements, target);
            return new JsonObject { ["moved"] = elements.Count, ["page"] = target.Name };
        });
    }

    /// <summary>
    ///     Builds the result that lists the current selection.
    /// </summary>
    /// <returns>The result object.</returns>
    private JsonObject SelectionResult() => new()
    {
        ["selection"] = AutomationJson.ToArray(Session.Editor.Selection, e => new JsonObject { ["id"] = e.Id, ["kind"] = KindOf(e), ["name"] = (e as Node)?.Name }),
    };

    /// <summary>
    ///     Describes the outcome of a paste, which reports failures as data rather than as errors.
    /// </summary>
    /// <param name="result">The paste result, or null when nothing could be pasted into.</param>
    /// <returns>The result object.</returns>
    private static JsonObject DescribePaste(PasteResult? result) => result is null
        ? new JsonObject { ["success"] = false, ["error"] = "Nothing was pasted: the editor is read-only, shows no page or has no clipboard." }
        : new JsonObject
        {
            ["success"] = result.Success,
            ["ids"] = AutomationJson.ToArray(result.Elements, e => e.Id),
            ["missingNodeTypes"] = AutomationJson.ToArray(result.MissingNodeTypes, t => t),
            ["warnings"] = AutomationJson.ToArray(result.Warnings, w => w),
            ["error"] = result.Error,
        };

    /// <summary>
    ///     Converts a JSON value to the kind a node property holds.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="name">The property name.</param>
    /// <param name="value">The JSON value.</param>
    /// <returns>The value to store.</returns>
    /// <exception cref="ArgumentException">Thrown when the value does not fit the property.</exception>
    private static object ConvertPropertyValue(Node node, string name, JsonNode? value)
    {
        if (value is not JsonValue json)
        {
            throw new ArgumentException("Argument 'value' must be a string, number or boolean.");
        }

        // Declared properties are parsed against their definition, so "8" sets an int and "#FF0000" a color
        var kind = json.GetValueKind();
        if (node.Type.FindProperty(name) is { } definition)
        {
            var text = kind == JsonValueKind.String ? json.GetValue<string>() : json.ToJsonString();
            return definition.TryParse(text, out var parsed, out var error) && parsed is not null
                ? parsed
                : throw new ArgumentException($"Property '{name}': {error}");
        }

        // Ad-hoc properties take the natural JSON type
        return kind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => json.GetValue<string>(),
            JsonValueKind.Number when json.TryGetValue(out int i) => i,
            JsonValueKind.Number when json.TryGetValue(out long l) => l,
            JsonValueKind.Number => json.GetValue<double>(),
            _ => throw new ArgumentException("Argument 'value' must be a string, number or boolean."),
        };
    }
}

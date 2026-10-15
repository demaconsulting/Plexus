using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using DemaConsulting.Plexus.Scene;

namespace DemaConsulting.Plexus.Avalonia.Tests;

/// <summary>
///     Unit tests of the editor's hover tooltips: default content, the <see cref="DiagramEditor.ToolTipRequested"/> event and when tooltips are shown or hidden.
/// </summary>
public class DiagramEditorToolTipTests
{
    /// <summary>
    ///     Validates that a port's tooltip shows its label, its type's display name and its description.
    /// </summary>
    [Fact]
    public Task DiagramEditor_GetToolTipContent_Port_ShowsLabelTypeAndDescription() => RunWithBus(host =>
    {
        // Arrange
        var bus = host.Page.AddNode("test.Bus", 200, 200);
        var flag = host.Page.AddNode("test.Flag", 0, 0);

        // Act
        var described = host.Editor.GetToolTipContent(bus, bus.Input("in1"));
        var named = host.Editor.GetToolTipContent(flag, flag.Output("on"));

        // Assert: the type key without a display name, the registered display name otherwise
        Assert.Multiple(
            () => Assert.Equal("in1 (f32)\nChannel 1", described),
            () => Assert.Equal("on (Flag value)", named));
    });

    /// <summary>
    ///     Validates that a node's tooltip comes from its type's tooltip function, else its description.
    /// </summary>
    [Fact]
    public Task DiagramEditor_GetToolTipContent_Node_UsesTypeToolTipThenDescription() => RunWithBus(host =>
    {
        // Arrange
        var bus = host.Page.AddNode("test.Bus", 200, 200);
        var live = host.Page.AddNode("test.Live", 400, 200, "L");
        var plain = host.AddSource(0, 0);

        // Act / Assert
        Assert.Multiple(
            () => Assert.Equal("Merges channels.", host.Editor.GetToolTipContent(bus)),
            () => Assert.Equal("L is live", host.Editor.GetToolTipContent(live)),
            () => Assert.Null(host.Editor.GetToolTipContent(plain)));
    });

    /// <summary>
    ///     Validates that a wire's tooltip names its source and target ports.
    /// </summary>
    [Fact]
    public Task DiagramEditor_GetToolTipContent_Wire_ShowsSourceToTarget() => UiTest.Run(host =>
    {
        // Arrange
        var a = host.AddSource(0, 0, "A");
        var b = host.AddSink(300, 0, "B");
        var wire = host.Page.Connect(a.Port("value"), b.Port("value"));

        // Act
        var content = host.Editor.GetToolTipContent(wire);

        // Assert
        Assert.Equal("A.value → B.value", content);
    });

    /// <summary>
    ///     Validates that annotations and groups have no default tooltip.
    /// </summary>
    [Fact]
    public Task DiagramEditor_GetToolTipContent_Annotation_IsNull() => UiTest.Run(host =>
    {
        // Arrange
        var note = host.Page.AddAnnotation("note", new Geometry.Rect(0, 0, 100, 50));
        var group = host.Page.CreateGroup([host.AddSource(300, 300)], "G");

        // Act / Assert
        Assert.Multiple(
            () => Assert.Null(host.Editor.GetToolTipContent(note)),
            () => Assert.Null(host.Editor.GetToolTipContent(group)),
            () => Assert.Throws<ArgumentNullException>(() => host.Editor.GetToolTipContent(null!)));
    });

    /// <summary>
    ///     Validates that a node type's tooltip function returning an empty string or null means no tooltip, not the description.
    /// </summary>
    [Fact]
    public Task DiagramEditor_GetToolTipContent_NodeFunctionReturnsEmpty_IsNull() => RunWithBus(host =>
    {
        // Arrange: two types whose tooltip function returns "" and null although they have a description
        var empty = host.Page.AddNode("test.EmptyTip", 0, 0);
        var none = host.Page.AddNode("test.NullTip", 300, 0);

        // Act
        var emptyContent = host.Editor.GetToolTipContent(empty);
        var nullContent = host.Editor.GetToolTipContent(none);

        // Assert
        Assert.Multiple(
            () => Assert.Null(emptyContent),
            () => Assert.Null(nullContent));
    });

    /// <summary>
    ///     Validates that a group, which has no default tooltip, shows what a handler supplies.
    /// </summary>
    [Fact]
    public Task DiagramEditor_GetToolTipContent_GroupWithHandlerContent_ReturnsHandlerContent() => UiTest.Run(host =>
    {
        // Arrange: a handler that describes groups
        var group = host.Page.CreateGroup([host.AddSource(300, 300)], "G");
        host.Editor.ToolTipRequested += (_, e) =>
        {
            if (e.Element is Group g)
            {
                e.Content = $"Group {g.Title}";
            }
        };

        // Act
        var content = host.Editor.GetToolTipContent(group);

        // Assert
        Assert.Equal("Group G", content);
    });

    /// <summary>
    ///     Validates that a handler sees the default and can replace or suppress it.
    /// </summary>
    [Fact]
    public Task DiagramEditor_ToolTipRequested_Handler_OverridesAndSuppresses() => UiTest.Run(host =>
    {
        // Arrange: replace node text, suppress wires
        var a = host.AddSource(0, 0, "A");
        var b = host.AddSink(300, 0, "B");
        var wire = host.Page.Connect(a.Port("value"), b.Port("value"));
        DiagramToolTipRequestedEventArgs? seen = null;
        host.Editor.ToolTipRequested += (_, e) =>
        {
            seen ??= e;
            if (e.Element is Wire)
            {
                e.Cancel = true;
            }
            else
            {
                e.Content = $"{((Node)e.Element).Name}: {e.Content ?? "nothing"}";
            }
        };

        // Act
        var nodeText = host.Editor.GetToolTipContent(a, a.Port("value"));
        var wireText = host.Editor.GetToolTipContent(wire);

        // Assert
        Assert.Multiple(
            () => Assert.Equal("A: value (f32)", nodeText),
            () => Assert.Null(wireText),
            () => Assert.Same(a, seen!.Element),
            () => Assert.Same(a.Port("value"), seen!.Port));
    });

    /// <summary>
    ///     Validates that hovering a node sets the editor's tooltip to the node's text.
    /// </summary>
    [Fact]
    public Task DiagramEditor_Hover_OverNode_SetsTip() => RunWithBus(host =>
    {
        // Arrange
        var bus = host.Page.AddNode("test.Bus", 200, 200);
        host.Flush();

        // Act
        Hover(host, host.CenterOf(bus));

        // Assert
        Assert.Equal("Merges channels.", ToolTip.GetTip(host.Editor));
    });

    /// <summary>
    ///     Validates that hovering a port shows the port's text and hovering empty canvas clears the tooltip.
    /// </summary>
    [Fact]
    public Task DiagramEditor_Hover_OverCanvas_ClearsTip() => RunWithBus(host =>
    {
        // Arrange
        var bus = host.Page.AddNode("test.Bus", 200, 200);
        host.Flush();
        Hover(host, host.FindHit(HitKind.Port, bus));
        var overPort = ToolTip.GetTip(host.Editor);

        // Act
        Hover(host, new Point(700, 550));

        // Assert
        Assert.Multiple(
            () => Assert.StartsWith("in", overPort as string, StringComparison.Ordinal),
            () => Assert.Null(ToolTip.GetTip(host.Editor)));
    });

    /// <summary>
    ///     Validates that an open tooltip moving straight to another element shows the new element's content and stays open.
    /// </summary>
    [Fact]
    public Task DiagramEditor_ToolTip_OpenThenMoveToAnotherElement_ShowsNewContent() => RunWithBus(host =>
    {
        // Arrange: an open tooltip over a bus
        var bus = host.Page.AddNode("test.Bus", 100, 200);
        var live = host.Page.AddNode("test.Live", 400, 200, "L");
        host.Flush();
        Hover(host, host.CenterOf(bus));
        ToolTip.SetIsOpen(host.Editor, true);

        // Act
        Hover(host, host.CenterOf(live));

        // Assert
        Assert.Multiple(
            () => Assert.Equal("L is live", ToolTip.GetTip(host.Editor)),
            () => Assert.True(ToolTip.GetIsOpen(host.Editor)));
        ToolTip.SetIsOpen(host.Editor, false);
    });

    /// <summary>
    ///     Validates that the tooltip is cleared when the pointer leaves the editor.
    /// </summary>
    [Fact]
    public Task DiagramEditor_ToolTip_PointerLeaves_ClearsTip() => RunWithBus(host =>
    {
        // Arrange
        var bus = host.Page.AddNode("test.Bus", 200, 200);
        host.Flush();
        Hover(host, host.CenterOf(bus));
        var before = ToolTip.GetTip(host.Editor);

        // Act
        Hover(host, new Point(-50, -50));

        // Assert
        Assert.Multiple(
            () => Assert.NotNull(before),
            () => Assert.Null(ToolTip.GetTip(host.Editor)));
    });

    /// <summary>
    ///     Validates that pressing a button (a drag) and an inline rename clear the tooltip, and that it does not return for the pressed element.
    /// </summary>
    [Fact]
    public Task DiagramEditor_ToolTip_DragOrRename_ClearsTip() => RunWithBus(host =>
    {
        // Arrange
        var bus = host.Page.AddNode("test.Bus", 200, 200);
        host.Flush();
        Hover(host, host.CenterOf(bus));
        var before = ToolTip.GetTip(host.Editor);

        // Act: drag the node a little, then hover it again
        host.Drag(host.CenterOf(bus), host.CenterOf(bus) + new Point(30, 0));
        var afterDrag = ToolTip.GetTip(host.Editor);
        Hover(host, host.CenterOf(bus));
        var afterHover = ToolTip.GetTip(host.Editor);

        // Act: move away and back (the tooltip returns), then rename
        Hover(host, new Point(700, 550));
        Hover(host, host.CenterOf(bus));
        var back = ToolTip.GetTip(host.Editor);
        host.Editor.BeginRename(bus);
        var afterRename = ToolTip.GetTip(host.Editor);
        host.Editor.CancelRename();

        // Assert
        Assert.Multiple(
            () => Assert.NotNull(before),
            () => Assert.Null(afterDrag),
            () => Assert.Null(afterHover),
            () => Assert.NotNull(back),
            () => Assert.Null(afterRename));
    });

    /// <summary>
    ///     Validates that a right-click press clears the tooltip, which stays away over the clicked element and returns after the pointer moves away.
    /// </summary>
    [Fact]
    public Task DiagramEditor_ToolTip_RightClickPress_ClearsTipUntilPointerMovesAway() => RunWithBus(host =>
    {
        // Arrange: an open tooltip over a bus, and a counter of context requests
        var bus = host.Page.AddNode("test.Bus", 200, 200);
        host.Flush();
        var contextRequests = 0;
        host.Editor.DiagramContextRequested += (_, _) => contextRequests++;
        Hover(host, host.CenterOf(bus));
        var before = ToolTip.GetTip(host.Editor);

        // Act: right-click the bus, move a little over it, then away and back
        host.RightClick(host.CenterOf(bus));
        var afterPress = ToolTip.GetTip(host.Editor);
        Hover(host, host.CenterOf(bus) + new Point(2, 0));
        var sameElement = ToolTip.GetTip(host.Editor);
        Hover(host, new Point(700, 550));
        Hover(host, host.CenterOf(bus));
        var back = ToolTip.GetTip(host.Editor);

        // Assert
        Assert.Multiple(
            () => Assert.NotNull(before),
            () => Assert.Null(afterPress),
            () => Assert.Null(sameElement),
            () => Assert.Equal("Merges channels.", back),
            () => Assert.Equal(1, contextRequests));
    });

    /// <summary>
    ///     Validates that turning tooltips off never sets a tooltip and raises no request while hovering.
    /// </summary>
    [Fact]
    public Task DiagramEditor_ShowToolTips_False_NeverSetsTip() => RunWithBus(host =>
    {
        // Arrange
        var bus = host.Page.AddNode("test.Bus", 200, 200);
        host.Flush();
        var requests = 0;
        host.Editor.ToolTipRequested += (_, _) => requests++;
        Hover(host, host.CenterOf(bus));
        var shown = ToolTip.GetTip(host.Editor);

        // Act
        host.Editor.ShowToolTips = false;
        var afterOff = ToolTip.GetTip(host.Editor);
        Hover(host, new Point(700, 550));
        Hover(host, host.CenterOf(bus));
        var hoverRequests = requests;
        var queried = host.Editor.GetToolTipContent(bus);

        // Assert: one request while on, none by hovering after, and the content query ignores the setting
        Assert.Multiple(
            () => Assert.NotNull(shown),
            () => Assert.Null(afterOff),
            () => Assert.Null(ToolTip.GetTip(host.Editor)),
            () => Assert.Equal(1, hoverRequests),
            () => Assert.Equal("Merges channels.", queried),
            () => Assert.Equal(2, requests));
    });

    /// <summary>
    ///     Runs a test with an editor whose registry also has <c>test.Bus</c>, <c>test.Live</c> (tooltip "{name} is live") and
    ///     <c>test.Flag</c> (an output <c>on</c> of the port type <c>flag</c>, displayed as "Flag value").
    /// </summary>
    /// <param name="body">The test body.</param>
    /// <returns>The task to return from the test.</returns>
    internal static Task RunWithBus(Action<EditorHost> body) => UiTest.Run(() =>
    {
        using var host = new EditorHost(configureTypes: AddToolTipTypes);
        body(host);
    });

    /// <summary>
    ///     Adds the bus, a node type with a live tooltip, and a node type whose port type has a display name.
    /// </summary>
    /// <param name="types">The registry.</param>
    internal static void AddToolTipTypes(NodeTypeRegistry types)
    {
        EditorHost.AddBus(types);
        types.Add(new NodeType("test.Live") { Title = "LIVE", Description = "unused", ToolTip = node => $"{node.Name} is live" });
        types.PortTypes.Add(new PortType("flag", Geometry.Color.Parse("#2EBF5B")) { DisplayName = "Flag value" });
        types.Add(new NodeType("test.Flag") { Title = "FLAG", Outputs = { new PortDefinition("on", "flag") } });
        types.Add(new NodeType("test.EmptyTip") { Title = "EMPTY", Description = "Has a description", ToolTip = _ => string.Empty });
        types.Add(new NodeType("test.NullTip") { Title = "NULL", Description = "Has a description", ToolTip = _ => null });
    }

    /// <summary>
    ///     Moves the pointer (no button held) to a point and lets the editor react.
    /// </summary>
    /// <param name="host">The host.</param>
    /// <param name="at">The point relative to the editor.</param>
    private static void Hover(EditorHost host, Point at)
    {
        host.Window.MouseMove(at, RawInputModifiers.None);
        host.Flush();
    }
}

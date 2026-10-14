using DemaConsulting.Plexus.Scene;

namespace DemaConsulting.Plexus.Avalonia.Tests;

/// <summary>
///     Unit tests of how the editor follows per-node ports that a port provider recomputes.
/// </summary>
public class DiagramEditorDynamicPortsTests
{
    /// <summary>
    ///     Validates that a port added by a property change can be connected by dragging, like any port.
    /// </summary>
    [Fact]
    public Task DiagramEditor_Connect_DragToPortAddedByPropertyChange_CreatesWire() => UiTest.Run(() =>
    {
        // Arrange: a bus that gains a third input
        using var host = new EditorHost(configureTypes: EditorHost.AddBus);
        var source = host.AddSource(60, 300);
        var bus = host.Page.AddNode("test.Bus", 400, 60);
        bus.Properties["Channels"] = 3;
        host.Flush();

        // Act: drag from the source to the new port
        host.Drag(host.PortOf(source), host.Editor.GetPortScreenPosition(bus.Input("in2")));

        // Assert
        var wire = Assert.Single(host.Page.Wires);
        Assert.Same(bus.Input("in2"), wire.Target);
    });

    /// <summary>
    ///     Validates that hit testing no longer finds a port that a property change removed, and finds the new ones.
    /// </summary>
    [Fact]
    public Task DiagramEditor_HitTest_PortRemovedByPropertyChange_IsNotHit() => UiTest.Run(() =>
    {
        // Arrange
        using var host = new EditorHost(configureTypes: EditorHost.AddBus);
        var bus = host.Page.AddNode("test.Bus", 400, 60);
        host.Flush();
        var in1 = bus.Input("in1");
        var at = host.Editor.GetPortScreenPosition(in1);
        var before = host.Editor.HitTest(at);

        // Act
        bus.Properties["Channels"] = 1;
        host.Flush();
        var after = host.Editor.HitTest(at);

        // Assert: in1 was hit before and is never hit afterwards
        Assert.Multiple(
            () => Assert.Same(in1, before.Port),
            () => Assert.NotSame(in1, after.Port),
            () => Assert.Single(bus.Inputs),
            () => Assert.Equal(HitKind.Port, host.Editor.HitTest(host.Editor.GetPortScreenPosition(bus.Input("in0"))).Kind));
    });
}

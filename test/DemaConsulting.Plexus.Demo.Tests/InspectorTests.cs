using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using DemaConsulting.Plexus.Avalonia.PropertyGrid;
using DemaConsulting.Plexus.Avalonia.PropertyGrid.Diagrams;
using DemaConsulting.Plexus.Demo.Gui;
using DemaConsulting.Plexus.Demo.Gui.Inspectors;
using DemaConsulting.Plexus.Demo.Model;

namespace DemaConsulting.Plexus.Demo.Tests;

/// <summary>
///     Tests for the demo's custom node inspectors: the live previews of the audio nodes' properties dialogs, the plain dialog
///     of the other nodes, the oscillator's waveform tooltip and the theme-aware plot control.
/// </summary>
public class InspectorTests
{
    /// <summary>
    ///     Opens the properties dialog of a node of the audio sample through Edit &gt; Properties.
    /// </summary>
    /// <param name="host">The main window host.</param>
    /// <param name="name">The node name.</param>
    /// <returns>The node and its dialog.</returns>
    private static (Node Node, PropertyGridDialog Dialog) OpenProperties(MainWindowHost host, string name)
    {
        var node = host.Session.Diagram.FindNode(name) ?? throw new InvalidOperationException($"No node {name}.");
        host.Editor.Select(node);
        host.Choose("Menu_Edit_Properties");
        return (node, host.Window.OwnedWindows.OfType<PropertyGridDialog>().Single());
    }

    /// <summary>
    ///     Presses a button of a dialog.
    /// </summary>
    /// <param name="host">The main window host.</param>
    /// <param name="dialog">The dialog.</param>
    /// <param name="id">The AutomationId of the button.</param>
    private static void Press(MainWindowHost host, Window dialog, string id)
    {
        host.Get<Button>(id, dialog).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        host.Flush();
    }

    /// <summary>
    ///     Validates that the oscillator, envelope and filter dialogs show a live preview that follows staged edits before OK,
    ///     and that Cancel leaves the node unchanged.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task MainWindow_PropertiesDialog_AudioNodesShowALivePreview() => GuiTest.Run(() =>
    {
        // Arrange
        using var host = new MainWindowHost(MainWindowHost.Options("--sample", "audio"));
        var before = host.Session.Diagram.History.UndoCount;

        // Act: the oscillator, with a staged frequency, then Cancel
        var (osc, oscDialog) = OpenProperties(host, "Osc1");
        var oscPlot = host.Get<SignalPlot>(AutomationIds.InspectorPlot, oscDialog);
        var oscPanel = host.Get<Border>(AutomationIds.PropertiesPreview, oscDialog);
        var initialSpan = oscPlot.Data!.XAxis.Maximum;
        host.TypeIntoRow(oscDialog, NodePropertySource.PropertyPath(DemoNodeTypes.FrequencyProperty), "440");
        var stagedSpan = oscPlot.Data.XAxis.Maximum;
        host.TypeIntoRow(oscDialog, NodePropertySource.PropertyPath(DemoNodeTypes.DcOffsetProperty), "0.8");
        var stagedLimit = oscPlot.Data.YAxis.Maximum;
        var oscWidth = oscDialog.Width;
        Press(host, oscDialog, AutomationIds.PropertiesCancel);

        // Act: the filter, with a staged cutoff
        var (tone, toneDialog) = OpenProperties(host, "Tone");
        var tonePlot = host.Get<SignalPlot>(AutomationIds.InspectorPlot, toneDialog);
        host.TypeIntoRow(toneDialog, NodePropertySource.PropertyPath(DemoNodeTypes.CutoffProperty), "500");
        var marker = Assert.Single(tonePlot.Data!.Markers);
        Press(host, toneDialog, AutomationIds.PropertiesCancel);

        // Act: the envelope, with a staged attack
        var (shape, shapeDialog) = OpenProperties(host, "Shape");
        var shapePlot = host.Get<SignalPlot>(AutomationIds.InspectorPlot, shapeDialog);
        host.TypeIntoRow(shapeDialog, NodePropertySource.PropertyPath(DemoNodeTypes.AttackProperty), "50");
        var regions = shapePlot.Data!.Regions;
        Press(host, shapeDialog, AutomationIds.PropertiesCancel);

        // Assert
        Assert.Multiple(
            () => Assert.Same(oscPanel, host.Find(AutomationIds.PropertiesPreview, oscDialog)),
            () => Assert.Equal(880, oscWidth),
            () => Assert.Equal(2000.0 / 220.0, initialSpan, 9),
            () => Assert.Equal(2000.0 / 440.0, stagedSpan, 9),
            () => Assert.Equal(2.0, stagedLimit),
            () => Assert.Equal(new PlotMarker(500, "fc"), marker),
            () => Assert.Equal(["A", "D", "S", "R"], regions.Select(r => r.Label)),
            () => Assert.Equal(50.0, regions[0].End),
            () => Assert.Equal(220.0, osc.Properties[DemoNodeTypes.FrequencyProperty]),
            () => Assert.Equal(0.0, osc.Properties[DemoNodeTypes.DcOffsetProperty]),
            () => Assert.Equal(900.0, tone.Properties[DemoNodeTypes.CutoffProperty]),
            () => Assert.Equal(10, shape.Properties.Get<int>(DemoNodeTypes.AttackProperty)),
            () => Assert.Equal(before, host.Session.Diagram.History.UndoCount),
            () => Assert.Empty(host.Window.OwnedWindows.OfType<PropertyGridDialog>()));
    });

    /// <summary>
    ///     Validates that OK in an oscillator inspector applies the staged settings as one undo entry, and that a double-click
    ///     on the oscillator opens the inspector.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task MainWindow_OscillatorInspector_DoubleClickOpensItAndOkIsOneUndoEntry() => GuiTest.Run(() =>
    {
        // Arrange
        using var host = new MainWindowHost(MainWindowHost.Options("--sample", "audio"));
        var osc = host.Session.Diagram.FindNode("Osc1")!;
        var before = host.Session.Diagram.History.UndoCount;
        var point = host.Editor.TranslatePoint(host.Editor.DiagramToScreen(new Point(osc.Bounds.Center.X, osc.Bounds.Center.Y)), host.Window)!.Value;

        // Act
        host.DoubleClick(point);
        var dialog = host.Window.OwnedWindows.OfType<PropertyGridDialog>().Single();
        var hasPreview = host.Find(AutomationIds.PropertiesPreview, dialog) is not null;
        host.TypeIntoRow(dialog, NodePropertySource.PropertyPath(DemoNodeTypes.AmplitudeProperty), "0.5");
        host.TypeIntoRow(dialog, NodePropertySource.PropertyPath(DemoNodeTypes.PhaseProperty), "90 °");
        Press(host, dialog, AutomationIds.PropertiesOk);

        // Assert
        Assert.Multiple(
            () => Assert.True(hasPreview),
            () => Assert.Equal(0.5, osc.Properties[DemoNodeTypes.AmplitudeProperty]),
            () => Assert.Equal(90.0, osc.Properties[DemoNodeTypes.PhaseProperty]),
            () => Assert.Equal(before + 1, host.Session.Diagram.History.UndoCount),
            () => Assert.Equal("Edited Osc1", host.Session.Message));
    });

    /// <summary>
    ///     Validates that nodes other than the oscillator, envelope and filter get the plain dialog without a preview.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task MainWindow_PropertiesDialog_OtherNodesHaveNoPreview() => GuiTest.Run(() =>
    {
        // Arrange
        using var host = new MainWindowHost(MainWindowHost.Options("--sample", "audio"));

        // Act
        var (_, dialog) = OpenProperties(host, "Amp");
        var preview = host.Find(AutomationIds.PropertiesPreview, dialog);
        var width = dialog.Width;
        Press(host, dialog, AutomationIds.PropertiesCancel);

        // Assert
        Assert.Null(preview);
        Assert.Equal(480, width);
    });

    /// <summary>
    ///     Validates that an oscillator's tooltip is a waveform thumbnail of its current values with a short description,
    ///     while other nodes keep their text tooltips.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task MainWindow_OscillatorToolTip_ShowsWaveformThumbnailAndText() => GuiTest.Run(() =>
    {
        // Arrange
        using var host = new MainWindowHost(MainWindowHost.Options("--sample", "audio"));
        var osc = host.Session.Diagram.FindNode("Osc1")!;
        var amp = host.Session.Diagram.FindNode("Amp")!;

        // Act
        var content = Assert.IsAssignableFrom<Control>(host.Editor.GetToolTipContent(osc));
        var portTip = host.Editor.GetToolTipContent(osc, osc.Port("out"));
        var ampTip = host.Editor.GetToolTipContent(amp);
        var parts = content.GetSelfAndLogicalDescendants().ToList();
        var plot = parts.OfType<SignalPlot>().Single();

        // Assert
        Assert.Multiple(
            () => Assert.True(plot.IsCompact),
            () => Assert.Equal(2000.0 / 220.0, plot.Data!.XAxis.Maximum, 9),
            () => Assert.Contains("Saw, 220 Hz", parts.OfType<TextBlock>().Select(t => t.Text)),
            () => Assert.Contains("Osc1", parts.OfType<TextBlock>().Select(t => t.Text)),
            () => Assert.IsNotAssignableFrom<Control>(portTip),
            () => Assert.IsNotAssignableFrom<Control>(ampTip));
    });

    /// <summary>
    ///     Validates that the plot takes its colors from the theme variant it is shown in.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task SignalPlot_Theme_FollowsLightAndDark() => GuiTest.Run(() =>
    {
        // Arrange
        var light = new SignalPlot { Data = AudioPreview.Oscillator(OscillatorSettings.Default) };
        var dark = new SignalPlot { Data = AudioPreview.Oscillator(OscillatorSettings.Default) };
        var lightWindow = new Window { Width = 400, Height = 300, Content = light, RequestedThemeVariant = ThemeVariant.Light };
        var darkWindow = new Window { Width = 400, Height = 300, Content = dark, RequestedThemeVariant = ThemeVariant.Dark };
        lightWindow.Show();
        darkWindow.Show();

        // Act
        var lightText = Assert.IsAssignableFrom<ISolidColorBrush>(light.GetBrush(SignalPlot.TextKey)).Color;
        var darkText = Assert.IsAssignableFrom<ISolidColorBrush>(dark.GetBrush(SignalPlot.TextKey)).Color;
        darkWindow.RequestedThemeVariant = ThemeVariant.Light;
        var switched = Assert.IsAssignableFrom<ISolidColorBrush>(dark.GetBrush(SignalPlot.TextKey)).Color;

        // Assert
        Assert.NotEqual(lightText, darkText);
        Assert.Equal(lightText, switched);
        lightWindow.Close();
        darkWindow.Close();
    });

    /// <summary>
    ///     Validates that the plot renders with and without data, full size and as a thumbnail, and captures the oscillator
    ///     inspector to the temp folder (<c>plexus-pg-demo</c>) for review.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task SignalPlot_Render_DrawsWithAndWithoutData() => GuiTest.Run(() =>
    {
        // Arrange
        var panel = new StackPanel();
        panel.Children.Add(new SignalPlot { Height = 100 });
        panel.Children.Add(new SignalPlot { Height = 200, Data = AudioPreview.Filter(FilterSettings.Default) });
        panel.Children.Add(new SignalPlot { Height = 200, Data = AudioPreview.Envelope(EnvelopeSettings.Default) });
        panel.Children.Add(new SignalPlot { IsCompact = true, Width = 160, Height = 60, Data = AudioPreview.Oscillator(OscillatorSettings.Default) });
        var window = new Window { Width = 400, Height = 600, Content = panel };
        window.Show();
        using var host = new MainWindowHost(MainWindowHost.Options("--sample", "audio"));
        var (_, dialog) = OpenProperties(host, "Osc1");

        // Act
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using var frame = window.CaptureRenderedFrame();
        dialog.UpdateLayout();
        host.Flush();
        using var inspector = dialog.CaptureRenderedFrame();
        var folder = Path.Combine(Path.GetTempPath(), "plexus-pg-demo");
        Directory.CreateDirectory(folder);
        inspector?.Save(Path.Combine(folder, "oscillator-inspector.png"), global::Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);

        // Assert
        Assert.NotNull(frame);
        Assert.NotNull(inspector);
        Assert.Equal("Preview", AutomationProperties.GetName(host.Get<Border>(AutomationIds.PropertiesPreview, dialog)));
        Press(host, dialog, AutomationIds.PropertiesCancel);
        window.Close();
    });
}

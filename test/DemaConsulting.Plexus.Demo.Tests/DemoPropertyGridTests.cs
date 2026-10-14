using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using DemaConsulting.Plexus.Avalonia.PropertyGrid;
using DemaConsulting.Plexus.Demo.Gui;
using DemaConsulting.Plexus.Demo.Model;

namespace DemaConsulting.Plexus.Demo.Tests;

/// <summary>
///     Tests for the demo's custom slider editor and headless screenshots of the property grid in the demo (written to the temp folder for review).
/// </summary>
public class DemoPropertyGridTests
{
    /// <summary>
    ///     Captures a window as a PNG under the temp folder (<c>plexus-pg-demo</c>).
    /// </summary>
    /// <param name="host">The main window host, used to pump the UI.</param>
    /// <param name="window">The window to capture.</param>
    /// <param name="name">The file name without extension.</param>
    /// <returns>The path of the PNG.</returns>
    private static string Capture(MainWindowHost host, Window window, string name)
    {
        host.Flush();
        window.UpdateLayout();
        host.Flush();
        var folder = Path.Combine(Path.GetTempPath(), "plexus-pg-demo");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, name + ".png");
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No frame was rendered.");
        frame.Save(path, global::Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
        return path;
    }

    /// <summary>
    ///     Validates that the slider editor edits a node property that carries the editor hint (a percentage) and the Task host data
    ///     (an annotated property) live in the panel, and that its range is 0 to 100.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task DemoEditors_Slider_EditsNodePropertyAndHostDataThroughThePanel() => GuiTest.Run(() =>
    {
        // Arrange
        using var host = new MainWindowHost(MainWindowHost.Options("--sample", "audio"));
        host.Get<MenuItem>("Menu_View_PropertiesPanel").IsChecked = true;
        var speaker = host.Session.RequirePage().Nodes.First(n => n.Type.Key == DemoNodeTypes.Speaker);
        host.Editor.Select(speaker);
        host.Flush();
        var before = host.Session.Diagram.History.UndoCount;
        var slider = host.Get<Control>(AutomationIds.PropertiesEditor("Volume")).GetSelfAndVisualDescendants().OfType<Slider>().Single();

        // Act
        slider.Value = 30;
        host.Flush();

        // Assert
        Assert.Equal((0.0, 100.0), (slider.Minimum, slider.Maximum));
        Assert.Equal(30, speaker.Properties.Get<int>("Volume"));
        Assert.Equal(before + 1, host.Session.Diagram.History.UndoCount);
        host.Editor.Undo();
        host.Flush();
        Assert.Equal(50, host.Get<Control>(AutomationIds.PropertiesEditor("Volume")).GetSelfAndVisualDescendants().OfType<Slider>().Single().Value);
    });

    /// <summary>
    ///     Validates that the slider of a Task's Progress edits the host data, as one undo entry that leaves the old data untouched.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task DemoEditors_Slider_EditsTheTaskProgressHostData() => GuiTest.Run(() =>
    {
        // Arrange
        using var host = new MainWindowHost(MainWindowHost.Options("--sample", "workflow"));
        var task = host.Session.RequirePage().Nodes.First(n => n.Type.Key == DemoNodeTypes.Task);
        var original = (TaskDetails)task.Data!;
        host.Get<MenuItem>("Menu_View_PropertiesPanel").IsChecked = true;
        host.Editor.Select(task);
        host.Flush();
        var before = host.Session.Diagram.History.UndoCount;

        // Act
        host.Get<Control>(AutomationIds.PropertiesEditor("Progress")).GetSelfAndVisualDescendants().OfType<Slider>().Single().Value = 80;
        host.Flush();

        // Assert
        Assert.Equal(80, ((TaskDetails)task.Data!).Progress);
        Assert.Equal(25, original.Progress);
        Assert.Equal(before + 1, host.Session.Diagram.History.UndoCount);
    });

    /// <summary>
    ///     Captures the properties dialog of a Task node, its collection editor and the main window with the docked panel; the
    ///     screenshots are for review (the test checks that the files are written).
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task DemoPropertyGrid_Screenshots_DialogCollectionEditorAndPanel() => GuiTest.Run(() =>
    {
        // Arrange
        using var host = new MainWindowHost(MainWindowHost.Options("--sample", "workflow"));
        var task = host.Session.RequirePage().Nodes.First(n => n.Type.Key == DemoNodeTypes.Task);
        host.Editor.Select(task);
        host.Choose("Menu_Edit_Properties");
        var dialog = host.Window.OwnedWindows.OfType<PropertyGridDialog>().Single();
        dialog.Height = 920;
        dialog.Grid.SetExpanded("Contact", true);

        // Act
        var dialogShot = Capture(host, dialog, "task-dialog");
        _ = dialog.Grid.EditCollectionAsync(dialog.Grid.Session!.FindItem("Checklist")!);
        host.Flush();
        var collection = dialog.OwnedWindows.OfType<CollectionEditorDialog>().Single();
        var collectionShot = Capture(host, collection, "checklist-collection-editor");
        collection.Close(false);
        host.Flush();
        dialog.Close(false);
        host.Flush();
        host.Get<MenuItem>("Menu_View_PropertiesPanel").IsChecked = true;
        host.Editor.Select(task);
        host.Flush();
        var panelShot = Capture(host, host.Window, "main-window-with-panel");

        // Assert
        Assert.True(File.Exists(dialogShot));
        Assert.True(File.Exists(collectionShot));
        Assert.True(File.Exists(panelShot));
    });
}

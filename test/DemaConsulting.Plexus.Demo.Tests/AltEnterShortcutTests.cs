using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using DemaConsulting.Plexus.Avalonia.PropertyGrid;
using DemaConsulting.Plexus.Demo.Gui;

namespace DemaConsulting.Plexus.Demo.Tests;

/// <summary>
///     Tests for the Alt+Enter shortcut of Edit &gt; Properties: the dialog opens only once Alt has been released, so
///     the window's access key handling sees the whole chord and the first key press after the dialog reaches the editor.
/// </summary>
public class AltEnterShortcutTests
{
    /// <summary>
    ///     Presses a key in the main window as the keyboard would.
    /// </summary>
    /// <param name="host">The window host.</param>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifier state while the key is down.</param>
    private static void Press(MainWindowHost host, Key key, RawInputModifiers modifiers)
    {
        host.Window.KeyPress(key, modifiers, PhysicalKey.None, null);
        host.Flush();
    }

    /// <summary>
    ///     Releases a key in the main window.
    /// </summary>
    /// <param name="host">The window host.</param>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifier state after the key is up.</param>
    private static void Release(MainWindowHost host, Key key, RawInputModifiers modifiers)
    {
        host.Window.KeyRelease(key, modifiers, PhysicalKey.None, null);
        host.Flush();
    }

    /// <summary>
    ///     Gets the open properties dialogs of the window.
    /// </summary>
    /// <param name="host">The window host.</param>
    /// <returns>The dialogs.</returns>
    private static List<PropertyGridDialog> Dialogs(MainWindowHost host) =>
        [.. host.Window.OwnedWindows.OfType<PropertyGridDialog>()];

    /// <summary>
    ///     Validates that Alt+Enter opens the dialog only after Alt is released, and that after OK the first Ctrl+Z reaches the editor.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task AltEnter_OpensDialogAfterAltRelease_ThenCtrlZReachesEditor() => GuiTest.Run(() =>
    {
        // Arrange
        using var host = new MainWindowHost(MainWindowHost.Options("--sample", "math"));
        var node = host.Session.RequirePage().Nodes[0];
        var originalName = node.Name;
        host.Editor.Select(node);
        host.Editor.Focus();
        host.Flush();

        // Act: Alt down, Enter down and up while Alt is still held (the dialog must not be open yet)
        Press(host, Key.LeftAlt, RawInputModifiers.Alt);
        Press(host, Key.Enter, RawInputModifiers.Alt);
        Release(host, Key.Enter, RawInputModifiers.Alt);
        var openWhileAltHeld = Dialogs(host).Count;

        // Act: Alt up (the dialog opens now)
        Release(host, Key.LeftAlt, RawInputModifiers.None);
        var dialogs = Dialogs(host);
        var dialog = dialogs.Single();
        host.TypeIntoRow(dialog, "Name", "Renamed");
        host.Get<Button>(AutomationIds.PropertiesOk, dialog).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        host.Flush();
        var renamed = node.Name;

        // Act: the first key press after the dialog is Ctrl+Z
        host.Editor.Focus();
        host.Flush();
        Press(host, Key.LeftCtrl, RawInputModifiers.Control);
        Press(host, Key.Z, RawInputModifiers.Control);
        Release(host, Key.Z, RawInputModifiers.Control);
        Release(host, Key.LeftCtrl, RawInputModifiers.None);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(0, openWhileAltHeld),
            () => Assert.Equal("Renamed", renamed),
            () => Assert.Equal(originalName, node.Name),
            () => Assert.Empty(Dialogs(host)));
    });

    /// <summary>
    ///     Validates that releasing Alt before Enter still opens the dialog once, and a repeating Enter opens one dialog.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task AltEnter_RepeatedOrAltReleasedFirst_OpensOneDialog() => GuiTest.Run(() =>
    {
        // Arrange
        using var host = new MainWindowHost(MainWindowHost.Options("--sample", "math"));
        host.Editor.Select(host.Session.RequirePage().Nodes[0]);
        host.Editor.Focus();
        host.Flush();

        // Act: Enter repeats while Alt is held, then Alt goes up before Enter does
        Press(host, Key.LeftAlt, RawInputModifiers.Alt);
        Press(host, Key.Enter, RawInputModifiers.Alt);
        Press(host, Key.Enter, RawInputModifiers.Alt);
        Press(host, Key.Enter, RawInputModifiers.Alt);
        var openWhileAltHeld = Dialogs(host).Count;
        Release(host, Key.LeftAlt, RawInputModifiers.None);
        var opened = Dialogs(host).Count;
        Release(host, Key.Enter, RawInputModifiers.None);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(0, openWhileAltHeld),
            () => Assert.Equal(1, opened),
            () => Assert.Single(Dialogs(host)));
    });

    /// <summary>
    ///     Validates that Alt+Enter does nothing while Edit &gt; Properties is disabled (no single node selected).
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task AltEnter_WithoutSingleNodeSelected_OpensNothing() => GuiTest.Run(() =>
    {
        // Arrange
        using var host = new MainWindowHost(MainWindowHost.Options("--sample", "math"));
        host.Editor.SelectAll();
        host.Editor.Focus();
        host.Flush();

        // Act
        Press(host, Key.LeftAlt, RawInputModifiers.Alt);
        Press(host, Key.Enter, RawInputModifiers.Alt);
        Release(host, Key.Enter, RawInputModifiers.Alt);
        Release(host, Key.LeftAlt, RawInputModifiers.None);

        // Assert
        Assert.Empty(Dialogs(host));
    });
}

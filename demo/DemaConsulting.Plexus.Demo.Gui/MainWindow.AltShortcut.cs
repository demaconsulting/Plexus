using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace DemaConsulting.Plexus.Demo.Gui;

/// <content>
///     The Alt+Enter shortcut of Edit &gt; Properties: it opens its dialog only once Alt has been released.
/// </content>
/// <remarks>
///     <para>
///         Avalonia's access key handler of a window remembers that Alt went down and decides on the Alt key-up
///         whether the press was a lone tap (which opens the menu bar) or part of a chord. It learns that it was a
///         chord from the next key-down, but only from one that no other handler has handled yet. Two things went wrong
///         when Alt+Enter was an ordinary menu item hot key: the hot key handled the Enter key-down before the access
///         key handler saw it, so releasing Alt opened the menu bar; and the dialog it opened took the window's keyboard
///         input, so the Alt key-up never reached the window and its access key state (the underlined menu captions)
///         stayed on until a later stray Alt key-up opened the menu bar and the next key press (Ctrl+Z, Escape) went
///         to the menu instead of the editor.
///     </para>
///     <para>
///         So the window handles the shortcut itself, in the bubble phase, after the access key handler has seen the
///         Enter key-down (it then ignores the Alt release), and <see cref="RunAfterAltRelease"/> postpones the dialog
///         until the Alt key-up has been routed through the window like any other key.
///     </para>
/// </remarks>
internal sealed partial class MainWindow
{
    /// <summary>
    ///     The shortcut of Edit &gt; Properties.
    /// </summary>
    private static readonly KeyGesture PropertiesGesture = new(Key.Enter, KeyModifiers.Alt);

    /// <summary>
    ///     Whether the Alt key is down in this window.
    /// </summary>
    private bool _altIsDown;

    /// <summary>
    ///     The command waiting for Alt to be released, or null.
    /// </summary>
    private Action? _afterAltRelease;

    /// <summary>
    ///     Starts tracking the Alt key and handling the Alt+Enter shortcut; called once from the constructor.
    /// </summary>
    private void WireAltShortcut()
    {
        // handledEventsToo: another handler may handle a key, and the tracking must still see every Alt change
        AddHandler(KeyDownEvent, OnAltTrackKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(KeyUpEvent, OnAltTrackKeyUp, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(KeyDownEvent, OnShortcutKeyDown, RoutingStrategies.Bubble);
        Deactivated += (_, _) =>
        {
            // The release of Alt can no longer be seen once another window has the keyboard, so nothing waits for it
            _altIsDown = false;
            _afterAltRelease = null;
        };
    }

    /// <summary>
    ///     Runs a command now, or as soon as the Alt key has been released when it is down.
    /// </summary>
    /// <remarks>
    ///     A later request replaces an earlier one that is still waiting, so a key that repeats while Alt is held
    ///     starts the command once.
    /// </remarks>
    /// <param name="command">The command.</param>
    private void RunAfterAltRelease(Action command)
    {
        if (_altIsDown)
        {
            _afterAltRelease = command;
        }
        else
        {
            command();
        }
    }

    /// <summary>
    ///     Handles Alt+Enter: opens the properties of the selected node when Edit &gt; Properties is enabled.
    /// </summary>
    /// <param name="sender">The window.</param>
    /// <param name="e">The key.</param>
    private void OnShortcutKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || !PropertiesGesture.Matches(e) || !_properties.IsEnabled)
        {
            return;
        }

        e.Handled = true;
        RunAfterAltRelease(() =>
        {
            HideAccessKeys();
            Fire(ShowSelectedNodePropertiesAsync);
        });
    }

    /// <summary>
    ///     Removes the access key underlines Alt switched on and gives the keyboard focus back to the editor.
    /// </summary>
    /// <remarks>
    ///     After a chord Avalonia leaves the menu captions underlined until the next mouse click. Opening and closing
    ///     the menu bar is the public way to make the access key handler hide them again.
    /// </remarks>
    private void HideAccessKeys()
    {
        _menuBar.Open();
        _menuBar.Close();
        Editor.Focus();
    }

    /// <summary>
    ///     Notes that Alt went down.
    /// </summary>
    /// <param name="sender">The window.</param>
    /// <param name="e">The key.</param>
    private void OnAltTrackKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.LeftAlt or Key.RightAlt)
        {
            _altIsDown = true;
        }
    }

    /// <summary>
    ///     Notes that Alt went up (or is no longer part of the modifiers) and starts the command that waited for it.
    /// </summary>
    /// <param name="sender">The window.</param>
    /// <param name="e">The key.</param>
    private void OnAltTrackKeyUp(object? sender, KeyEventArgs e)
    {
        if (!_altIsDown || (e.Key is not (Key.LeftAlt or Key.RightAlt) && e.KeyModifiers.HasFlag(KeyModifiers.Alt)))
        {
            return;
        }

        _altIsDown = false;
        if (_afterAltRelease is not { } command)
        {
            return;
        }

        _afterAltRelease = null;

        // Posted, not called: the key-up must finish its route first, so the access key handler of the window has
        // certainly seen the Alt release before the dialog takes the keyboard
        Dispatcher.UIThread.Post(command, DispatcherPriority.Input);
    }
}

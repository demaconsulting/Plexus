using Avalonia;
using Avalonia.Controls;

namespace DemaConsulting.Plexus.Demo.Gui;

/// <summary>
///     Where a window should sit so that it is fully on the screen: the position to move it to and, only when it is
///     genuinely too big for the screen, the client size to shrink it to.
/// </summary>
/// <param name="Position">The position of the window's top-left corner, in physical pixels.</param>
/// <param name="Width">The new client width in device-independent units, or null to keep the current width.</param>
/// <param name="Height">The new client height in device-independent units, or null to keep the current height.</param>
internal readonly record struct WindowPlacementResult(PixelPoint Position, double? Width, double? Height);

/// <summary>
///     Keeps windows on the screen: a window that opens, or ends up, partly outside the working area is moved back in.
/// </summary>
/// <remarks>
///     <para>
///     Moving is tried before resizing, so a window that merely drifted off-screen is put back at exactly the size it
///     had. Only a window still overflowing once it has been pushed against the top-left of the working area is
///     genuinely too big, and only that window is shrunk - never below its minimum size; when even the minimum does not
///     fit, it stays pinned to the top-left so its title bar remains reachable. A maximized or minimized window is left
///     alone.
///     </para>
///     <para>
///     The arithmetic lives in <see cref="Calculate"/>, which takes plain values so that it can be tested without a
///     window; <see cref="EnsureFullyVisible"/> gathers the values from a window and applies the result.
///     </para>
///     <para>
///     Adapted from another DEMA Consulting application, where a 1400x900 main window ran under the taskbar and off
///     the right edge of a laptop screen.
///     </para>
/// </remarks>
internal static class WindowPlacement
{
    /// <summary>
    ///     Makes a window keep itself on the screen: it is placed when it opens and again whenever the screens or their
    ///     scaling change.
    /// </summary>
    /// <param name="window">The window; call this before it is shown.</param>
    public static void KeepOnScreen(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        // Placement needs the real frame size, which exists only once the window has been opened
        window.Opened += (_, _) =>
        {
            EnsureFullyVisible(window);

            // A monitor being plugged in, removed or rescaled changes the working area under an open window
            window.ScalingChanged += (_, _) => EnsureFullyVisible(window);
            if (window.Screens is { } screens)
            {
                screens.Changed += (_, _) => EnsureFullyVisible(window);
            }
        };
    }

    /// <summary>
    ///     Brings a window fully onto its screen when it is windowed, and leaves it alone entirely when it is maximized
    ///     or minimized.
    /// </summary>
    /// <param name="window">The window.</param>
    public static void EnsureFullyVisible(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (window.WindowState != WindowState.Normal)
        {
            return;
        }

        var screen = window.Screens.ScreenFromWindow(window) ?? window.Screens.Primary;
        if (screen is null)
        {
            return;
        }

        // Position is the top-left of the frame, so the on-screen extent is measured with FrameSize (title bar and
        // borders included); Bounds alone under-reports the height and leaves the bottom strip below the screen.
        var frame = window.FrameSize ?? window.Bounds.Size;
        var result = Calculate(
            screen.WorkingArea,
            screen.Scaling,
            frame,
            window.Bounds.Size,
            new Size(window.MinWidth, window.MinHeight),
            window.Position);

        if (result.Width is { } width)
        {
            window.Width = width;
        }

        if (result.Height is { } height)
        {
            window.Height = height;
        }

        if (result.Position != window.Position)
        {
            window.Position = result.Position;
        }
    }

    /// <summary>
    ///     Works out where a window has to go to be fully inside a working area.
    /// </summary>
    /// <param name="workArea">The screen's working area (the screen minus the taskbar), in physical pixels.</param>
    /// <param name="scaling">The screen's scaling factor (physical pixels per device-independent unit); zero or less means 1.</param>
    /// <param name="frame">The size of the whole window frame including title bar and borders, in device-independent units.</param>
    /// <param name="client">The size of the window's client area, in device-independent units.</param>
    /// <param name="minimum">The smallest client size the window may be given, in device-independent units.</param>
    /// <param name="position">The current position of the frame's top-left corner, in physical pixels.</param>
    /// <returns>The position to use and, when moving was not enough, the client size to shrink to.</returns>
    /// <remarks>
    ///     The working area and the position are physical pixels; the sizes are device-independent units, so the frame
    ///     is converted with <paramref name="scaling"/> before it is compared with the working area. The far edges are
    ///     corrected before the near ones, so a window too large to fit ends up pinned to the top-left rather than to
    ///     the bottom-right.
    /// </remarks>
    public static WindowPlacementResult Calculate(PixelRect workArea, double scaling, Size frame, Size client, Size minimum, PixelPoint position)
    {
        var scale = scaling <= 0 ? 1.0 : scaling;

        // Width and Height set the client area, so the chrome (frame minus client) comes off the working area
        var chromeWidth = Math.Max(0, frame.Width - client.Width);
        var chromeHeight = Math.Max(0, frame.Height - client.Height);
        var windowWidth = (int)Math.Round(frame.Width * scale);
        var windowHeight = (int)Math.Round(frame.Height * scale);

        var right = workArea.X + workArea.Width;
        var bottom = workArea.Y + workArea.Height;

        // Move first
        var newX = Math.Max(workArea.X, Math.Min(position.X, right - windowWidth));
        var newY = Math.Max(workArea.Y, Math.Min(position.Y, bottom - windowHeight));

        // Resize only if moving was not enough, and never below the minimum
        double? newWidth = null;
        double? newHeight = null;
        if (newX + windowWidth > right)
        {
            newWidth = Math.Max(minimum.Width, (workArea.Width / scale) - chromeWidth);
        }

        if (newY + windowHeight > bottom)
        {
            newHeight = Math.Max(minimum.Height, (workArea.Height / scale) - chromeHeight);
        }

        return new WindowPlacementResult(new PixelPoint(newX, newY), newWidth, newHeight);
    }
}

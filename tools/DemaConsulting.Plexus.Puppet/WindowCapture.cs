using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using FlaUI.Core.AutomationElements;

namespace DemaConsulting.Plexus.Puppet;

/// <summary>
///     Renders windows to bitmaps with <c>PrintWindow</c>, so a shot shows the demo even when another window covers it.
/// </summary>
internal static partial class WindowCapture
{
    /// <summary>
    ///     Asks the window to render its full content, including what DirectComposition draws (Windows 8.1+).
    /// </summary>
    private const uint RenderFullContent = 2;

    /// <summary>
    ///     Renders the main window with any open dialogs drawn over it at their places, on a canvas covering them all.
    /// </summary>
    /// <param name="mainWindow">The main window.</param>
    /// <param name="dialogs">The open dialogs (a dialog can stick out past the main window's edge).</param>
    /// <returns>The bitmap; the caller disposes it.</returns>
    internal static Bitmap Render(AutomationElement mainWindow, IReadOnlyList<AutomationElement> dialogs)
    {
        var windows = dialogs.Prepend(mainWindow).Select(w => (Window: w, Bounds: FrameOf(w))).ToList();
        var area = windows.Skip(1).Aggregate(windows[0].Bounds, (a, w) => Rectangle.Union(a, w.Bounds));
        var canvas = new Bitmap(Math.Max(1, area.Width), Math.Max(1, area.Height), PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(canvas);
        graphics.Clear(Color.Black);
        foreach (var (window, bounds) in windows)
        {
            using var image = RenderOne(window, bounds);
            graphics.DrawImage(image, bounds.X - area.X, bounds.Y - area.Y);
        }

        return canvas;
    }

    /// <summary>
    ///     Copies the pixels of the main window's visible frame (title bar included) (and any pop-ups and dialogs inside the union) from the
    ///     screen, so what only the compositor draws (a drag ghost, a context menu) is in the picture.
    /// </summary>
    /// <param name="mainWindow">The main window.</param>
    /// <param name="others">The open dialogs and pop-ups.</param>
    /// <returns>The bitmap; the caller disposes it.</returns>
    /// <remarks>The window must be on screen and not covered: unlike <see cref="Render"/> this is a real screen copy.</remarks>
    internal static Bitmap CaptureScreen(AutomationElement mainWindow, IReadOnlyList<AutomationElement> others)
    {
        var handle = mainWindow.Properties.NativeWindowHandle.ValueOrDefault;
        var frame = (handle != IntPtr.Zero ? WindowPlacement.GetVisibleFrame(handle) : null) ?? mainWindow.BoundingRectangle;
        var area = others.Aggregate(frame, (a, w) => Rectangle.Union(a, w.BoundingRectangle));
        var canvas = new Bitmap(Math.Max(1, area.Width), Math.Max(1, area.Height), PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(canvas);
        graphics.CopyFromScreen(area.Location, Point.Empty, area.Size);
        return canvas;
    }

    /// <summary>
    ///     Gets the window's whole frame (title bar and borders included), which <c>PrintWindow</c> draws.
    /// </summary>
    /// <param name="window">The window.</param>
    /// <returns>The frame in screen pixels.</returns>
    private static Rectangle FrameOf(AutomationElement window)
    {
        var handle = window.Properties.NativeWindowHandle.ValueOrDefault;
        return handle != IntPtr.Zero && GetWindowRect(handle, out var r)
            ? Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom)
            : window.BoundingRectangle;
    }

    /// <summary>
    ///     Renders one window into a bitmap.
    /// </summary>
    /// <param name="window">The window.</param>
    /// <param name="bounds">Its frame.</param>
    /// <returns>The bitmap.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <c>PrintWindow</c> fails.</exception>
    private static Bitmap RenderOne(AutomationElement window, Rectangle bounds)
    {
        var handle = window.Properties.NativeWindowHandle.ValueOrDefault;
        var image = new Bitmap(Math.Max(1, bounds.Width), Math.Max(1, bounds.Height), PixelFormat.Format32bppArgb);
        try
        {
            using var graphics = Graphics.FromImage(image);

            // A pop-up without a window handle of its own (a tooltip) cannot be printed: copy its pixels from the screen
            if (handle == IntPtr.Zero)
            {
                graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
                return image;
            }

            var dc = graphics.GetHdc();
            try
            {
                if (!PrintWindow(handle, dc, RenderFullContent))
                {
                    throw new InvalidOperationException($"the window \"{window.Name}\" could not be rendered (PrintWindow failed)");
                }
            }
            finally
            {
                graphics.ReleaseHdc(dc);
            }

            return image;
        }
        catch
        {
            // The caller never receives the bitmap when rendering throws, so it is released here
            image.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Native <c>PrintWindow</c>.
    /// </summary>
    /// <param name="hwnd">The window.</param>
    /// <param name="hdc">The device context to draw into.</param>
    /// <param name="flags">The render flags.</param>
    /// <returns><see langword="true"/> on success.</returns>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);

    /// <summary>
    ///     Native <c>GetWindowRect</c>.
    /// </summary>
    /// <param name="hwnd">The window.</param>
    /// <param name="rect">Its frame.</param>
    /// <returns><see langword="true"/> on success.</returns>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowRect(IntPtr hwnd, out NativeRect rect);

    /// <summary>
    ///     The native <c>RECT</c> structure.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        /// <summary>
        ///     The left edge.
        /// </summary>
        public int Left;

        /// <summary>
        ///     The top edge.
        /// </summary>
        public int Top;

        /// <summary>
        ///     The right edge.
        /// </summary>
        public int Right;

        /// <summary>
        ///     The bottom edge.
        /// </summary>
        public int Bottom;
    }
}

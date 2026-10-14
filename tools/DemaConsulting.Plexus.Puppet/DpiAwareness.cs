using System.Runtime.InteropServices;

namespace DemaConsulting.Plexus.Puppet;

/// <summary>
///     Makes the Puppet process DPI aware, so the pixel coordinates it reads and injects are physical screen pixels.
/// </summary>
/// <remarks>
///     The demo reports geometry in physical pixels and Windows scales the coordinates of a DPI-unaware process
///     (UI Automation rectangles, the cursor position), so without this the mouse would land in the wrong place on a
///     scaled monitor.
/// </remarks>
internal static class DpiAwareness
{
    /// <summary>
    ///     The per-monitor-aware-v2 context handle (<c>DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2</c>).
    /// </summary>
    private static readonly IntPtr PerMonitorAwareV2 = new(-4);

    /// <summary>
    ///     Opts the process in to per-monitor DPI awareness, falling back to system awareness on old Windows.
    /// </summary>
    internal static void Enable()
    {
        try
        {
            if (!SetProcessDpiAwarenessContext(PerMonitorAwareV2))
            {
                SetProcessDPIAware();
            }
        }
        catch (EntryPointNotFoundException)
        {
            // Old Windows without the per-monitor API: the system-wide awareness fallback is not available either
        }
        catch (DllNotFoundException)
        {
            // Not Windows: nothing to configure
        }
    }

    /// <summary>
    ///     Native <c>SetProcessDpiAwarenessContext</c>.
    /// </summary>
    /// <param name="value">The awareness context.</param>
    /// <returns><see langword="true"/> on success.</returns>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

    /// <summary>
    ///     Native <c>SetProcessDPIAware</c>.
    /// </summary>
    /// <returns><see langword="true"/> on success.</returns>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessDPIAware();
}

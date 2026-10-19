using System.Diagnostics;
using DemaConsulting.Plexus.Puppet.Automation;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using FlaUiApplication = FlaUI.Core.Application;

namespace DemaConsulting.Plexus.Puppet;

/// <summary>
///     A FlaUI attachment to one running <c>plexus-demo</c> process and its main window.
/// </summary>
internal sealed class AttachContext : IDisposable
{
    /// <summary>
    ///     How long to wait for the main window to exist.
    /// </summary>
    private static readonly TimeSpan WindowAppearTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    ///     The UI Automation session, disposed with the context.
    /// </summary>
    private readonly UIA3Automation _automation;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AttachContext"/> class.
    /// </summary>
    /// <param name="alias">The instance alias.</param>
    /// <param name="automation">The UI Automation session.</param>
    /// <param name="application">The attached application.</param>
    /// <param name="mainWindow">The main window.</param>
    private AttachContext(string alias, UIA3Automation automation, FlaUiApplication application, Window mainWindow)
    {
        Alias = alias;
        _automation = automation;
        Application = application;
        MainWindow = mainWindow;
    }

    /// <summary>
    ///     Gets the instance alias.
    /// </summary>
    internal string Alias { get; }

    /// <summary>
    ///     Gets the attached application.
    /// </summary>
    internal FlaUiApplication Application { get; }

    /// <summary>
    ///     Gets the main window.
    /// </summary>
    internal Window MainWindow { get; }

    /// <summary>
    ///     Gets the process id of the instance.
    /// </summary>
    internal int ProcessId => Application.ProcessId;

    /// <summary>
    ///     Gets the element that holds keyboard focus.
    /// </summary>
    /// <returns>The element, or <see langword="null"/> when none can be read.</returns>
    internal AutomationElement? TryGetFocusedElement()
    {
        try
        {
            return _automation.FocusedElement();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return null;
        }
    }

    /// <summary>
    ///     Gets the UI Automation root of the desktop.
    /// </summary>
    /// <returns>The desktop element.</returns>
    internal AutomationElement GetDesktop() => _automation.GetDesktop();

    /// <summary>
    ///     Gets every top-level window of the instance: the main window first, then dialogs and open pop-ups.
    /// </summary>
    /// <returns>The windows.</returns>
    internal IReadOnlyList<AutomationElement> Roots()
    {
        var roots = new List<AutomationElement> { MainWindow };
        var mainHandle = UiActions.Safe(() => MainWindow.Properties.NativeWindowHandle.ValueOrDefault, IntPtr.Zero);
        var others = UiActions.Safe(
            () => GetDesktop().FindAllChildren(cf => cf.ByProcessId(ProcessId)),
            []);

        // UI Automation nests an owned window (a modal dialog, a tooltip pop-up) under its owner instead of listing it on the desktop
        var owned = UiActions.Safe(
            () => MainWindow.FindAllChildren(cf => cf.ByControlType(ControlType.Window).Or(cf.ByControlType(ControlType.ToolTip))),
            []);
        var seen = new HashSet<IntPtr> { mainHandle };
        foreach (var other in others.Concat(owned))
        {
            var handle = UiActions.Safe(() => other.Properties.NativeWindowHandle.ValueOrDefault, IntPtr.Zero);
            if (handle == IntPtr.Zero || seen.Add(handle))
            {
                roots.Add(other);
            }
        }

        return roots;
    }

    /// <summary>
    ///     Attaches to a process.
    /// </summary>
    /// <param name="alias">The instance alias.</param>
    /// <param name="process">The process.</param>
    /// <param name="error">Why attaching failed when the result is <see langword="null"/>.</param>
    /// <returns>The context, or <see langword="null"/> on failure.</returns>
    internal static AttachContext? TryAttach(string alias, Process process, out string error)
    {
        var automation = new UIA3Automation();
        var attached = false;
        try
        {
            var app = FlaUiApplication.Attach(process);
            var window = app.GetMainWindow(automation, WindowAppearTimeout);
            if (window is null)
            {
                error = $"Instance '{alias}' (pid={process.Id}) has no main window yet.";
                return null;
            }

            error = string.Empty;
            var context = new AttachContext(alias, automation, app, window);
            attached = true;
            return context;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            error = ex.Message;
            return null;
        }
        finally
        {
            // The session is owned by the context on success; on every other path (no window, an exception) it is released exactly once here
            if (!attached)
            {
                automation.Dispose();
            }
        }
    }

    /// <summary>
    ///     Releases the UI Automation session.
    /// </summary>
    public void Dispose()
    {
        _automation.Dispose();
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using DemaConsulting.Plexus.Demo.Gui.Automation;
using Material.Icons.Avalonia;

namespace DemaConsulting.Plexus.Demo.Gui;

/// <summary>
///     The Avalonia application: applies the theme, shows the main window and, when asked, starts the automation endpoint.
/// </summary>
internal sealed class App : Application
{
    /// <summary>
    ///     How long the process waits for a graceful shutdown after an automation <c>quit</c> before it exits anyway.
    /// </summary>
    private static readonly TimeSpan QuitGrace = TimeSpan.FromSeconds(5);

    /// <summary>
    ///     The command-line options.
    /// </summary>
    private readonly GuiOptions _options;

    /// <summary>
    ///     The automation pipe server, or null when the endpoint is disabled.
    /// </summary>
    private AutomationServer? _server;

    /// <summary>
    ///     Initializes a new instance of the <see cref="App"/> class.
    /// </summary>
    /// <param name="options">The command-line options.</param>
    public App(GuiOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        // Fluent gives every control a real template; the demo has no XAML styles of its own
        Styles.Add(new FluentTheme());

        // The toolbar icons (Material.Icons.Avalonia needs its styles registered to draw)
        Styles.Add(new MaterialIconStyles(null));

        // Every window - the main window, the demo's dialogs and the library's property-grid and collection-editor
        // dialogs - shows the Plexus icon in its title bar and taskbar button, set once here rather than per window
        Styles.Add(new Style(selector => selector.Is<Window>())
        {
            Setters = { new Setter(Window.IconProperty, LoadWindowIcon()) },
        });
    }

    /// <summary>
    ///     The resource URI of the window icon: the repository's <c>Icon.png</c>, linked into the assembly.
    /// </summary>
    // cspell:ignore avares - Avalonia's resource URI scheme
    internal static readonly Uri WindowIconUri = new("avares://plexus-demo/Assets/Icon.png");

    /// <summary>
    ///     Loads the window icon from the assembly's resources.
    /// </summary>
    /// <returns>The icon shared by every window of the demo.</returns>
    internal static WindowIcon LoadWindowIcon()
    {
        using var stream = AssetLoader.Open(WindowIconUri);
        return new WindowIcon(stream);
    }

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow(_options);
            desktop.MainWindow = window;

            // A demo must not die from a failed command: show the problem in the status bar instead
            Dispatcher.UIThread.UnhandledException += (_, e) =>
            {
                window.Session.SetMessage($"Error: {e.Exception.Message}");
                e.Handled = true;
            };

            if (_options.AutomationName is { } name)
            {
                StartAutomation(window, desktop, name);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    ///     Starts the demo-only automation endpoint.
    /// </summary>
    /// <param name="window">The main window whose session the commands drive.</param>
    /// <param name="desktop">The application lifetime, used to quit.</param>
    /// <param name="name">The automation name.</param>
    private void StartAutomation(MainWindow window, IClassicDesktopStyleApplicationLifetime desktop, string name)
    {
        var dispatcher = new AutomationDispatcher(window.Session);

        // Commands touch the model and the controls, so each request hops to the UI thread
        _server = new AutomationServer(
            name,
            line => Dispatcher.UIThread.InvokeAsync(() => dispatcher.ExecuteAsync(line)),
            () =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    window.CloseWithoutPrompt();
                    desktop.Shutdown();
                });

                // Safety net for a scripted quit: never leave a stray process behind, even if a dialog blocks shutdown
                _ = Task.Delay(QuitGrace).ContinueWith(_ => Environment.Exit(0), TaskScheduler.Default);
            });
        _server.Start();
        desktop.Exit += (_, _) => _server?.Dispose();
    }
}

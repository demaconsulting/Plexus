using Avalonia;

namespace DemaConsulting.Plexus.Demo.Gui;

/// <summary>
///     The entry point of <c>plexus-demo</c>, the reference host application for the Plexus diagram editor.
/// </summary>
internal static class Program
{
    /// <summary>
    ///     Parses the command line and runs the application.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The process exit code: 0 on success, 2 for a usage error.</returns>
    [STAThread]
    public static int Main(string[] args)
    {
        if (!GuiOptions.TryParse(args, out var options, out var error) || options is null)
        {
            Console.Error.WriteLine(error);
            Console.Error.WriteLine(GuiOptions.Usage);
            return 2;
        }

        if (options.ShowHelp)
        {
            Console.WriteLine(GuiOptions.Usage);
            return 0;
        }

        return BuildAvaloniaApp(options).StartWithClassicDesktopLifetime(args);
    }

    /// <summary>
    ///     Configures the Avalonia application for the desktop platform.
    /// </summary>
    /// <param name="options">The parsed command line.</param>
    /// <returns>The application builder.</returns>
    public static AppBuilder BuildAvaloniaApp(GuiOptions options) =>
        AppBuilder.Configure(() => new App(options)).UsePlatformDetect();
}

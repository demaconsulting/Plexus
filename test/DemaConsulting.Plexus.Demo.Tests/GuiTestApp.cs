using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using Material.Icons.Avalonia;

namespace DemaConsulting.Plexus.Demo.Tests;

/// <summary>
///     The minimal Avalonia application the GUI tests run inside: the Fluent theme and nothing else.
/// </summary>
public sealed class GuiTestApp : Application
{
    /// <inheritdoc />
    public override void Initialize()
    {
        // Fluent gives dialogs and menus real templates, so controls lay out and focus as in the demo
        Styles.Add(new FluentTheme());

        // The demo window's toolbar icons need their styles to draw
        Styles.Add(new MaterialIconStyles(null));
    }
}

/// <summary>
///     Builds the headless Skia application used by the GUI tests.
/// </summary>
public static class GuiTestAppBuilder
{
    /// <summary>
    ///     Configures a headless application that still draws with Skia, so text measurement and
    ///     rendered frames are real rather than stubbed.
    /// </summary>
    /// <returns>The configured application builder.</returns>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<GuiTestApp>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;

namespace DemaConsulting.Plexus.Avalonia.Tests;

/// <summary>
///     The minimal Avalonia application the headless tests run inside: the Fluent theme and nothing else.
/// </summary>
public sealed class TestApp : Application
{
    /// <inheritdoc />
    public override void Initialize()
    {
        // Fluent gives the rename TextBox a real template, so focus and text input behave as in an app
        Styles.Add(new FluentTheme());
    }
}

/// <summary>
///     Builds the headless Skia application used by the tests.
/// </summary>
public static class TestAppBuilder
{
    /// <summary>
    ///     Configures a headless application that still draws with Skia, so text measurement and
    ///     rendered frames are real rather than stubbed.
    /// </summary>
    /// <returns>The configured application builder.</returns>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApp>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

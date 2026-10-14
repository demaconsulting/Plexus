using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests;

/// <summary>
///     The minimal Avalonia application the headless tests run inside: the Fluent theme and nothing else.
/// </summary>
public sealed class TestApp : global::Avalonia.Application
{
    /// <inheritdoc />
    public override void Initialize()
    {
        // Fluent gives controls a real template, so the UI phase can drive focus and input as in an app
        Styles.Add(new FluentTheme());
    }
}

/// <summary>
///     Builds the headless Skia application used by the tests.
/// </summary>
public static class TestAppBuilder
{
    /// <summary>
    ///     Configures a headless application that still draws with Skia, so text measurement and rendered frames are
    ///     real rather than stubbed.
    /// </summary>
    /// <returns>The configured application builder.</returns>
    public static global::Avalonia.AppBuilder BuildAvaloniaApp() =>
        global::Avalonia.AppBuilder.Configure<TestApp>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

using Avalonia.Headless;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests;

/// <summary>
///     Runs test bodies on the headless Avalonia UI thread (used by the grid control tests of the UI phase).
/// </summary>
/// <remarks>
///     Avalonia controls may only be touched on the UI thread. This helper owns one headless session for the whole
///     test run (Avalonia allows only one) and executes each test body on that session's UI thread, rethrowing
///     failures so xUnit reports them normally. It stands in for the <c>[AvaloniaFact]</c> attribute of
///     Avalonia.Headless.XUnit, which does not work with xunit.v3 4.x.
/// </remarks>
public static class UiTest
{
    /// <summary>
    ///     The shared headless session.
    /// </summary>
    private static readonly HeadlessUnitTestSession Session = HeadlessUnitTestSession.StartNew(typeof(TestAppBuilder));

    /// <summary>
    ///     Runs a test body on the UI thread.
    /// </summary>
    /// <param name="body">The test body.</param>
    /// <returns>A task that completes when the body has finished; return it from the test so xUnit awaits it.</returns>
    public static Task Run(Action body) => Session.Dispatch(body, CancellationToken.None);
}

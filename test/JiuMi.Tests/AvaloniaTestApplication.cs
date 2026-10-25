using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(JiuMi.Tests.AvaloniaTestApplication))]

namespace JiuMi.Tests;

/// <summary>
/// Hosts the Fluent theme for tests that need a running Avalonia application.
/// </summary>
public static class AvaloniaTestApplication
{
    /// <summary>
    /// Builds the headless application the tests run on.
    /// </summary>
    /// <returns>The configured builder.</returns>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApplication>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

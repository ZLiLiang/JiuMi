using Avalonia;
using Avalonia.Headless;
using FluentAvalonia.Styling;

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

/// <summary>
/// Minimal application whose styles match the shell, so resource lookups behave as they do at runtime.
/// </summary>
public sealed class TestApplication : global::Avalonia.Application
{
    /// <inheritdoc />
    public override void Initialize() => Styles.Add(new FluentAvaloniaTheme());
}

/// <summary>
/// Serializes the tests that share one Avalonia application.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AvaloniaTestGroup
{
    /// <summary>The collection name used by the headless tests.</summary>
    public const string Name = "Avalonia";
}

/// <summary>
/// Runs test bodies on the headless application's UI thread.
/// </summary>
internal static class HeadlessTest
{
    private static readonly HeadlessUnitTestSession _session =
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(HeadlessTest).Assembly);

    /// <summary>
    /// Dispatches <paramref name="action"/> to the UI thread and waits for it to finish.
    /// </summary>
    /// <param name="action">The test body to run.</param>
    public static void Run(Action action) =>
        _session.Dispatch(action, CancellationToken.None);
}

using Avalonia.Headless;

namespace JiuMi.Tests;

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

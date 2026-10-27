using JiuMi.Infrastructure.Shell;

namespace JiuMi.Infrastructure.Tests;

public sealed class SingleInstanceCoordinatorTests
{
    [Fact]
    public void CreateName_GivenSamePath_IsStableAndNamespaced()
    {
        string name = SingleInstanceCoordinator.CreateName(@"C:\Apps\JiuMi\JiuMi.Desktop.exe");
        string same = SingleInstanceCoordinator.CreateName(@"C:\Apps\JiuMi\JiuMi.Desktop.exe");
        string other = SingleInstanceCoordinator.CreateName(@"C:\Other\JiuMi.Desktop.exe");

        Assert.StartsWith("JiuMi.", name, StringComparison.Ordinal);
        Assert.Matches("^JiuMi\\.[0-9A-F]{32}$", name);
        Assert.Equal(name, same);
        Assert.NotEqual(name, other);
    }

    [Fact]
    public void TryAcquire_GivenFirstInstance_ReturnsTrue()
    {
        using var coordinator = new SingleInstanceCoordinator(CreateExecutablePath(), Serilog.Core.Logger.None);

        Assert.True(coordinator.TryAcquire());
    }

    [Fact]
    public void TryAcquire_GivenRunningInstance_RequestsActivationAndReturnsFalse()
    {
        string executablePath = CreateExecutablePath();
        using var first = new SingleInstanceCoordinator(executablePath, Serilog.Core.Logger.None);
        using var activation = new ManualResetEventSlim();
        first.ActivationRequested += (_, _) => activation.Set();

        Assert.True(first.TryAcquire());

        using var second = new SingleInstanceCoordinator(executablePath, Serilog.Core.Logger.None);
        Assert.False(second.TryAcquire());

        Assert.True(
            activation.Wait(TimeSpan.FromSeconds(10)),
            "The running instance did not receive the activation request.");
    }

    [Fact]
    public void TryAcquire_AfterFirstDisposed_ReturnsTrue()
    {
        string executablePath = CreateExecutablePath();
        using (var first = new SingleInstanceCoordinator(executablePath, Serilog.Core.Logger.None))
        {
            Assert.True(first.TryAcquire());
        }

        using var second = new SingleInstanceCoordinator(executablePath, Serilog.Core.Logger.None);
        Assert.True(second.TryAcquire());
    }

    private static string CreateExecutablePath() =>
        Path.Combine(Path.GetTempPath(), $"jiumi-{Guid.NewGuid():N}.exe");
}

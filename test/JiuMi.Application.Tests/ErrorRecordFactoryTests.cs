using JiuMi.Application.Errors;
using JiuMi.Application.Settings;

namespace JiuMi.Application.Tests;

public sealed class ErrorRecordFactoryTests
{
    [Fact]
    public void Create_GivenRegisteredCode_TakesTheLevelFromTheCatalog()
    {
        var factory = new ErrorRecordFactory(new StubApplicationInfo(), new StubSettingsStore(), TimeProvider.System);

        ErrorRecord record = factory.Create(ErrorCodes.LocalizationSwitchFailed, "Localization.Switch");

        Assert.Equal(ErrorLevel.Warning, record.Level);
    }

    [Fact]
    public void Create_GivenExplicitLevelForUnregisteredCode_KeepsTheLevelWithoutAPresentation()
    {
        var factory = new ErrorRecordFactory(new StubApplicationInfo(), new StubSettingsStore(), TimeProvider.System);

        ErrorRecord record = factory.Create(
            ErrorCodes.UnexpectedUnobservedTask,
            "TaskScheduler.UnobservedTaskException",
            new InvalidOperationException("abandoned"),
            ErrorLevel.Warning);

        Assert.Equal(ErrorLevel.Warning, record.Level);
        Assert.Null(ErrorPresentationCatalog.Find(ErrorCodes.UnexpectedUnobservedTask));
        Assert.Equal("System.InvalidOperationException", record.ExceptionType);
    }

    [Fact]
    public void Create_GivenRegisteredCodeAndExplicitLevel_KeepsTheCatalogLevel()
    {
        var factory = new ErrorRecordFactory(new StubApplicationInfo(), new StubSettingsStore(), TimeProvider.System);

        ErrorRecord record = factory.Create(
            ErrorCodes.LocalizationSwitchFailed,
            "Localization.Switch",
            level: ErrorLevel.Error);

        Assert.Equal(ErrorLevel.Warning, record.Level);
    }

    private sealed class StubApplicationInfo : IApplicationInfo
    {
        public string Name => "JiuMi";

        public string Version => "1.0.0";

        public string RepositoryUrl => "https://example.test/JiuMi";
    }

    private sealed class StubSettingsStore : ISettingsStore
    {
        public SettingsLocation Location { get; } = SettingsLocation.MemoryOnly;

        public AppSettings Current { get; } = new();

        public SettingsLoadResult Load() => throw new NotSupportedException();

        public SettingsSaveResult Save(AppSettings settings) => throw new NotSupportedException();
    }
}

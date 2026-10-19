using JiuMi.Application.Errors;

namespace JiuMi.Application.Tests;

public sealed class ErrorRecordFormatterTests
{
    [Fact]
    public void Format_GivenFullRecord_WritesTextThenOneFieldPerLine()
    {
        var record = new ErrorRecord
        {
            Timestamp = new DateTimeOffset(2026, 10, 19, 15, 12, 46, TimeSpan.FromHours(8)).AddMilliseconds(290),
            Level = ErrorLevel.Error,
            Code = "Settings.SaveFailed",
            Operation = "Settings.Save",
            ExceptionType = "System.IO.IOException",
            ExceptionMessage = "disk full",
            StackTrace = "   at Alpha()" + Environment.NewLine + "   at Beta()",
            AppVersion = "1.2.3",
            OsDescription = "Windows 11",
            ConfigPath = @"C:\config\settings.json",
            Count = 2,
        };

        string text = ErrorRecordFormatter.Format(record, "设置保存失败");

        string[] lines =
        [
            "设置保存失败",
            "timestamp: 2026-10-19T15:12:46.2900000+08:00",
            "level: Error",
            "code: Settings.SaveFailed",
            "operation: Settings.Save",
            "exceptionType: System.IO.IOException",
            "exceptionMessage: disk full",
            "stackTrace:    at Alpha()",
            "   at Beta()",
            "appVersion: 1.2.3",
            "osDescription: Windows 11",
            @"configPath: C:\config\settings.json",
            "count: 2",
        ];

        Assert.Equal(string.Join(Environment.NewLine, lines) + Environment.NewLine, text);
    }

    [Fact]
    public void Format_GivenNoOptionalFields_OmitsTheirLines()
    {
        var record = new ErrorRecord
        {
            Timestamp = new DateTimeOffset(2026, 10, 19, 15, 12, 46, TimeSpan.FromHours(8)),
            Level = ErrorLevel.Warning,
            Code = "Localization.SwitchFailed",
            Operation = "Localization.Switch",
        };

        string text = ErrorRecordFormatter.Format(record, "语言切换失败");

        string[] expected =
        [
            "语言切换失败",
            "timestamp: 2026-10-19T15:12:46.0000000+08:00",
            "level: Warning",
            "code: Localization.SwitchFailed",
            "operation: Localization.Switch",
            "count: 1",
        ];

        Assert.Equal(string.Join(Environment.NewLine, expected) + Environment.NewLine, text);
    }
}

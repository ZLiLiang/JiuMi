using JiuMi.Application.Errors;
using JiuMi.Infrastructure.Errors;

namespace JiuMi.Infrastructure.Tests;

public sealed class ErrorReporterTests
{
    [Fact]
    public void Report_GivenSameCodeTwice_RaisesTheMergedCount()
    {
        var reporter = new ErrorReporter();
        DateTimeOffset timestamp = new(2026, 10, 19, 10, 0, 0, TimeSpan.FromHours(8));
        ErrorRecord? raised = null;
        reporter.Reported += (_, record) => raised = record;

        reporter.Report(CreateRecord(timestamp));
        reporter.Report(CreateRecord(timestamp.AddMinutes(1)));

        Assert.NotNull(raised);
        Assert.Equal(2, raised.Count);
        Assert.Single(reporter.Details.Records);
    }

    private static ErrorRecord CreateRecord(DateTimeOffset timestamp) =>
        new()
        {
            Timestamp = timestamp,
            Level = ErrorLevel.Error,
            Code = ErrorCodes.SettingsSaveFailed,
            Operation = "Settings.Save",
        };
}

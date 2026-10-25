using JiuMi.Application.Errors;
using JiuMi.Infrastructure.Errors;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace JiuMi.Infrastructure.Tests;

public sealed class ErrorReporterTests
{
    [Fact]
    public void Report_GivenSameCodeTwice_RaisesTheMergedCount()
    {
        using Logger logger = CreateLogger(out _);
        var reporter = new ErrorReporter(logger);
        DateTimeOffset timestamp = new(2026, 10, 19, 10, 0, 0, TimeSpan.FromHours(8));
        ErrorRecord? raised = null;
        reporter.Reported += (_, record) => raised = record;

        reporter.Report(CreateRecord(timestamp));
        reporter.Report(CreateRecord(timestamp.AddMinutes(1)));

        Assert.NotNull(raised);
        Assert.Equal(2, raised.Count);
        Assert.Single(reporter.Details.Records);
    }

    [Fact]
    public void Report_GivenAWarningRecord_WritesOneEventWithTheRecordFields()
    {
        using Logger logger = CreateLogger(out CollectingSink sink);
        var reporter = new ErrorReporter(logger);
        ErrorRecord record = CreateRecord(
            new DateTimeOffset(2026, 10, 19, 10, 0, 0, TimeSpan.FromHours(8)),
            ErrorLevel.Warning);

        reporter.Report(record);

        LogEvent logEvent = Assert.Single(sink.Events);
        Assert.Equal(LogEventLevel.Warning, logEvent.Level);
        Assert.Equal(record.Code, ReadProperty(logEvent, "code"));
        Assert.Equal(record.Operation, ReadProperty(logEvent, "operation"));
        Assert.Equal("1", ReadProperty(logEvent, "count"));
    }

    [Fact]
    public void Report_GivenAnErrorRecordWithAnException_WritesItsDetailsToTheEvent()
    {
        using Logger logger = CreateLogger(out CollectingSink sink);
        var reporter = new ErrorReporter(logger);
        ErrorRecord record = CreateRecord(
            new DateTimeOffset(2026, 10, 19, 10, 0, 0, TimeSpan.FromHours(8)),
            ErrorLevel.Error) with
        {
            ExceptionType = "System.IO.IOException",
            ExceptionMessage = "Disk is full.",
            StackTrace = "at Example()",
        };

        reporter.Report(record);

        LogEvent logEvent = Assert.Single(sink.Events);
        Assert.Equal(LogEventLevel.Error, logEvent.Level);
        Assert.Equal("System.IO.IOException", ReadProperty(logEvent, "exceptionType"));
        Assert.Equal("Disk is full.", ReadProperty(logEvent, "exceptionMessage"));
        Assert.Equal("at Example()", ReadProperty(logEvent, "stackTrace"));
    }

    [Fact]
    public void Report_GivenARepeatedCode_WritesOneEventPerReportWithTheMergedCount()
    {
        using Logger logger = CreateLogger(out CollectingSink sink);
        var reporter = new ErrorReporter(logger);
        DateTimeOffset timestamp = new(2026, 10, 19, 10, 0, 0, TimeSpan.FromHours(8));

        reporter.Report(CreateRecord(timestamp));
        reporter.Report(CreateRecord(timestamp.AddMinutes(1)));

        Assert.Equal(2, sink.Events.Count);
        Assert.Equal("1", ReadProperty(sink.Events[0], "count"));
        Assert.Equal("2", ReadProperty(sink.Events[1], "count"));
    }

    private static Logger CreateLogger(out CollectingSink sink)
    {
        sink = new CollectingSink();
        return new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(sink)
            .CreateLogger();
    }

    private static string? ReadProperty(LogEvent logEvent, string name) =>
        logEvent.Properties.TryGetValue(name, out LogEventPropertyValue? value) &&
        value is ScalarValue scalar
            ? scalar.Value?.ToString()
            : null;

    private static ErrorRecord CreateRecord(
        DateTimeOffset timestamp,
        ErrorLevel level = ErrorLevel.Error) =>
        new()
        {
            Timestamp = timestamp,
            Level = level,
            Code = ErrorCodes.SettingsSaveFailed,
            Operation = "Settings.Save",
        };

    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events
        {
            get;
        } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}

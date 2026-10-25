using JiuMi.Application.Errors;

namespace JiuMi.Application.Tests;

public sealed class ErrorDetailBufferTests
{
    [Fact]
    public void Add_GivenSameCode_MergesIntoOneRecordAndIncrementsCount()
    {
        var buffer = new ErrorDetailBuffer();
        DateTimeOffset first = new(2026, 10, 19, 10, 0, 0, TimeSpan.FromHours(8));
        DateTimeOffset second = first.AddMinutes(5);

        buffer.Add(CreateRecord("Settings.SaveFailed", first));
        ErrorRecord merged = buffer.Add(CreateRecord("Settings.SaveFailed", second));

        Assert.Equal(2, merged.Count);
        ErrorRecord stored = Assert.Single(buffer.Records);
        Assert.Equal(2, stored.Count);
        Assert.Equal(second, stored.Timestamp);
    }

    [Fact]
    public void Add_GivenMoreThanCapacity_DropsOldestRecords()
    {
        var buffer = new ErrorDetailBuffer();
        DateTimeOffset timestamp = new(2026, 10, 19, 10, 0, 0, TimeSpan.FromHours(8));

        for (int index = 0; index < ErrorDetailBuffer.Capacity + 10; index++)
        {
            buffer.Add(CreateRecord("Code." + index, timestamp.AddSeconds(index)));
        }

        Assert.Equal(ErrorDetailBuffer.Capacity, buffer.Records.Count);
        Assert.Null(buffer.Find("Code.9"));
        Assert.NotNull(buffer.Find("Code.10"));
        Assert.NotNull(buffer.Find("Code.59"));
    }

    private static ErrorRecord CreateRecord(string code, DateTimeOffset timestamp) =>
        new()
        {
            Timestamp = timestamp,
            Level = ErrorLevel.Error,
            Code = code,
            Operation = "Settings.Save",
        };
}

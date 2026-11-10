using System.Text.Json;
using JiuMi.Application.FileRename;
using JiuMi.Infrastructure.FileRename;

namespace JiuMi.Infrastructure.Tests;

public sealed class RenameBatchLogWriterTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "JiuMi.Tests." + Guid.NewGuid().ToString("N"));

    public RenameBatchLogWriterTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task WriteAsync_GivenBatch_WritesReadableJson()
    {
        string path = Path.Combine(_root, "rename.json");
        var batch = new RenameBatch(
            new DateTimeOffset(2026, 10, 31, 15, 30, 0, TimeSpan.FromHours(8)),
            BackupEnabled: false,
            BackupDirectory: null,
            [
                new RenameBatchEntry(
                    @"D:\work\a.txt",
                    @"D:\work\b.txt",
                    IsDirectory: false,
                    Stamp: null,
                    RenameBatchEntryState.Renamed),
            ]);

        await new RenameBatchLogWriter().WriteAsync(path, batch);

        using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        Assert.Equal("renamed", document.RootElement.GetProperty("items")[0].GetProperty("state").GetString());
        Assert.Equal(@"D:\work\a.txt", document.RootElement.GetProperty("items")[0].GetProperty("from").GetString());
    }
}

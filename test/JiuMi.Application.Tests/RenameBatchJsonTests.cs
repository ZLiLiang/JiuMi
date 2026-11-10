using System.Text.Json;
using JiuMi.Application.FileRename;

namespace JiuMi.Application.Tests;

public sealed class RenameBatchJsonTests
{
    [Fact]
    public void Serialize_GivenBatch_WritesPathsStatesAndFailure()
    {
        var batch = new RenameBatch(
            new DateTimeOffset(2026, 10, 31, 15, 30, 0, TimeSpan.FromHours(8)),
            BackupEnabled: true,
            BackupDirectory: @"D:\JiuMi-backup-20261031-153000",
            [
                new RenameBatchEntry(
                    @"D:\work\a.txt",
                    @"D:\work\b.txt",
                    IsDirectory: false,
                    new RenameFileStamp(12, new DateTime(2026, 10, 31, 7, 0, 0, DateTimeKind.Utc)),
                    RenameBatchEntryState.Renamed),
                new RenameBatchEntry(
                    @"D:\work\c.txt",
                    @"D:\work\d.txt",
                    IsDirectory: false,
                    Stamp: null,
                    RenameBatchEntryState.Failed,
                    RenameFailure.AccessDenied),
            ]);

        using JsonDocument document = JsonDocument.Parse(RenameBatchJson.Serialize(batch));
        JsonElement root = document.RootElement;

        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.True(root.GetProperty("backupEnabled").GetBoolean());
        Assert.Equal(@"D:\JiuMi-backup-20261031-153000", root.GetProperty("backupDirectory").GetString());
        JsonElement items = root.GetProperty("items");
        Assert.Equal(2, items.GetArrayLength());
        Assert.Equal("file", items[0].GetProperty("kind").GetString());
        Assert.Equal("renamed", items[0].GetProperty("state").GetString());
        Assert.False(items[0].TryGetProperty("failure", out _));
        Assert.Equal("accessDenied", items[1].GetProperty("failure").GetString());
    }
}

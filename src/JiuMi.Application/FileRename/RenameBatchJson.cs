using System.Text.Json;

namespace JiuMi.Application.FileRename;

/// <summary>
/// Writes the last batch as a readable JSON document. The document is for the user, not for
/// re-importing it later.
/// </summary>
public static class RenameBatchJson
{
    private const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Serializes a batch.
    /// </summary>
    /// <param name="batch">The batch to write.</param>
    /// <returns>The JSON text.</returns>
    public static string Serialize(RenameBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var document = new LogDocument(
            CurrentSchemaVersion,
            batch.ExecutedAt,
            batch.BackupEnabled,
            batch.BackupDirectory,
            [
                .. batch.Entries.Select(entry => new LogEntry(
                    entry.OriginalPath,
                    entry.RenamedPath,
                    entry.IsDirectory ? "folder" : "file",
                    ToStorageValue(entry.State),
                    entry.Failure is { } failure ? ToStorageValue(failure) : null)),
            ]);

        return JsonSerializer.Serialize(document, _options);
    }

    private static string ToStorageValue(RenameBatchEntryState state) => state switch
    {
        RenameBatchEntryState.Renamed => "renamed",
        RenameBatchEntryState.Failed => "failed",
        RenameBatchEntryState.Undone => "undone",
        _ => "undoFailed",
    };

    private static string ToStorageValue(RenameFailure failure) => failure switch
    {
        RenameFailure.SourceMissing => "sourceMissing",
        RenameFailure.TargetExists => "targetExists",
        RenameFailure.AccessDenied => "accessDenied",
        RenameFailure.BackupFailed => "backupFailed",
        RenameFailure.Modified => "modified",
        _ => "unknown",
    };

    private sealed record LogDocument(
        int SchemaVersion,
        DateTimeOffset ExecutedAt,
        bool BackupEnabled,
        string? BackupDirectory,
        IReadOnlyList<LogEntry> Items);

    private sealed record LogEntry(
        string From,
        string To,
        string Kind,
        string State,
        string? Failure);
}

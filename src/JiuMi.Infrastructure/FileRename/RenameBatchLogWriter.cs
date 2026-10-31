using System.Text;
using JiuMi.Application.FileRename;

namespace JiuMi.Infrastructure.FileRename;

/// <summary>
/// Writes the last batch as an indented JSON file.
/// </summary>
public sealed class RenameBatchLogWriter : IRenameBatchLogWriter
{
    /// <inheritdoc />
    public async Task WriteAsync(
        string path,
        RenameBatch batch,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(batch);

        string json = RenameBatchJson.Serialize(batch);
        await File.WriteAllTextAsync(path, json, new UTF8Encoding(false), cancellationToken)
            .ConfigureAwait(false);
    }
}

namespace JiuMi.Application.FileRename;

/// <summary>
/// Writes the last batch to a JSON file the user chooses.
/// </summary>
public interface IRenameBatchLogWriter
{
    /// <summary>
    /// Writes the batch log.
    /// </summary>
    /// <param name="path">The absolute destination path.</param>
    /// <param name="batch">The batch to write.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the file is written.</returns>
    Task WriteAsync(string path, RenameBatch batch, CancellationToken cancellationToken = default);
}

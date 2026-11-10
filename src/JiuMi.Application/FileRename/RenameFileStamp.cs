namespace JiuMi.Application.FileRename;

/// <summary>
/// The size and write time of a renamed file, used to detect outside changes before undo.
/// </summary>
/// <param name="Length">The file length in bytes.</param>
/// <param name="LastWriteTimeUtc">The last write time in UTC ticks.</param>
public sealed record RenameFileStamp(long Length, DateTime LastWriteTimeUtc);

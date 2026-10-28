namespace JiuMi.Application.WorldClock;

/// <summary>
/// One catalog document listed by a catalog manifest.
/// </summary>
/// <param name="Name">The file name inside the catalog directory.</param>
/// <param name="Sha256">The hexadecimal SHA-256 of the file content.</param>
/// <param name="Size">The file size in bytes.</param>
public sealed record WorldClockCatalogManifestFile(string Name, string Sha256, long Size);

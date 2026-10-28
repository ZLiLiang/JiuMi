using System;
using System.Collections.Generic;

namespace JiuMi.Application.WorldClock;

/// <summary>
/// Describes one published version of the catalog: what it contains and when it changed.
/// </summary>
/// <param name="SchemaVersion">The manifest schema this document uses.</param>
/// <param name="Version">The monotonic catalog version; a higher number replaces a lower one.</param>
/// <param name="UpdatedAt">When the catalog data last changed.</param>
/// <param name="Files">The three catalog documents this version publishes.</param>
public sealed record WorldClockCatalogManifest(
    int SchemaVersion,
    int Version,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<WorldClockCatalogManifestFile> Files)
{
    /// <summary>
    /// Gets the manifest schema the application reads.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// Gets the documents every catalog version must publish.
    /// </summary>
    public static IReadOnlyList<string> RequiredFiles
    {
        get;
    } = ["countries.json", "cities.json", "office-hours.json"];
}

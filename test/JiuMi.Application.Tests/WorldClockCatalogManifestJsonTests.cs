using JiuMi.Application.WorldClock;

namespace JiuMi.Application.Tests;

/// <summary>
/// Validation of the catalog manifest that drives the online update.
/// </summary>
public sealed class WorldClockCatalogManifestJsonTests
{
    private const string Sha =
        "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";

    [Fact]
    public void TryParse_ValidDocument_BuildsTheManifest()
    {
        bool parsed = WorldClockCatalogManifestJson.TryParse(
            BuildJson(DefaultFiles),
            out WorldClockCatalogManifest? manifest,
            out string? error);

        Assert.True(parsed);
        Assert.Null(error);
        Assert.NotNull(manifest);
        Assert.Equal(1, manifest!.SchemaVersion);
        Assert.Equal(20261028, manifest.Version);
        Assert.Equal(
            new DateTimeOffset(2026, 10, 28, 9, 0, 0, TimeSpan.Zero),
            manifest.UpdatedAt);
        Assert.Equal(3, manifest.Files.Count);
        Assert.Contains(manifest.Files, file => file.Name == "cities.json" && file.Size == 12);
    }

    [Fact]
    public void TryParse_GivenUnknownSchemaVersion_ReturnsError()
    {
        AssertRejected(BuildJson(DefaultFiles, schemaVersion: 2));
    }

    [Fact]
    public void TryParse_GivenNonPositiveVersion_ReturnsError()
    {
        AssertRejected(BuildJson(DefaultFiles, version: 0));
    }

    [Fact]
    public void TryParse_GivenMissingFile_ReturnsError()
    {
        AssertRejected(BuildJson(
            """
            { "name": "countries.json", "sha256": "%SHA%", "size": 11 },
            { "name": "cities.json", "sha256": "%SHA%", "size": 12 }
            """));
    }

    [Fact]
    public void TryParse_GivenUnknownFile_ReturnsError()
    {
        AssertRejected(BuildJson(DefaultFiles.Replace(
            "\"countries.json\"",
            "\"../settings.json\"",
            StringComparison.Ordinal)));
    }

    [Fact]
    public void TryParse_GivenNonHexHash_ReturnsError()
    {
        AssertRejected(BuildJson(DefaultFiles.Replace(
            "%SHA%",
            new string('z', 64),
            StringComparison.Ordinal)));
    }

    [Fact]
    public void TryParse_GivenZeroSize_ReturnsError()
    {
        AssertRejected(BuildJson(DefaultFiles.Replace("\"size\": 11", "\"size\": 0", StringComparison.Ordinal)));
    }

    [Fact]
    public void TryParse_GivenInvalidJson_ReturnsError()
    {
        AssertRejected("{");
    }

    private const string DefaultFiles = """
        { "name": "countries.json", "sha256": "%SHA%", "size": 11 },
        { "name": "cities.json", "sha256": "%SHA%", "size": 12 },
        { "name": "office-hours.json", "sha256": "%SHA%", "size": 13 }
        """;

    private static string BuildJson(string files, int schemaVersion = 1, int version = 20261028) =>
        $$"""
        {
          "schemaVersion": {{schemaVersion}},
          "version": {{version}},
          "updatedAt": "2026-10-28T09:00:00Z",
          "files": [
            {{files.Replace("%SHA%", Sha, StringComparison.Ordinal)}}
          ]
        }
        """;

    private static void AssertRejected(string json)
    {
        bool parsed = WorldClockCatalogManifestJson.TryParse(
            json,
            out WorldClockCatalogManifest? manifest,
            out string? error);

        Assert.False(parsed);
        Assert.Null(manifest);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }
}

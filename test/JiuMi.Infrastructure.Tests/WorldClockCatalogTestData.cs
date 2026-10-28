using System.Security.Cryptography;
using System.Text;
using JiuMi.Application.WorldClock;

namespace JiuMi.Infrastructure.Tests;

/// <summary>
/// Builds small but valid catalog bundles for the cache and updater tests.
/// </summary>
internal static class WorldClockCatalogTestData
{
    public static readonly DateTimeOffset UpdatedAt =
        new(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);

    public static (WorldClockCatalogManifest Manifest, IReadOnlyDictionary<string, byte[]> Files) Create(
        int version,
        string cityName)
    {
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["countries.json"] = Encoding.UTF8.GetBytes(CountriesJson),
            ["cities.json"] = Encoding.UTF8.GetBytes(CitiesJson(cityName)),
            ["office-hours.json"] = Encoding.UTF8.GetBytes(OfficeHoursJson),
        };

        var manifestFiles = new List<WorldClockCatalogManifestFile>(files.Count);
        foreach ((string name, byte[] content) in files)
        {
            manifestFiles.Add(new WorldClockCatalogManifestFile(
                name,
                Convert.ToHexStringLower(SHA256.HashData(content)),
                content.LongLength));
        }

        var manifest = new WorldClockCatalogManifest(
            WorldClockCatalogManifest.CurrentSchemaVersion,
            version,
            UpdatedAt,
            manifestFiles);
        return (manifest, files);
    }

    public static void WriteBundle(string directory, int version, string cityName)
    {
        (WorldClockCatalogManifest manifest, IReadOnlyDictionary<string, byte[]> files) =
            Create(version, cityName);
        Directory.CreateDirectory(directory);
        foreach (WorldClockCatalogManifestFile file in manifest.Files)
        {
            File.WriteAllBytes(Path.Combine(directory, file.Name), files[file.Name]);
        }
    }

    public static string ManifestJson(WorldClockCatalogManifest manifest) =>
        $$"""
        {
          "schemaVersion": {{manifest.SchemaVersion}},
          "version": {{manifest.Version}},
          "updatedAt": "{{manifest.UpdatedAt:O}}",
          "files": [
        {{string.Join(",\n", manifest.Files.Select(file => $$"""    { "name": "{{file.Name}}", "sha256": "{{file.Sha256}}", "size": {{file.Size}} }"""))}}
          ]
        }
        """;

    private const string CountriesJson = """
        {
          "countries": [
            { "iso2": "JP", "name": { "zh": "日本", "en": "Japan" }, "dialCode": "+81",
              "representative": { "zone": "Asia/Tokyo" } }
          ]
        }
        """;

    private const string OfficeHoursJson = """
        {
          "global": { "start": "09:00", "end": "18:00", "days": [1, 2, 3, 4, 5] },
          "overrides": []
        }
        """;

    private static string CitiesJson(string cityName) =>
        $$"""
        {
          "cities": [
            { "id": "Asia/Tokyo", "country": "JP", "name": { "zh": "{{cityName}}", "en": "Tokyo" } }
          ]
        }
        """;
}

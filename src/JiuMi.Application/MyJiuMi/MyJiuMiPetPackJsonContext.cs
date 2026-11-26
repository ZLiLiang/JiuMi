using System.Text.Json;
using System.Text.Json.Serialization;

namespace JiuMi.Application.MyJiuMi;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(MyJiuMiPetPackManifestLoader.ManifestDto))]
internal sealed partial class MyJiuMiPetPackJsonContext : JsonSerializerContext
{
}

using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;

namespace JiuMi.Application.MyJiuMi;

/// <summary>
/// Deserializes and validates JiuMi native character pack manifests (<c>schemaVersion: 1</c>) using
/// <see cref="System.Text.Json"/> source generation.
/// </summary>
public static class MyJiuMiPetPackManifestLoader
{
    /// <summary>
    /// The only manifest schema version supported by this loader.
    /// </summary>
    public const int SupportedSchemaVersion = 1;

    private static readonly IReadOnlyDictionary<string, MyJiuMiPetPackClip> _emptyClips =
        new ReadOnlyDictionary<string, MyJiuMiPetPackClip>(
            new Dictionary<string, MyJiuMiPetPackClip>(StringComparer.Ordinal));

    private static readonly IReadOnlyDictionary<string, string> _emptyFiles =
        new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(StringComparer.Ordinal));

    /// <summary>
    /// Parses and validates a character pack manifest from a JSON string.
    /// </summary>
    /// <param name="json">The raw JSON content of <c>manifest.json</c>.</param>
    /// <returns>A result containing the validated manifest or a failure reason.</returns>
    public static MyJiuMiPetPackLoadResult Load(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return MyJiuMiPetPackLoadResult.Failure("Manifest JSON is empty.");
        }

        ManifestDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize(json, MyJiuMiPetPackJsonContext.Default.ManifestDto);
        }
        catch (JsonException exception)
        {
            return MyJiuMiPetPackLoadResult.Failure($"Manifest JSON is invalid: {exception.Message}");
        }

        return Validate(dto);
    }

    /// <summary>
    /// Parses and validates a character pack manifest from a UTF-8 JSON stream.
    /// </summary>
    /// <param name="stream">The readable stream containing <c>manifest.json</c>.</param>
    /// <returns>A result containing the validated manifest or a failure reason.</returns>
    public static MyJiuMiPetPackLoadResult Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        ManifestDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize(stream, MyJiuMiPetPackJsonContext.Default.ManifestDto);
        }
        catch (JsonException exception)
        {
            return MyJiuMiPetPackLoadResult.Failure($"Manifest JSON is invalid: {exception.Message}");
        }

        return Validate(dto);
    }

    private static MyJiuMiPetPackLoadResult Validate(ManifestDto? dto)
    {
        if (dto is null)
        {
            return MyJiuMiPetPackLoadResult.Failure("Manifest root object is null.");
        }

        if (dto.SchemaVersion != SupportedSchemaVersion)
        {
            string actual = dto.SchemaVersion?.ToString(CultureInfo.InvariantCulture) ?? "missing";
            return MyJiuMiPetPackLoadResult.Failure(
                $"Unsupported manifest schemaVersion '{actual}'; expected {SupportedSchemaVersion}.");
        }

        if (string.IsNullOrWhiteSpace(dto.Id))
        {
            return MyJiuMiPetPackLoadResult.Failure("Manifest 'id' is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return MyJiuMiPetPackLoadResult.Failure("Manifest 'name' is required.");
        }

        if (dto.Canvas is not { Width: > 0, Height: > 0 })
        {
            return MyJiuMiPetPackLoadResult.Failure("Manifest 'canvas' must specify positive width and height.");
        }

        var canvas = new MyJiuMiPetPackCanvas(dto.Canvas.Width.Value, dto.Canvas.Height.Value);
        if (!TryValidateAnchor(dto.DefaultAnchor, canvas, out MyJiuMiPetPackAnchor defaultAnchor, out string? anchorError))
        {
            return MyJiuMiPetPackLoadResult.Failure($"Manifest 'defaultAnchor' is invalid: {anchorError}");
        }

        if (dto.Clips is null || dto.Clips.Count == 0)
        {
            return MyJiuMiPetPackLoadResult.Failure("Manifest 'clips' must contain at least the core animation clips.");
        }

        if (!TryValidateClipDictionary(
                dto.Clips,
                sectionName: "clips",
                canvas,
                defaultAnchor,
                out Dictionary<string, MyJiuMiPetPackClip>? clips,
                out string? clipsError))
        {
            return MyJiuMiPetPackLoadResult.Failure(clipsError);
        }

        foreach (MyJiuMiState state in Enum.GetValues<MyJiuMiState>())
        {
            string requiredKey = MyJiuMiPetPackManifest.GetClipKey(state);
            if (!clips.ContainsKey(requiredKey))
            {
                return MyJiuMiPetPackLoadResult.Failure($"Manifest is missing required core clip '{requiredKey}'.");
            }
        }

        IReadOnlyDictionary<string, MyJiuMiPetPackClip> interactions = _emptyClips;
        if (dto.Interactions is { Count: > 0 })
        {
            if (!TryValidateClipDictionary(
                    dto.Interactions,
                    sectionName: "interactions",
                    canvas,
                    defaultAnchor,
                    out Dictionary<string, MyJiuMiPetPackClip>? parsedInteractions,
                    out string? interactionsError))
            {
                return MyJiuMiPetPackLoadResult.Failure(interactionsError);
            }

            interactions = new ReadOnlyDictionary<string, MyJiuMiPetPackClip>(parsedInteractions);
        }

        IReadOnlyDictionary<string, string> files = _emptyFiles;
        if (dto.Files is { Count: > 0 })
        {
            var parsedFiles = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach ((string path, string? hash) in dto.Files)
            {
                if (!TryNormalizeRelativePath(path, out string normalizedPath))
                {
                    return MyJiuMiPetPackLoadResult.Failure($"Manifest 'files' contains an invalid relative path '{path}'.");
                }

                if (string.IsNullOrWhiteSpace(hash))
                {
                    return MyJiuMiPetPackLoadResult.Failure($"Manifest 'files' entry '{normalizedPath}' has an empty checksum.");
                }

                parsedFiles[normalizedPath] = hash.Trim();
            }

            files = new ReadOnlyDictionary<string, string>(parsedFiles);
        }

        var manifest = new MyJiuMiPetPackManifest(
            SupportedSchemaVersion,
            dto.Id.Trim(),
            dto.Name.Trim(),
            canvas,
            defaultAnchor,
            new ReadOnlyDictionary<string, MyJiuMiPetPackClip>(clips),
            interactions,
            files);

        return MyJiuMiPetPackLoadResult.Success(manifest);
    }

    private static bool TryValidateClipDictionary(
        Dictionary<string, ClipDto> source,
        string sectionName,
        MyJiuMiPetPackCanvas canvas,
        MyJiuMiPetPackAnchor packDefaultAnchor,
        [NotNullWhen(true)] out Dictionary<string, MyJiuMiPetPackClip>? result,
        [NotNullWhen(false)] out string? error)
    {
        var parsed = new Dictionary<string, MyJiuMiPetPackClip>(StringComparer.Ordinal);
        foreach ((string key, ClipDto? clipDto) in source)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                result = null;
                error = $"Manifest '{sectionName}' contains an empty key.";
                return false;
            }

            string displayName = string.Equals(sectionName, "clips", StringComparison.Ordinal)
                ? key
                : $"{sectionName}.{key}";
            if (!TryValidateClip(
                    key,
                    displayName,
                    clipDto,
                    canvas,
                    packDefaultAnchor,
                    out MyJiuMiPetPackClip? clip,
                    out string? clipError))
            {
                result = null;
                error = clipError;
                return false;
            }

            parsed[key] = clip;
        }

        result = parsed;
        error = null;
        return true;
    }

    private static bool TryValidateClip(
        string key,
        string displayName,
        ClipDto? dto,
        MyJiuMiPetPackCanvas canvas,
        MyJiuMiPetPackAnchor packDefaultAnchor,
        [NotNullWhen(true)] out MyJiuMiPetPackClip? clip,
        [NotNullWhen(false)] out string? error)
    {
        clip = null;
        if (dto is null)
        {
            error = $"Clip '{displayName}' is null.";
            return false;
        }

        if (dto.Loop is not { } loops)
        {
            error = $"Clip '{displayName}' must specify 'loop'.";
            return false;
        }

        MyJiuMiPetPackAnchor clipAnchor = packDefaultAnchor;
        if (dto.Anchor is not null &&
            !TryValidateAnchor(dto.Anchor, canvas, out clipAnchor, out string? clipAnchorError))
        {
            error = $"Clip '{displayName}' has an invalid anchor: {clipAnchorError}";
            return false;
        }

        if (dto.Frames is null || dto.Frames.Count == 0)
        {
            error = $"Clip '{displayName}' must contain at least one frame.";
            return false;
        }

        var frames = new List<MyJiuMiPetPackFrame>(dto.Frames.Count);
        for (int index = 0; index < dto.Frames.Count; index++)
        {
            FrameDto? frameDto = dto.Frames[index];
            if (frameDto is null)
            {
                error = $"Clip '{displayName}' frame {index} is null.";
                return false;
            }

            if (!TryNormalizeRelativePath(frameDto.Path, out string normalizedPath))
            {
                error = $"Clip '{displayName}' frame {index} has an invalid relative path '{frameDto.Path}'.";
                return false;
            }

            if (frameDto.DurationMs is not > 0)
            {
                error = $"Clip '{displayName}' frame {index} must have a positive 'durationMs'.";
                return false;
            }

            MyJiuMiPetPackAnchor frameAnchor = clipAnchor;
            if (frameDto.Anchor is not null &&
                !TryValidateAnchor(frameDto.Anchor, canvas, out frameAnchor, out string? frameAnchorError))
            {
                error = $"Clip '{displayName}' frame {index} has an invalid anchor: {frameAnchorError}";
                return false;
            }

            frames.Add(new MyJiuMiPetPackFrame(
                normalizedPath,
                TimeSpan.FromMilliseconds(frameDto.DurationMs.Value),
                frameAnchor));
        }

        clip = new MyJiuMiPetPackClip(key, frames.AsReadOnly(), loops, clipAnchor);
        error = null;
        return true;
    }

    private static bool TryValidateAnchor(
        AnchorDto? dto,
        MyJiuMiPetPackCanvas canvas,
        out MyJiuMiPetPackAnchor anchor,
        [NotNullWhen(false)] out string? error)
    {
        anchor = default;
        if (dto is not { X: { } x, Y: { } y })
        {
            error = "both 'x' and 'y' are required.";
            return false;
        }

        if (!double.IsFinite(x) || !double.IsFinite(y))
        {
            error = "coordinates must be finite numbers.";
            return false;
        }

        if (x < 0 || x > canvas.Width || y < 0 || y > canvas.Height)
        {
            error = string.Create(
                CultureInfo.InvariantCulture,
                $"coordinates ({x}, {y}) lie outside canvas {canvas.Width}x{canvas.Height}.");
            return false;
        }

        anchor = new MyJiuMiPetPackAnchor(x, y);
        error = null;
        return true;
    }

    private static bool TryNormalizeRelativePath(string? rawPath, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return false;
        }

        string candidate = rawPath.Trim().Replace('\\', '/');
        if (candidate.StartsWith('/') ||
            candidate.EndsWith('/') ||
            candidate.Contains(':', StringComparison.Ordinal))
        {
            return false;
        }

        string[] segments = candidate.Split('/');
        foreach (string segment in segments)
        {
            if (string.IsNullOrEmpty(segment) ||
                segment is "." or "..")
            {
                return false;
            }
        }

        normalized = candidate;
        return true;
    }

    internal sealed class ManifestDto
    {
        public int? SchemaVersion
        {
            get; set;
        }

        public string? Id
        {
            get; set;
        }

        public string? Name
        {
            get; set;
        }

        public CanvasDto? Canvas
        {
            get; set;
        }

        public AnchorDto? DefaultAnchor
        {
            get; set;
        }

        public Dictionary<string, ClipDto>? Clips
        {
            get; set;
        }

        public Dictionary<string, ClipDto>? Interactions
        {
            get; set;
        }

        public Dictionary<string, string>? Files
        {
            get; set;
        }
    }

    internal sealed class CanvasDto
    {
        public int? Width
        {
            get; set;
        }

        public int? Height
        {
            get; set;
        }
    }

    internal sealed class AnchorDto
    {
        public double? X
        {
            get; set;
        }

        public double? Y
        {
            get; set;
        }
    }

    internal sealed class ClipDto
    {
        public bool? Loop
        {
            get; set;
        }

        public AnchorDto? Anchor
        {
            get; set;
        }

        public List<FrameDto>? Frames
        {
            get; set;
        }
    }

    internal sealed class FrameDto
    {
        public string? Path
        {
            get; set;
        }

        public int? DurationMs
        {
            get; set;
        }

        public AnchorDto? Anchor
        {
            get; set;
        }
    }
}

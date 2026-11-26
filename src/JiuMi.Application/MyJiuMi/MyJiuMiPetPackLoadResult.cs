namespace JiuMi.Application.MyJiuMi;

/// <summary>
/// The outcome of parsing and validating a character pack manifest.
/// </summary>
/// <param name="Manifest">The validated manifest when loading succeeded; otherwise <see langword="null"/>.</param>
/// <param name="Error">A diagnostic reason when loading failed; otherwise <see langword="null"/>.</param>
public sealed record MyJiuMiPetPackLoadResult(
    MyJiuMiPetPackManifest? Manifest,
    string? Error)
{
    /// <summary>
    /// Gets a value indicating whether the manifest parsed and passed validation.
    /// </summary>
    public bool IsSuccess => Manifest is not null && Error is null;

    /// <summary>
    /// Creates a successful load result.
    /// </summary>
    /// <param name="manifest">The validated manifest.</param>
    /// <returns>A successful result carrying <paramref name="manifest"/>.</returns>
    public static MyJiuMiPetPackLoadResult Success(MyJiuMiPetPackManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return new MyJiuMiPetPackLoadResult(manifest, null);
    }

    /// <summary>
    /// Creates a failed load result.
    /// </summary>
    /// <param name="error">The validation or deserialization reason.</param>
    /// <returns>A failed result carrying <paramref name="error"/>.</returns>
    public static MyJiuMiPetPackLoadResult Failure(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return new MyJiuMiPetPackLoadResult(null, error);
    }
}

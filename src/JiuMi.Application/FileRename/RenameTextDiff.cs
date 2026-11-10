namespace JiuMi.Application.FileRename;

/// <summary>
/// The three pieces of a new name around the changed part. The preview highlights
/// <see cref="Changed"/> so a case-only change in the extension is still visible.
/// </summary>
/// <param name="Prefix">The unchanged start shared by both names.</param>
/// <param name="Changed">The middle that differs.</param>
/// <param name="Suffix">The unchanged end shared by both names.</param>
public sealed record RenameTextDiff(string Prefix, string Changed, string Suffix)
{
    /// <summary>
    /// Computes the longest common prefix and suffix of two names.
    /// </summary>
    /// <param name="original">The original name.</param>
    /// <param name="target">The new name.</param>
    /// <returns>The diff segments.</returns>
    public static RenameTextDiff Compute(string original, string target)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(target);

        int prefix = 0;
        while (prefix < original.Length &&
            prefix < target.Length &&
            original[prefix] == target[prefix])
        {
            prefix++;
        }

        int suffix = 0;
        while (suffix < original.Length - prefix &&
            suffix < target.Length - prefix &&
            original[^(suffix + 1)] == target[^(suffix + 1)])
        {
            suffix++;
        }

        return new RenameTextDiff(
            target[..prefix],
            target[prefix..(target.Length - suffix)],
            target[(target.Length - suffix)..]);
    }
}

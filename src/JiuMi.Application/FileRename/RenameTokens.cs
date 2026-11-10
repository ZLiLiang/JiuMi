using System.Globalization;
using System.Text;

namespace JiuMi.Application.FileRename;

/// <summary>
/// Expands the tokens a rename template or replacement may contain. Unknown tokens stay literal.
/// </summary>
public static class RenameTokens
{
    /// <summary>
    /// Expands a whole-name template.
    /// </summary>
    /// <param name="template">The template text.</param>
    /// <param name="stem">The original name without its extension.</param>
    /// <param name="extension">The original extension, including its dot.</param>
    /// <param name="index">The item number, from one.</param>
    /// <param name="count">The number of items in the batch.</param>
    /// <param name="today">The date substituted for <c>{date}</c>.</param>
    /// <returns>The expanded name.</returns>
    public static string ExpandTemplate(
        string template,
        string stem,
        string extension,
        int index,
        int count,
        DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(stem);
        ArgumentNullException.ThrowIfNull(extension);

        var result = new StringBuilder(template.Length + 16);
        int position = 0;
        while (position < template.Length)
        {
            int end = template[position] == '{' ? template.IndexOf('}', position + 1) : -1;
            if (end < 0)
            {
                result.Append(template[position]);
                position++;
                continue;
            }

            ReadOnlySpan<char> token = template.AsSpan(position + 1, end - position - 1);
            if (token.SequenceEqual("name"))
            {
                result.Append(stem);
            }
            else if (token.SequenceEqual("ext"))
            {
                result.Append(extension);
            }
            else if (token.SequenceEqual("date"))
            {
                result.Append(today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            }
            else if (!TryExpandIndex(token, index, count, result))
            {
                result.Append(template, position, end - position + 1);
            }

            position = end + 1;
        }

        return result.ToString();
    }

    /// <summary>
    /// Expands the index tokens of a replacement string. Other braces stay literal.
    /// </summary>
    /// <param name="replacement">The replacement text.</param>
    /// <param name="index">The item number, from one.</param>
    /// <param name="count">The number of items in the batch.</param>
    /// <returns>The expanded replacement.</returns>
    public static string ExpandIndex(string replacement, int index, int count)
    {
        ArgumentNullException.ThrowIfNull(replacement);

        var result = new StringBuilder(replacement.Length + 8);
        int position = 0;
        while (position < replacement.Length)
        {
            int end = replacement[position] == '{' ? replacement.IndexOf('}', position + 1) : -1;
            if (end < 0 ||
                !TryExpandIndex(replacement.AsSpan(position + 1, end - position - 1), index, count, result))
            {
                result.Append(replacement[position]);
                position++;
                continue;
            }

            position = end + 1;
        }

        return result.ToString();
    }

    private static bool TryExpandIndex(
        ReadOnlySpan<char> token,
        int index,
        int count,
        StringBuilder result)
    {
        if (token.SequenceEqual("index"))
        {
            result.Append(index.ToString(CultureInfo.InvariantCulture));
            return true;
        }

        if (token.Length < 7 ||
            !token[..6].SequenceEqual("index:") ||
            token[6] != '0')
        {
            return false;
        }

        foreach (char character in token[6..])
        {
            if (character != '0')
            {
                return false;
            }
        }

        int width = Math.Max(token.Length - 6, count.ToString(CultureInfo.InvariantCulture).Length);
        result.Append(index.ToString("D" + width.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture));
        return true;
    }

    /// <summary>
    /// Builds the index token of a requested zero-pad width.
    /// </summary>
    /// <param name="width">The minimum digits, or zero for no padding.</param>
    /// <returns>The token text.</returns>
    public static string IndexToken(int width) =>
        width > 0 ? "{index:" + new string('0', width) + "}" : "{index}";
}

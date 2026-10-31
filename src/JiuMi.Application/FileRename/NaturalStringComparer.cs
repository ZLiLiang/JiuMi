namespace JiuMi.Application.FileRename;

/// <summary>
/// Orders names the way people read them: letters ignore case and digit runs compare by value.
/// Equal names are still ordered deterministically so a plan never depends on enumeration order.
/// </summary>
public sealed class NaturalStringComparer : IComparer<string>
{
    /// <summary>Gets the shared comparer.</summary>
    public static NaturalStringComparer Instance { get; } = new();

    /// <inheritdoc />
    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }

        if (y is null)
        {
            return 1;
        }

        int left = 0;
        int right = 0;
        while (left < x.Length && right < y.Length)
        {
            if (char.IsAsciiDigit(x[left]) && char.IsAsciiDigit(y[right]))
            {
                int leftStart = left;
                int rightStart = right;
                while (left < x.Length && char.IsAsciiDigit(x[left]))
                {
                    left++;
                }

                while (right < y.Length && char.IsAsciiDigit(y[right]))
                {
                    right++;
                }

                ReadOnlySpan<char> leftNumber = x.AsSpan(leftStart, left - leftStart).TrimStart('0');
                ReadOnlySpan<char> rightNumber = y.AsSpan(rightStart, right - rightStart).TrimStart('0');
                int lengthOrder = leftNumber.Length.CompareTo(rightNumber.Length);
                if (lengthOrder != 0)
                {
                    return lengthOrder;
                }

                int valueOrder = leftNumber.SequenceCompareTo(rightNumber);
                if (valueOrder != 0)
                {
                    return valueOrder;
                }

                continue;
            }

            char leftCharacter = char.ToUpperInvariant(x[left]);
            char rightCharacter = char.ToUpperInvariant(y[right]);
            if (leftCharacter != rightCharacter)
            {
                return leftCharacter.CompareTo(rightCharacter);
            }

            left++;
            right++;
        }

        int remainingOrder = (x.Length - left).CompareTo(y.Length - right);
        return remainingOrder != 0 ? remainingOrder : string.CompareOrdinal(x, y);
    }
}

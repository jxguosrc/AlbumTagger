namespace AlbumTagger.Core.Files;

public sealed class NaturalStringComparer : IComparer<string?>
{
    public static NaturalStringComparer OrdinalIgnoreCase { get; } = new();

    private NaturalStringComparer()
    {
    }

    public int Compare(string? left, string? right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left is null)
        {
            return -1;
        }

        if (right is null)
        {
            return 1;
        }

        var leftIndex = 0;
        var rightIndex = 0;

        while (leftIndex < left.Length && rightIndex < right.Length)
        {
            if (IsAsciiDigit(left[leftIndex]) && IsAsciiDigit(right[rightIndex]))
            {
                var numericComparison = CompareNumberRuns(left, ref leftIndex, right, ref rightIndex);
                if (numericComparison != 0)
                {
                    return numericComparison;
                }

                continue;
            }

            var leftCharacter = char.ToUpperInvariant(left[leftIndex]);
            var rightCharacter = char.ToUpperInvariant(right[rightIndex]);
            var characterComparison = leftCharacter.CompareTo(rightCharacter);
            if (characterComparison != 0)
            {
                return characterComparison;
            }

            leftIndex++;
            rightIndex++;
        }

        return (left.Length - leftIndex).CompareTo(right.Length - rightIndex);
    }

    private static int CompareNumberRuns(
        string left,
        ref int leftIndex,
        string right,
        ref int rightIndex)
    {
        var leftRunStart = leftIndex;
        var rightRunStart = rightIndex;

        while (leftIndex < left.Length && IsAsciiDigit(left[leftIndex]))
        {
            leftIndex++;
        }

        while (rightIndex < right.Length && IsAsciiDigit(right[rightIndex]))
        {
            rightIndex++;
        }

        var leftSignificant = leftRunStart;
        var rightSignificant = rightRunStart;

        while (leftSignificant < leftIndex - 1 && left[leftSignificant] == '0')
        {
            leftSignificant++;
        }

        while (rightSignificant < rightIndex - 1 && right[rightSignificant] == '0')
        {
            rightSignificant++;
        }

        var leftSignificantLength = leftIndex - leftSignificant;
        var rightSignificantLength = rightIndex - rightSignificant;
        if (leftSignificantLength != rightSignificantLength)
        {
            return leftSignificantLength.CompareTo(rightSignificantLength);
        }

        for (var index = 0; index < leftSignificantLength; index++)
        {
            var digitComparison = left[leftSignificant + index].CompareTo(right[rightSignificant + index]);
            if (digitComparison != 0)
            {
                return digitComparison;
            }
        }

        var leftRunLength = leftIndex - leftRunStart;
        var rightRunLength = rightIndex - rightRunStart;
        return leftRunLength.CompareTo(rightRunLength);
    }

    private static bool IsAsciiDigit(char value) => value is >= '0' and <= '9';
}

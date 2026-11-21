using System.Text.RegularExpressions;

namespace Nova.Desktop;

internal static class ResearchRepetitionGuard
{
    private const int MinimumRepeatedTokenCount = 12;
    private const int MinimumRepetitions = 4;
    private const int MaximumPatternLength = 8;

    private static readonly Regex Words = new(@"[\p{L}\p{N}]+", RegexOptions.Compiled);

    public static bool HasRepeatedSequence(string text) =>
        FindRepeatedSequenceStart(text) is not null;

    public static int? FindRepeatedSequenceStart(string text)
    {
        var matches = Words.Matches(text);
        for (var start = 0; start < matches.Count - MinimumRepeatedTokenCount + 1; start++)
        {
            var maximumPatternLength = Math.Min(
                MaximumPatternLength, (matches.Count - start) / MinimumRepetitions);
            for (var patternLength = 1; patternLength <= maximumPatternLength; patternLength++)
            {
                var repetitions = 1;
                while (start + (repetitions + 1) * patternLength <= matches.Count
                       && RepeatsPattern(matches, start, patternLength, repetitions))
                {
                    repetitions++;
                }

                if (repetitions >= MinimumRepetitions
                    && repetitions * patternLength >= MinimumRepeatedTokenCount)
                    return matches[start].Index;
            }
        }

        return null;
    }

    private static bool RepeatsPattern(
        MatchCollection matches, int start, int patternLength, int repetitions)
    {
        var candidateStart = start + repetitions * patternLength;
        for (var offset = 0; offset < patternLength; offset++)
        {
            if (!string.Equals(
                    matches[start + offset].Value,
                    matches[candidateStart + offset].Value,
                    StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }
}

// SPDX-License-Identifier: Elastic-2.0
using System.CommandLine;

namespace GauntletCI.Cli.Utilities;

/// <summary>
/// Provides fuzzy matching for command-line suggestions ("did you mean?").
/// Uses Levenshtein distance to find the closest matching command or option.
/// </summary>
public static class CommandSuggester
{
    /// <summary>
    /// Calculate Levenshtein distance between two strings.
    /// This measures the minimum number of single-character edits needed to transform one string into another.
    /// </summary>
    public static int LevenshteinDistance(string source, string target)
    {
        if (source.Length == 0) return target.Length;
        if (target.Length == 0) return source.Length;

        var matrix = new int[source.Length + 1, target.Length + 1];

        for (int i = 0; i <= source.Length; i++) matrix[i, 0] = i;
        for (int j = 0; j <= target.Length; j++) matrix[0, j] = j;

        for (int i = 1; i <= source.Length; i++)
        {
            for (int j = 1; j <= target.Length; j++)
            {
                int cost = source[i - 1] == target[j - 1] ? 0 : 1;
                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost
                );
            }
        }

        return matrix[source.Length, target.Length];
    }

    /// <summary>
    /// Find the closest match from a list of candidates.
    /// Returns null if no match is sufficiently close (within threshold).
    /// </summary>
    public static string? FindClosestMatch(string input, IEnumerable<string> candidates, int maxDistance = 2)
    {
        var inputLower = input.ToLowerInvariant();
        var normalizedCandidates = candidates
            .Select(c => c.ToLowerInvariant())
            .Distinct()
            .ToList();

        var distances = normalizedCandidates
            .Select(c => (candidate: c, distance: LevenshteinDistance(inputLower, c)))
            .OrderBy(x => x.distance)
            .ToList();

        var bestMatch = distances.FirstOrDefault();
        return bestMatch.distance <= maxDistance ? bestMatch.candidate : null;
    }

    /// <summary>
    /// Get all close matches (sorted by similarity).
    /// Useful for suggesting multiple alternatives.
    /// </summary>
    public static IReadOnlyList<string> FindCloseMatches(string input, IEnumerable<string> candidates, int maxDistance = 2, int maxSuggestions = 3)
    {
        var inputLower = input.ToLowerInvariant();
        var normalizedCandidates = candidates
            .Select(c => c.ToLowerInvariant())
            .Distinct()
            .ToList();

        return normalizedCandidates
            .Select(c => (candidate: c, distance: LevenshteinDistance(inputLower, c)))
            .Where(x => x.distance <= maxDistance && x.distance > 0)
            .OrderBy(x => x.distance)
            .Take(maxSuggestions)
            .Select(x => x.candidate)
            .ToList();
    }

    /// <summary>
    /// Format a helpful error message with suggestions.
    /// </summary>
    public static string FormatSuggestionMessage(string invalidInput, IReadOnlyList<string> suggestions)
    {
        if (suggestions.Count == 0)
            return $"Unknown option or command: {invalidInput}";

        if (suggestions.Count == 1)
            return $"Unknown option or command: {invalidInput}\nDid you mean: {suggestions[0]}?";

        var suggestionList = string.Join("\n  ", suggestions);
        return $"Unknown option or command: {invalidInput}\nDid you mean one of these?\n  {suggestionList}";
    }
}

// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Corpus.Labeling;

/// <summary>
/// Normalizes raw Ollama URL input (CLI flags or config) into a clean, deduplicated list.
/// </summary>
public static class OllamaUrlNormalizer
{
    /// <summary>
    /// Splits comma-separated entries, trims whitespace and trailing slashes, removes blanks,
    /// and deduplicates case-insensitively. Returns an empty list if input is null or all blank.
    /// </summary>
    public static IReadOnlyList<string> Normalize(IEnumerable<string>? rawUrls)
    {
        return (rawUrls ?? [])
            .SelectMany(url => (url ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => url.Trim().TrimEnd('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

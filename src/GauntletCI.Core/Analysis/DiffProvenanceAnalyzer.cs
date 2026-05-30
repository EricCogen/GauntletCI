// SPDX-License-Identifier: Elastic-2.0
using System.Text.RegularExpressions;
using GauntletCI.Core.Diff;

namespace GauntletCI.Core.Analysis;

/// <summary>
/// Classifies added diff lines as net-new vs relocated by matching normalized content against removed lines (PG-PROVENANCE).
/// </summary>
public static class DiffProvenanceAnalyzer
{
    private static readonly Regex WhitespaceCollapse = new(@"\s+", RegexOptions.Compiled);

    /// <summary>Lookup of added line locations classified as relocated code.</summary>
    public sealed class Index
    {
        private readonly HashSet<(string File, int Line)> _relocatedAddedLines;

        internal Index(HashSet<(string File, int Line)> relocatedAddedLines)
        {
            _relocatedAddedLines = relocatedAddedLines;
        }

        /// <summary>Returns true when the added line at <paramref name="filePath"/>:<paramref name="line"/> matches a removed line.</summary>
        public bool IsRelocated(string? filePath, int line)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            return _relocatedAddedLines.Contains((NormalizePath(filePath), line));
        }
    }

    /// <summary>Builds a provenance index for all added lines in <paramref name="diff"/>.</summary>
    public static Index Build(DiffContext diff)
    {
        ArgumentNullException.ThrowIfNull(diff);

        var removedBuckets = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var file in diff.Files)
        {
            foreach (var line in file.RemovedLines)
            {
                var normalized = NormalizeLine(line.Content);
                if (normalized.Length == 0)
                    continue;

                removedBuckets.TryGetValue(normalized, out var count);
                removedBuckets[normalized] = count + 1;
            }
        }

        var relocated = new HashSet<(string File, int Line)>();
        foreach (var file in diff.Files)
        {
            var filePath = NormalizePath(file.NewPath);
            foreach (var line in file.AddedLines)
            {
                var normalized = NormalizeLine(line.Content);
                if (normalized.Length == 0)
                    continue;

                if (!removedBuckets.TryGetValue(normalized, out var remaining) || remaining <= 0)
                    continue;

                removedBuckets[normalized] = remaining - 1;
                relocated.Add((filePath, line.LineNumber));
            }
        }

        return new Index(relocated);
    }

    internal static string NormalizeLine(string content)
    {
        var trimmed = content.Trim();
        if (trimmed.Length == 0)
            return string.Empty;

        var commentIndex = trimmed.IndexOf("//", StringComparison.Ordinal);
        if (commentIndex >= 0)
            trimmed = trimmed[..commentIndex].TrimEnd();

        return WhitespaceCollapse.Replace(trimmed, " ");
    }

    private static string NormalizePath(string path) =>
        path.Replace('\\', '/').TrimStart('/');
}

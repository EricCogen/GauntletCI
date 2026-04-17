// SPDX-License-Identifier: Elastic-2.0
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace GauntletCI.Core.Configuration;

/// <summary>
/// Reads .gauntletci-ignore from the repo root and filters findings.
/// Each line is either:
///   GCI0003                    -- suppress rule for all files
///   GCI0003:src/Generated/**   -- suppress rule for matching paths only
///   # comment line             -- ignored
/// </summary>
public class IgnoreList
{
    private readonly List<(string RuleId, string? PathGlob)> _entries = [];

    private IgnoreList() { }

    public static IgnoreList Load(string repoPath)
    {
        var list = new IgnoreList();
        var path = Path.Combine(repoPath, ".gauntletci-ignore");
        if (!File.Exists(path)) return list;

        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith('#')) continue;

            var parts = line.Split(':', 2);
            var ruleId = parts[0].Trim().ToUpperInvariant();
            var glob = parts.Length > 1 ? parts[1].Trim() : null;
            list._entries.Add((ruleId, glob));
        }

        return list;
    }

    /// <summary>
    /// Returns true if the finding should be suppressed based on the ignore list.
    /// </summary>
    public bool IsSuppressed(string ruleId, string? filePath = null)
    {
        foreach (var (id, glob) in _entries)
        {
            if (!id.Equals(ruleId, StringComparison.OrdinalIgnoreCase)) continue;
            if (glob is null) return true;
            if (filePath is null) continue;
            if (GlobMatches(glob, filePath)) return true;
        }
        return false;
    }

    public bool IsEmpty => _entries.Count == 0;

    /// <summary>Appends a suppression rule to the .gauntletci-ignore file.</summary>
    public static void Append(string repoPath, string ruleId, string? pathGlob = null)
    {
        var path = Path.Combine(repoPath, ".gauntletci-ignore");
        var entry = pathGlob is not null ? $"{ruleId}:{pathGlob}" : ruleId;
        File.AppendAllText(path, $"{entry}{Environment.NewLine}");
    }

    private static readonly ConcurrentDictionary<string, Regex> _globCache = new();

    private static bool GlobMatches(string pattern, string input)
    {
        // Normalize separators
        pattern = pattern.Replace('\\', '/');
        input = input.Replace('\\', '/');

        var regex = _globCache.GetOrAdd(pattern, p =>
        {
            // Convert glob to regex: ** → .*, * → [^/]*, ? → [^/]
            var regexStr = "^" + Regex.Escape(p)
                .Replace(@"\*\*", ".*")
                .Replace(@"\*", "[^/]*")
                .Replace(@"\?", "[^/]") + "$";
            return new Regex(regexStr, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        });

        return regex.IsMatch(input);
    }
}

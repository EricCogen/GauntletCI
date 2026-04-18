// SPDX-License-Identifier: Elastic-2.0
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using GauntletCI.Core.Model;

namespace GauntletCI.Cli.Baseline;

/// <summary>
/// Reads and writes <c>.gauntletci-baseline.json</c> and computes stable finding fingerprints.
/// A fingerprint is a SHA-256 hash of the rule ID, file path, and the first 100 characters of
/// the evidence string — stable enough to survive minor text reflow but sensitive to actual
/// code changes.
/// </summary>
public static class BaselineStore
{
    private const string FileName = ".gauntletci-baseline.json";

    /// <summary>Returns the absolute path to the baseline file for the given repo root.</summary>
    public static string GetPath(string repoRoot) => Path.Combine(repoRoot, FileName);

    // Strips a leading "Line N: " prefix from evidence so fingerprints survive line-number shifts
    private static readonly Regex LineNumberPrefixRegex = new(
        @"^Line \d+:\s*", RegexOptions.Compiled);

    /// <summary>Computes a stable hex fingerprint for a single finding.</summary>
    public static string ComputeFingerprint(Finding f)
    {
        // Normalize evidence: remove "Line N: " prefix so fingerprints survive minor line-number
        // shifts (e.g. after unrelated edits that reflow hunk positions)
        var evidence = LineNumberPrefixRegex.Replace(f.Evidence, string.Empty);
        var excerpt = evidence.Length > 100 ? evidence[..100] : evidence;
        var raw = $"{f.RuleId}|{f.FilePath ?? ""}|{excerpt}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Loads the baseline file, or returns <c>null</c> if none exists.</summary>
    public static BaselineFile? Load(string repoRoot)
    {
        var path = GetPath(repoRoot);
        if (!File.Exists(path)) return null;

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;

        var version    = root.GetProperty("version").GetInt32();
        var createdAt  = root.GetProperty("createdAt").GetDateTimeOffset();
        string? commit = root.TryGetProperty("commit", out var c) ? c.GetString() : null;
        var fingerprints = root.GetProperty("fingerprints")
            .EnumerateArray()
            .Select(e => e.GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        return new BaselineFile(version, createdAt, commit, fingerprints);
    }

    /// <summary>Writes (or overwrites) the baseline file with the supplied fingerprints.</summary>
    public static void Save(string repoRoot, IEnumerable<string> fingerprints, string? commit = null)
    {
        var payload = new
        {
            version      = 1,
            createdAt    = DateTimeOffset.UtcNow,
            commit,
            fingerprints = fingerprints.Distinct().OrderBy(s => s).ToArray(),
        };
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(GetPath(repoRoot), json);
    }

    /// <summary>Deletes the baseline file. Returns <c>true</c> if a file was deleted.</summary>
    public static bool Clear(string repoRoot)
    {
        var path = GetPath(repoRoot);
        if (!File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }
}

/// <summary>In-memory representation of a loaded baseline file.</summary>
public sealed record BaselineFile(
    int Version,
    DateTimeOffset CreatedAt,
    string? Commit,
    IReadOnlySet<string> Fingerprints);

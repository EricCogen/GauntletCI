// SPDX-License-Identifier: Elastic-2.0
using System.Text.Json;
using System.Text.Json.Serialization;
using GauntletCI.Corpus.Interfaces;
using GauntletCI.Corpus.Models;
using GauntletCI.Corpus.Storage;

namespace GauntletCI.Corpus.Labeling;

/// <summary>
/// Infers preliminary <see cref="ExpectedFinding"/> labels from diff content using
/// pattern-matching heuristics. Gold labels (human-reviewed) always take precedence.
/// </summary>
public sealed class SilverLabelEngine
{
    private readonly IFixtureStore _store;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public SilverLabelEngine(IFixtureStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Infers heuristic <see cref="ExpectedFinding"/> labels from the given diff text.
    /// </summary>
    public Task<IReadOnlyList<ExpectedFinding>> InferLabelsAsync(
        string fixtureId, string diffText, CancellationToken ct = default)
    {
        var addedLines = ExtractAddedLines(diffText);
        var pathLines  = ExtractPathLines(diffText);

        var labels = new List<ExpectedFinding>();

        // GCI0016 — Sync-over-async (.Result / .Wait())
        if (addedLines.Any(l => l.Contains(".Result", StringComparison.Ordinal)
                             || l.Contains(".Wait()", StringComparison.Ordinal)))
        {
            labels.Add(new ExpectedFinding
            {
                RuleId             = "GCI0016",
                ShouldTrigger      = true,
                ExpectedConfidence = 0.6,
                Reason             = "Diff contains .Result or .Wait() on added lines (sync-over-async)",
                LabelSource        = LabelSource.Heuristic,
                IsInconclusive     = false,
            });
        }

        // GCI0007 — Secret/credential exposure
        if (addedLines.Any(l =>
                l.Contains("password",  StringComparison.OrdinalIgnoreCase) ||
                l.Contains("secret",    StringComparison.OrdinalIgnoreCase) ||
                l.Contains("api_key",   StringComparison.OrdinalIgnoreCase) ||
                l.Contains("token",     StringComparison.OrdinalIgnoreCase)))
        {
            labels.Add(new ExpectedFinding
            {
                RuleId             = "GCI0007",
                ShouldTrigger      = true,
                ExpectedConfidence = 0.7,
                Reason             = "Diff contains credential-related keyword on added lines",
                LabelSource        = LabelSource.Heuristic,
                IsInconclusive     = false,
            });
        }

        // GCI0003 — Empty catch block
        if (HasEmptyCatch(addedLines))
        {
            labels.Add(new ExpectedFinding
            {
                RuleId             = "GCI0003",
                ShouldTrigger      = true,
                ExpectedConfidence = 0.65,
                Reason             = "Diff contains empty or comment-only catch block on added lines",
                LabelSource        = LabelSource.Heuristic,
                IsInconclusive     = false,
            });
        }

        // GCI0021 — Migration file touched
        if (pathLines.Any(l =>
                l.Contains("Migration",  StringComparison.OrdinalIgnoreCase) ||
                l.Contains("_migration", StringComparison.OrdinalIgnoreCase)))
        {
            labels.Add(new ExpectedFinding
            {
                RuleId             = "GCI0021",
                ShouldTrigger      = true,
                ExpectedConfidence = 0.6,
                Reason             = "Diff touches a migration file (path contains 'Migration' or '_migration')",
                LabelSource        = LabelSource.Heuristic,
                IsInconclusive     = false,
            });
        }

        // Fallback: if no heuristic matched for a given rule, emit inconclusive label
        // (not emitted here — the spec only emits inconclusive when none of the above apply globally)
        if (labels.Count == 0)
        {
            labels.Add(new ExpectedFinding
            {
                RuleId             = "GCI0000",
                ShouldTrigger      = false,
                ExpectedConfidence = 0.0,
                Reason             = "No heuristic patterns matched this diff",
                LabelSource        = LabelSource.Heuristic,
                IsInconclusive     = true,
            });
        }

        return Task.FromResult<IReadOnlyList<ExpectedFinding>>(labels);
    }

    /// <summary>
    /// Applies inferred heuristic labels to a fixture's <c>expected.json</c>.
    /// Existing HumanReview or Seed labels are never overwritten unless
    /// <paramref name="overwriteExisting"/> is <c>true</c>.
    /// </summary>
    public async Task ApplyToFixtureAsync(
        string fixtureId, string diffText, bool overwriteExisting = false, CancellationToken ct = default)
    {
        var inferred    = await InferLabelsAsync(fixtureId, diffText, ct);
        var existing    = await ReadExistingLabelsAsync(fixtureId, ct);
        var merged      = MergeLabels(existing, inferred, overwriteExisting);
        await _store.SaveExpectedFindingsAsync(fixtureId, merged, ct);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static List<string> ExtractAddedLines(string diffText)
    {
        var lines = diffText.Split('\n');
        return lines
            .Where(l => l.StartsWith('+') && !l.StartsWith("+++"))
            .Select(l => l[1..]) // strip leading '+'
            .ToList();
    }

    private static List<string> ExtractPathLines(string diffText)
    {
        var lines = diffText.Split('\n');
        return lines
            .Where(l => l.StartsWith("--- ") || l.StartsWith("+++ ") || l.StartsWith("diff --git"))
            .ToList();
    }

    private static bool HasEmptyCatch(List<string> addedLines)
    {
        // Look for a 'catch' line followed by an empty or comment-only body.
        // We join added lines and look for the pattern.
        var joined = string.Join("\n", addedLines);

        // Inline empty catch: catch (...) { }  or  catch { }
        if (System.Text.RegularExpressions.Regex.IsMatch(joined, @"catch\s*(\([^)]*\))?\s*\{\s*\}"))
            return true;

        // Multi-line: catch line, then only whitespace/comment lines, then closing brace
        for (int i = 0; i < addedLines.Count; i++)
        {
            if (!addedLines[i].TrimStart().StartsWith("catch", StringComparison.OrdinalIgnoreCase)) continue;

            // Scan ahead for opening brace and content
            bool inBlock = false;
            bool hasNonCommentContent = false;

            for (int j = i; j < addedLines.Count && j < i + 10; j++)
            {
                var trimmed = addedLines[j].Trim();
                if (trimmed.Contains('{')) inBlock = true;
                if (!inBlock) continue;

                if (trimmed == "{" || trimmed == "}" || trimmed == "") continue;
                if (trimmed.StartsWith("//") || trimmed.StartsWith("/*") || trimmed.StartsWith("*")) continue;

                hasNonCommentContent = true;
                break;
            }

            if (inBlock && !hasNonCommentContent) return true;
        }

        return false;
    }

    private async Task<IReadOnlyList<ExpectedFinding>> ReadExistingLabelsAsync(string fixtureId, CancellationToken ct)
    {
        var metadata = await _store.GetMetadataAsync(fixtureId, ct);
        if (metadata is null) return [];

        string fixturePath;
        if (_store is FixtureFolderStore ffs)
            fixturePath = FixtureIdHelper.GetFixturePath(ffs.BasePath, metadata.Tier, fixtureId);
        else
            return [];

        var expectedPath = Path.Combine(fixturePath, "expected.json");
        if (!File.Exists(expectedPath)) return [];

        var json = await File.ReadAllTextAsync(expectedPath, ct);
        return JsonSerializer.Deserialize<List<ExpectedFinding>>(json, JsonOpts) ?? [];
    }

    private static IReadOnlyList<ExpectedFinding> MergeLabels(
        IReadOnlyList<ExpectedFinding> existing,
        IReadOnlyList<ExpectedFinding> inferred,
        bool overwriteExisting)
    {
        var merged = existing.ToDictionary(f => f.RuleId, f => f);

        foreach (var label in inferred)
        {
            if (merged.TryGetValue(label.RuleId, out var existing_))
            {
                // Never overwrite HumanReview or Seed unless explicitly requested
                if (!overwriteExisting &&
                    (existing_.LabelSource == LabelSource.HumanReview ||
                     existing_.LabelSource == LabelSource.Seed))
                {
                    continue;
                }
            }
            merged[label.RuleId] = label;
        }

        return merged.Values.ToList();
    }
}

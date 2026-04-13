// SPDX-License-Identifier: Elastic-2.0
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using GauntletCI.Corpus.Interfaces;
using GauntletCI.Corpus.Models;
using GauntletCI.Corpus.Storage;

namespace GauntletCI.Corpus.Labeling;

/// <summary>
/// Infers preliminary <see cref="ExpectedFinding"/> labels from diff content and review
/// comments using pattern-matching heuristics. Gold labels (human-reviewed) always take precedence.
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

    // Review comment keyword -> (ruleId, reason, confidence) mapping
    private static readonly (string[] Keywords, string RuleId, string Reason, double Confidence)[] CommentRules =
    [
        (["needs tests", "add test", "missing test", "no test"],
            "GCI0005", "Review comment requests tests", 0.7),
        (["null", "can this be null", "null reference", "nullreferenceexception", "nullable"],
            "GCI0006", "Review comment mentions null/nullable concern", 0.6),
        (["breaking change", "backwards compat", "backward compat", "semver", "api break"],
            "GCI0004", "Review comment mentions breaking change", 0.65),
        ([".result", ".wait()", "async", "blocking", "deadlock", "configureawait"],
            "GCI0016", "Review comment mentions async/blocking concern", 0.65),
        (["hardcoded", "hard-coded", "magic string", "magic number", "config", "environment variable"],
            "GCI0010", "Review comment mentions hardcoded value or configuration concern", 0.6),
        (["exception", "catch", "swallowing", "ignored exception"],
            "GCI0003", "Review comment mentions exception handling concern", 0.6),
        (["thread safe", "thread-safe", "race condition", "concurrent", "lock"],
            "GCI0016", "Review comment mentions thread safety concern", 0.6),
        (["secret", "password", "credential", "api key", "api_key", "token"],
            "GCI0007", "Review comment mentions credential/secret concern", 0.75),
        (["large file", "file size", "binary file", "binary blob"],
            "GCI0022", "Review comment mentions large or binary file", 0.6),
        (["migration", "schema change", "db migration", "database migration"],
            "GCI0021", "Review comment mentions migration concern", 0.65),
        (["rename", "too many files", "broad change", "sweeping change"],
            "GCI0023", "Review comment mentions broad/sweeping change", 0.55),
    ];

    public SilverLabelEngine(IFixtureStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Infers heuristic labels from diff text alone.
    /// </summary>
    public Task<IReadOnlyList<ExpectedFinding>> InferLabelsAsync(
        string fixtureId, string diffText, CancellationToken ct = default)
    {
        var addedLines = ExtractAddedLines(diffText);
        var pathLines  = ExtractPathLines(diffText);
        var labels     = new List<ExpectedFinding>();

        ApplyDiffHeuristics(addedLines, pathLines, labels);

        return Task.FromResult<IReadOnlyList<ExpectedFinding>>(labels);
    }

    /// <summary>
    /// Infers heuristic labels from review comment JSON (raw/review-comments.json content).
    /// </summary>
    public Task<IReadOnlyList<ExpectedFinding>> InferLabelsFromCommentsAsync(
        string reviewCommentsJson, CancellationToken ct = default)
    {
        var labels = new List<ExpectedFinding>();

        try
        {
            using var doc   = JsonDocument.Parse(reviewCommentsJson);
            var commentBodies = ExtractCommentBodies(doc.RootElement);
            ApplyCommentHeuristics(commentBodies, labels);
        }
        catch (JsonException)
        {
            // Malformed JSON -- skip comment scanning
        }

        return Task.FromResult<IReadOnlyList<ExpectedFinding>>(labels);
    }

    /// <summary>
    /// Applies inferred heuristic labels to a fixture's <c>expected.json</c>, scanning both
    /// the diff and any available review comments. Existing HumanReview or Seed labels are
    /// never overwritten unless <paramref name="overwriteExisting"/> is <c>true</c>.
    /// </summary>
    public async Task ApplyToFixtureAsync(
        string fixtureId, string diffText, bool overwriteExisting = false, CancellationToken ct = default)
    {
        var inferred = (await InferLabelsAsync(fixtureId, diffText, ct)).ToList();

        // Also scan review comments if available
        var reviewCommentsJson = await TryReadReviewCommentsAsync(fixtureId, ct);
        if (reviewCommentsJson is not null)
        {
            var commentLabels = await InferLabelsFromCommentsAsync(reviewCommentsJson, ct);
            foreach (var label in commentLabels)
            {
                // Merge: comment label wins over diff label only if diff doesn't already have it
                if (!inferred.Any(l => l.RuleId == label.RuleId))
                    inferred.Add(label);
            }
        }

        var existing = await ReadExistingLabelsAsync(fixtureId, ct);
        var merged   = MergeLabels(existing, inferred, overwriteExisting);
        await _store.SaveExpectedFindingsAsync(fixtureId, merged, ct);
    }

    // -- Heuristic application -------------------------------------------------

    private static void ApplyDiffHeuristics(List<string> addedLines, List<string> pathLines, List<ExpectedFinding> labels)
    {
        // GCI0016 -- Sync-over-async (.Result / .Wait())
        if (addedLines.Any(l => l.Contains(".Result", StringComparison.Ordinal)
                             || l.Contains(".Wait()", StringComparison.Ordinal)))
        {
            labels.Add(MakeLabel("GCI0016", "Diff contains .Result or .Wait() on added lines (sync-over-async)", 0.6));
        }

        // GCI0007 -- Secret/credential exposure
        if (addedLines.Any(l =>
                l.Contains("password",  StringComparison.OrdinalIgnoreCase) ||
                l.Contains("secret",    StringComparison.OrdinalIgnoreCase) ||
                l.Contains("api_key",   StringComparison.OrdinalIgnoreCase) ||
                l.Contains("token",     StringComparison.OrdinalIgnoreCase)))
        {
            labels.Add(MakeLabel("GCI0007", "Diff contains credential-related keyword on added lines", 0.7));
        }

        // GCI0003 -- Empty catch block
        if (HasEmptyCatch(addedLines))
            labels.Add(MakeLabel("GCI0003", "Diff contains empty or comment-only catch block on added lines", 0.65));

        // GCI0021 -- Migration file touched
        if (pathLines.Any(l =>
                l.Contains("Migration",  StringComparison.OrdinalIgnoreCase) ||
                l.Contains("_migration", StringComparison.OrdinalIgnoreCase)))
        {
            labels.Add(MakeLabel("GCI0021", "Diff touches a migration file (path contains 'Migration' or '_migration')", 0.6));
        }

        // GCI0004 -- Breaking change signals (public API removed/renamed)
        if (addedLines.Any(l =>
                Regex.IsMatch(l, @"\[Obsolete", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(l, @"(public|protected)\s+(abstract|virtual|override)\s+\w+.*\(") && l.Contains("throw new NotImplemented")))
        {
            labels.Add(MakeLabel("GCI0004", "Diff contains [Obsolete] attribute or throws NotImplemented on public API", 0.55));
        }

        // GCI0005 -- Test file deleted or test count reduced
        if (pathLines.Any(l =>
                l.Contains("Test",  StringComparison.OrdinalIgnoreCase) ||
                l.Contains("Spec.",  StringComparison.OrdinalIgnoreCase) ||
                l.Contains(".test.", StringComparison.OrdinalIgnoreCase)))
        {
            var diffText = string.Join("\n", addedLines);
            if (pathLines.Any(l => l.StartsWith("---")))
                labels.Add(MakeLabel("GCI0005", "Diff modifies a test file -- possible test coverage reduction", 0.5));
        }

        // GCI0006 -- Possible null dereference (assignment without null check)
        if (addedLines.Any(l =>
                Regex.IsMatch(l, @"=\s*null\b") ||
                Regex.IsMatch(l, @"!\.\w+\(")))
        {
            labels.Add(MakeLabel("GCI0006", "Diff assigns null or uses null-forgiving operator on added lines", 0.5));
        }

        // GCI0010 -- Hardcoded configuration value
        if (addedLines.Any(l =>
                Regex.IsMatch(l, @"""https?://[^""]{10,}""") ||
                Regex.IsMatch(l, @"(port|host|url|endpoint)\s*=\s*""", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(l, @"(port|host|url|endpoint)\s*=\s*\d{2,}", RegexOptions.IgnoreCase)))
        {
            labels.Add(MakeLabel("GCI0010", "Diff contains hardcoded URL/host/port on added lines", 0.55));
        }

        // GCI0022 -- Large binary or generated file
        if (pathLines.Any(l =>
                l.Contains(".min.js",  StringComparison.OrdinalIgnoreCase) ||
                l.Contains(".bundle.", StringComparison.OrdinalIgnoreCase) ||
                l.Contains(".dll",     StringComparison.OrdinalIgnoreCase) ||
                l.Contains(".exe",     StringComparison.OrdinalIgnoreCase) ||
                l.Contains(".png",     StringComparison.OrdinalIgnoreCase) ||
                l.Contains(".jpg",     StringComparison.OrdinalIgnoreCase)))
        {
            labels.Add(MakeLabel("GCI0022", "Diff touches a binary or generated file", 0.65));
        }
    }

    private static void ApplyCommentHeuristics(IReadOnlyList<string> commentBodies, List<ExpectedFinding> labels)
    {
        var emitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (keywords, ruleId, reason, confidence) in CommentRules)
        {
            if (emitted.Contains(ruleId)) continue;

            bool matched = commentBodies.Any(body =>
                keywords.Any(kw => body.Contains(kw, StringComparison.OrdinalIgnoreCase)));

            if (matched)
            {
                labels.Add(MakeLabel(ruleId, $"[review comment] {reason}", confidence));
                emitted.Add(ruleId);
            }
        }
    }

    // -- Private helpers -------------------------------------------------------

    private static ExpectedFinding MakeLabel(string ruleId, string reason, double confidence) =>
        new()
        {
            RuleId             = ruleId,
            ShouldTrigger      = true,
            ExpectedConfidence = confidence,
            Reason             = reason,
            LabelSource        = LabelSource.Heuristic,
            IsInconclusive     = false,
        };

    private static List<string> ExtractAddedLines(string diffText)
    {
        return diffText.Split('\n')
            .Where(l => l.StartsWith('+') && !l.StartsWith("+++"))
            .Select(l => l[1..])
            .ToList();
    }

    private static List<string> ExtractPathLines(string diffText)
    {
        return diffText.Split('\n')
            .Where(l => l.StartsWith("--- ") || l.StartsWith("+++ ") || l.StartsWith("diff --git"))
            .ToList();
    }

    private static IReadOnlyList<string> ExtractCommentBodies(JsonElement root)
    {
        var bodies = new List<string>();

        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in root.EnumerateArray())
                if (el.TryGetProperty("body", out var body))
                    bodies.Add(body.GetString() ?? string.Empty);
        }
        else if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in root.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in prop.Value.EnumerateArray())
                        if (el.TryGetProperty("body", out var body))
                            bodies.Add(body.GetString() ?? string.Empty);
                }
            }
        }

        return bodies;
    }

    private static bool HasEmptyCatch(List<string> addedLines)
    {
        var joined = string.Join("\n", addedLines);

        if (Regex.IsMatch(joined, @"catch\s*(\([^)]*\))?\s*\{\s*\}"))
            return true;

        for (int i = 0; i < addedLines.Count; i++)
        {
            if (!addedLines[i].TrimStart().StartsWith("catch", StringComparison.OrdinalIgnoreCase)) continue;

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

    private async Task<string?> TryReadReviewCommentsAsync(string fixtureId, CancellationToken ct)
    {
        var metadata = await _store.GetMetadataAsync(fixtureId, ct);
        if (metadata is null || _store is not FixtureFolderStore ffs) return null;

        var path = Path.Combine(
            FixtureIdHelper.GetFixturePath(ffs.BasePath, metadata.Tier, fixtureId),
            "raw", "review-comments.json");

        if (!File.Exists(path)) return null;
        return await File.ReadAllTextAsync(path, ct);
    }

    private async Task<IReadOnlyList<ExpectedFinding>> ReadExistingLabelsAsync(string fixtureId, CancellationToken ct)
    {
        var metadata = await _store.GetMetadataAsync(fixtureId, ct);
        if (metadata is null) return [];

        if (_store is not FixtureFolderStore ffs) return [];

        var expectedPath = Path.Combine(
            FixtureIdHelper.GetFixturePath(ffs.BasePath, metadata.Tier, fixtureId),
            "expected.json");

        if (!File.Exists(expectedPath)) return [];

        var json = await File.ReadAllTextAsync(expectedPath, ct);
        return JsonSerializer.Deserialize<List<ExpectedFinding>>(json, JsonOpts) ?? [];
    }

    private static IReadOnlyList<ExpectedFinding> MergeLabels(
        IReadOnlyList<ExpectedFinding> existing,
        IEnumerable<ExpectedFinding> inferred,
        bool overwriteExisting)
    {
        var merged = existing.ToDictionary(f => f.RuleId, f => f);

        foreach (var label in inferred)
        {
            if (merged.TryGetValue(label.RuleId, out var existingLabel))
            {
                if (!overwriteExisting &&
                    (existingLabel.LabelSource == LabelSource.HumanReview ||
                     existingLabel.LabelSource == LabelSource.Seed))
                {
                    continue;
                }
            }
            merged[label.RuleId] = label;
        }

        return merged.Values.ToList();
    }
}

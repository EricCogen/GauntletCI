// SPDX-License-Identifier: Elastic-2.0
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using GauntletCI.Corpus.Interfaces;
using GauntletCI.Corpus.Models;

namespace GauntletCI.Corpus.Labeling;

/// <summary>
/// Infers preliminary <see cref="ExpectedFinding"/> labels from diff content and review
/// comments using pattern-matching heuristics. Gold labels (human-reviewed) always take precedence.
/// </summary>
/// <remarks>
/// For each rule that has a heuristic, the engine emits BOTH positive labels (heuristic matched →
/// ShouldTrigger = true) and negative labels (heuristic didn't match → ShouldTrigger = false).
/// Negative labels are emitted at lower confidence (0.4) to enable real precision computation.
/// Rules without any heuristic receive no label — precision/recall stays Unknown for those rules.
/// </remarks>
public sealed class SilverLabelEngine
{
    private readonly IFixtureStore _store;
    private readonly ILlmLabeler _llmLabeler;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// The set of rule IDs covered by at least one diff or comment heuristic.
    /// For fixtures processed by label-all, rules in this set always receive a label
    /// (either ShouldTrigger=true or ShouldTrigger=false).
    /// </summary>
    public static readonly IReadOnlySet<string> RulesWithHeuristics = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "GCI0003", "GCI0004", "GCI0005", "GCI0006", "GCI0010",
        "GCI0012", "GCI0016", "GCI0021", "GCI0022", "GCI0023",
        "GCI0024", "GCI0029", "GCI0032", "GCI0036", "GCI0038",
        "GCI0039", "GCI0041", "GCI0042", "GCI0043", "GCI0044",
        "GCI0045", "GCI0046", "GCI0047", "GCI0049",
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
        (["secret", "password", "credential", "api key", "api_key"],
            "GCI0012", "Review comment mentions credential/secret concern", 0.75),
        (["large file", "file size", "binary file", "binary blob"],
            "GCI0022", "Review comment mentions large or binary file", 0.6),
        (["migration", "schema change", "db migration", "database migration"],
            "GCI0021", "Review comment mentions migration concern", 0.65),
        (["rename", "too many files", "broad change", "sweeping change"],
            "GCI0023", "Review comment mentions broad/sweeping change", 0.55),

        // --- Rules added after initial corpus labeling ---

        (["dispose", "using statement", "memory leak", "resource leak", "not disposed", "idisposable", "undisposed"],
            "GCI0024", "Review comment mentions resource disposal concern", 0.65),

        (["pii", "personal data", "gdpr", "personally identifiable", "sensitive data", "user data in log", "privacy"],
            "GCI0029", "Review comment mentions PII or privacy in logging", 0.70),

        (["unhandled exception", "exception propagation", "missing catch", "throw without catch", "uncaught"],
            "GCI0032", "Review comment mentions uncaught or unhandled exception path", 0.60),

        (["[pure]", "side effect in getter", "getter has side effect", "mutation in getter", "pure method mutates"],
            "GCI0036", "Review comment mentions side effect in a pure context", 0.55),

        (["service locator", "captive dependency", "scoped in singleton", "getrequiredservice", "getservice", "di anti-pattern"],
            "GCI0038", "Review comment mentions DI anti-pattern or service locator", 0.65),

        (["httpclient", "http client factory", "socket exhaustion", "ihttpclientfactory", "timeout missing", "cancellation token missing"],
            "GCI0039", "Review comment mentions HttpClient or external service safety concern", 0.65),

        (["missing assertion", "skipped test", "test ignored", "[ignore]", "uninformative test name", "test quality"],
            "GCI0041", "Review comment mentions test quality or missing assertions", 0.60),

        (["todo", "fixme", "not implemented", "notimplementedexception", "stub left", "incomplete implementation"],
            "GCI0042", "Review comment mentions TODO/stub or incomplete implementation", 0.70),

        (["null forgiving", "pragma warning disable nullable", "nullable warning", "cs8600", "null safety", "null-forgiving operator"],
            "GCI0043", "Review comment mentions nullable or null-forgiving operator concern", 0.60),

        (["linq in loop", "allocation in loop", "performance hotpath", "hot path", "gc pressure", "memory pressure", "linq overhead"],
            "GCI0044", "Review comment mentions performance or LINQ in loop concern", 0.60),

        (["over-engineering", "unnecessary abstraction", "single use interface", "passive wrapper", "delegation wrapper", "yagni"],
            "GCI0045", "Review comment mentions over-engineering or unnecessary abstraction", 0.55),

        (["inconsistent naming", "missing async suffix", "sync async naming", "pattern inconsistency", "naming convention"],
            "GCI0046", "Review comment mentions naming pattern inconsistency", 0.55),

        (["contradictory name", "misleading method name", "method rename", "naming contradiction", "boolean naming inversion"],
            "GCI0047", "Review comment mentions contradictory or misleading naming contract", 0.55),

        (["float comparison", "floating point equality", "double equality", "use epsilon", "math.abs comparison", "floating-point"],
            "GCI0049", "Review comment mentions floating-point equality comparison concern", 0.65),
    ];

    /// <summary>
    /// Initializes the engine with the fixture store used to persist and read expected findings.
    /// </summary>
    /// <param name="store">The fixture store providing read/write access to fixture files.</param>
    /// <param name="llmLabeler">Optional LLM labeler for Tier 3 fallback; defaults to <see cref="NullLlmLabeler"/>.</param>
    public SilverLabelEngine(IFixtureStore store, ILlmLabeler? llmLabeler = null)
    {
        _store = store;
        _llmLabeler = llmLabeler ?? new NullLlmLabeler();
    }

    /// <summary>
    /// Infers heuristic labels from diff text alone.
    /// Returns both positive and negative labels for all rules with heuristics.
    /// </summary>
    public Task<IReadOnlyList<ExpectedFinding>> InferLabelsAsync(
        string fixtureId, string diffText, CancellationToken ct = default)
    {
        var addedLines   = ExtractAddedLines(diffText);
        var removedLines = ExtractRemovedLines(diffText);
        var pathLines    = ExtractPathLines(diffText);
        var labels       = new List<ExpectedFinding>();

        ApplyDiffHeuristics(addedLines, removedLines, pathLines, labels);

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
    /// After merging positive matches, emits ShouldTrigger=false for any covered rule that
    /// did not produce a positive signal, enabling real precision computation.
    /// </summary>
    /// <returns>Total number of labels written to <c>expected.json</c>.</returns>
    public async Task<int> ApplyToFixtureAsync(
        string fixtureId, string diffText, bool overwriteExisting = false, CancellationToken ct = default, Action<string>? log = null)
    {
        // ── Tier 1: Diff + comment heuristics ────────────────────────────────
        var inferred = (await InferLabelsAsync(fixtureId, diffText, ct)).ToList();

        IReadOnlyList<string> commentBodies = [];
        IReadOnlySet<string> commentPaths   = new HashSet<string>(StringComparer.Ordinal);

        var reviewCommentsJson = await _store.TryReadReviewCommentsAsync(fixtureId, ct);
        if (reviewCommentsJson is not null)
        {
            try
            {
                using var doc = JsonDocument.Parse(reviewCommentsJson);
                commentBodies = ExtractCommentBodies(doc.RootElement);
                commentPaths  = ExtractCommentPaths(doc.RootElement);

                var commentLabels = new List<ExpectedFinding>();
                ApplyCommentHeuristics(commentBodies, commentLabels);

                foreach (var label in commentLabels)
                {
                    var existing = inferred.FirstOrDefault(l => l.RuleId == label.RuleId);
                    if (existing is null)
                        inferred.Add(label);
                    else if (label.ExpectedConfidence > existing.ExpectedConfidence)
                    {
                        inferred.Remove(existing);
                        inferred.Add(label);
                    }
                }
            }
            catch (JsonException)
            {
                // Malformed JSON -- skip comment scanning
            }
        }

        // ── Tier 2: File-path correlation ─────────────────────────────────────
        var actualFindings = await _store.ReadActualFindingsAsync(fixtureId, ct);

        if (commentPaths.Count > 0)
        {
            var positiveRuleIdsTier12 = inferred
                .Where(l => l.ShouldTrigger)
                .Select(l => l.RuleId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var finding in actualFindings)
            {
                if (!finding.DidTrigger || finding.FilePath is null) continue;
                if (!RulesWithHeuristics.Contains(finding.RuleId)) continue;
                if (positiveRuleIdsTier12.Contains(finding.RuleId)) continue;

                var normalizedPath = finding.FilePath.Replace('\\', '/').ToLowerInvariant();
                if (commentPaths.Contains(normalizedPath))
                {
                    inferred.Add(new ExpectedFinding
                    {
                        RuleId             = finding.RuleId,
                        ShouldTrigger      = true,
                        ExpectedConfidence = 0.55,
                        Reason             = $"[file-path correlation] Reviewer commented on '{finding.FilePath}'",
                        LabelSource        = LabelSource.FilePathCorrelation,
                        IsInconclusive     = false,
                    });
                    positiveRuleIdsTier12.Add(finding.RuleId);
                }
            }
        }

        // ── Tier 3: LLM fallback for uncertain findings ───────────────────────
        var positiveRuleIdsAfterTier12 = inferred
            .Where(l => l.ShouldTrigger)
            .Select(l => l.RuleId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (_llmLabeler is not NullLlmLabeler)
        {
            foreach (var finding in actualFindings)
            {
                if (!finding.DidTrigger) continue;
                if (!RulesWithHeuristics.Contains(finding.RuleId)) continue;
                if (positiveRuleIdsAfterTier12.Contains(finding.RuleId)) continue;

                var diffSnippet = ExtractFileDiffHunk(diffText, finding.FilePath);
                (log ?? Console.WriteLine)($"  [llm] Tier 3 calling {_llmLabeler.GetType().Name} for rule {finding.RuleId}");

                var result = await _llmLabeler.ClassifyAsync(
                    finding.RuleId,
                    finding.Message,
                    finding.Evidence,
                    finding.FilePath,
                    commentBodies,
                    diffSnippet,
                    ct);

                if (result is not null && !result.IsInconclusive)
                {
                    inferred.Add(new ExpectedFinding
                    {
                        RuleId             = finding.RuleId,
                        ShouldTrigger      = result.ShouldTrigger,
                        ExpectedConfidence = result.Confidence,
                        Reason             = $"[llm] {result.Reason}",
                        LabelSource        = LabelSource.LlmReview,
                        IsInconclusive     = false,
                    });
                    positiveRuleIdsAfterTier12.Add(finding.RuleId);
                }
            }
        }

        // For every rule with a heuristic that did NOT produce a positive signal,
        // emit a negative label so FalsePositive detection works in scoring.
        var positiveRuleIdsFinal = inferred
            .Where(l => l.ShouldTrigger)
            .Select(l => l.RuleId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var ruleId in RulesWithHeuristics)
        {
            if (!positiveRuleIdsFinal.Contains(ruleId))
            {
                inferred.Add(MakeNegativeLabel(ruleId,
                    "Heuristic found no signal for this rule on diff or review comments", 0.40));
            }
        }

        var existingLabels = await _store.ReadExpectedFindingsAsync(fixtureId, ct);

        // When overwriting, strip stale heuristic labels for rules removed from RulesWithHeuristics.
        if (overwriteExisting)
            existingLabels = existingLabels
                .Where(l => l.LabelSource != LabelSource.Heuristic || RulesWithHeuristics.Contains(l.RuleId))
                .ToList();

        var merged = MergeLabels(existingLabels, inferred, overwriteExisting);
        await _store.SaveExpectedFindingsAsync(fixtureId, merged, ct);
        return merged.Count;
    }

    // -- Heuristic application -------------------------------------------------

    private static void ApplyDiffHeuristics(List<string> addedLines, List<string> removedLines, List<string> pathLines, List<ExpectedFinding> labels)
    {
        // GCI0016 -- Sync-over-async (.Result / .Wait())
        if (addedLines.Any(l => l.Contains(".Result", StringComparison.Ordinal)
                             || l.Contains(".Wait()", StringComparison.Ordinal)))
        {
            labels.Add(MakeLabel("GCI0016", "Diff contains .Result or .Wait() on added lines (sync-over-async)", 0.6));
        }

        // GCI0012 -- Secret/credential exposure
        // Tightened: exclude CancellationToken, require assignment to literal string value
        // to avoid flagging benign token/password parameter names.
        if (addedLines.Any(IsCredentialAssignment))
        {
            labels.Add(MakeLabel("GCI0012", "Diff contains credential keyword assigned to a literal string value on added lines", 0.7));
        }

        // GCI0003 -- Empty catch block
        if (HasEmptyCatch(addedLines))
            labels.Add(MakeLabel("GCI0003", "Diff contains empty or comment-only catch block on added lines", 0.65));

        // GCI0021 -- Migration file touched OR serialization attribute/enum member removed
        if (pathLines.Any(l =>
                l.Contains("Migration",  StringComparison.OrdinalIgnoreCase) ||
                l.Contains("_migration", StringComparison.OrdinalIgnoreCase)) ||
            removedLines.Any(l =>
            {
                var t = l.TrimStart();
                return t.StartsWith("[JsonProperty",   StringComparison.OrdinalIgnoreCase) ||
                       t.StartsWith("[JsonPropertyName", StringComparison.OrdinalIgnoreCase) ||
                       t.StartsWith("[DataMember",     StringComparison.OrdinalIgnoreCase) ||
                       t.StartsWith("[Column(",        StringComparison.OrdinalIgnoreCase) ||
                       t.StartsWith("[BsonElement",    StringComparison.OrdinalIgnoreCase) ||
                       t.StartsWith("[Key]",           StringComparison.OrdinalIgnoreCase) ||
                       t.StartsWith("[ForeignKey",     StringComparison.OrdinalIgnoreCase) ||
                       t.StartsWith("[Required]",      StringComparison.OrdinalIgnoreCase);
            }))
        {
            labels.Add(MakeLabel("GCI0021", "Diff removes a serialization attribute or touches a migration file", 0.55));
        }

        // GCI0004 -- Breaking change signals (public API removed/renamed)
        if (addedLines.Any(l =>
                Regex.IsMatch(l, @"\[Obsolete", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(l, @"(public|protected)\s+(abstract|virtual|override)\s+\w+.*\(") && l.Contains("throw new NotImplemented")))
        {
            labels.Add(MakeLabel("GCI0004", "Diff contains [Obsolete] attribute or throws NotImplemented on public API", 0.55));
        }

        // GCI0005 -- Test file DELETED (not merely modified)
        // Tightened: only trigger when a test file appears exclusively in removed-file lines (--- a/...),
        // not when tests are simply added or modified (which is good, not risky).
        {
            var removedPaths = pathLines
                .Where(l => l.StartsWith("--- a/") || l.StartsWith("--- ") && !l.StartsWith("--- /dev/null"))
                .ToList();
            var addedPaths = pathLines
                .Where(l => l.StartsWith("+++ b/") || l.StartsWith("+++ ") && !l.StartsWith("+++ /dev/null"))
                .ToList();

            bool testFileRemoved = removedPaths.Any(l =>
                l.Contains("Test",  StringComparison.OrdinalIgnoreCase) ||
                l.Contains("Spec.", StringComparison.OrdinalIgnoreCase) ||
                l.Contains(".test.", StringComparison.OrdinalIgnoreCase));

            bool testFileAdded = addedPaths.Any(l =>
                l.Contains("Test",  StringComparison.OrdinalIgnoreCase) ||
                l.Contains("Spec.", StringComparison.OrdinalIgnoreCase) ||
                l.Contains(".test.", StringComparison.OrdinalIgnoreCase));

            // Only flag if test files are being removed and no test files are being added in exchange
            if (testFileRemoved && !testFileAdded)
                labels.Add(MakeLabel("GCI0005", "Diff removes a test file with no corresponding new test file (coverage reduction)", 0.65));
        }

        // GCI0006 -- Possible null dereference
        // Tightened: require meaningful null assignment pattern — property/variable set to null, or
        // null-forgiving operator used in a non-trivial position. Skip bare declarations and
        // null-coalescing assignments which are safe patterns.
        if (addedLines.Any(IsMeaningfulNullPattern))
        {
            labels.Add(MakeLabel("GCI0006", "Diff contains a meaningful null assignment or null-forgiving use on added lines", 0.5));
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

        // GCI0023 -- Broad/sweeping rename (many files changed)
        // Heuristic: if the diff touches 10+ distinct path segments at the same level it may be a bulk rename
        var distinctDirs = pathLines
            .Where(l => l.StartsWith("+++ ") || l.StartsWith("--- "))
            .Select(l => Path.GetDirectoryName(l.Split(' ').Last())?.Replace('\\', '/') ?? "")
            .Where(d => !string.IsNullOrEmpty(d))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        if (distinctDirs >= 8)
            labels.Add(MakeLabel("GCI0023", $"Diff touches {distinctDirs} distinct directories — possible broad sweeping change", 0.5));

        // --- Rules added after initial corpus labeling ---

        // GCI0024 -- Resource allocated without disposal (no using/try-finally)
        if (addedLines.Any(l =>
                !l.TrimStart().StartsWith("//") &&
                !l.Contains("using ", StringComparison.Ordinal) &&
                (l.Contains("new FileStream(", StringComparison.Ordinal) ||
                 l.Contains("new StreamWriter(", StringComparison.Ordinal) ||
                 l.Contains("new StreamReader(", StringComparison.Ordinal) ||
                 l.Contains("new SqlConnection(", StringComparison.Ordinal) ||
                 l.Contains("new SqlCommand(", StringComparison.Ordinal) ||
                 l.Contains("new TcpClient(", StringComparison.Ordinal) ||
                 l.Contains("new GZipStream(", StringComparison.Ordinal) ||
                 Regex.IsMatch(l, @"=\s*new [A-Z]\w*(Stream|Reader|Writer|Connection|Timer|Transaction)\("))))
        {
            labels.Add(MakeLabel("GCI0024", "Diff allocates a disposable resource without a using statement on added lines", 0.55));
        }

        // GCI0029 -- PII term in a log call
        {
            var piiLogPrefixes = new[] { "_logger.", "logger.", "Logger.", "_log.", "log.", "Log.Information", "Log.Warning", "Log.Error", "Log.Debug" };
            var piiTerms       = new[] { "email", "ssn", "phonenumber", "creditcard", "dateofbirth", "passport", "bankaccount", "address", "ipaddress", "token", "username" };
            if (addedLines.Any(l =>
                    piiLogPrefixes.Any(p => l.Contains(p, StringComparison.Ordinal)) &&
                    piiTerms.Any(t => l.Contains(t, StringComparison.OrdinalIgnoreCase))))
            {
                labels.Add(MakeLabel("GCI0029", "Diff contains a PII term inside a log call on added lines", 0.65));
            }
        }

        // GCI0032 -- Non-guard throw new without test assertion coverage
        {
            var guardPrefixes = new[] { "throw new ArgumentNullException", "throw new ArgumentException", "throw new ArgumentOutOfRangeException", "throw new ObjectDisposedException", "throw new NotImplementedException" };
            bool hasRealThrow = addedLines.Any(l =>
                l.Contains("throw new", StringComparison.Ordinal) &&
                !guardPrefixes.Any(g => l.Contains(g, StringComparison.Ordinal)) &&
                !l.TrimStart().StartsWith("//"));
            if (hasRealThrow)
                labels.Add(MakeLabel("GCI0032", "Diff adds a throw new (non-guard) expression without test assertion coverage", 0.55));
        }

        // GCI0036 -- [Pure] attribute added, or mutation pattern inside getter
        if (addedLines.Any(l => l.Contains("[Pure]", StringComparison.Ordinal)))
            labels.Add(MakeLabel("GCI0036", "Diff adds a [Pure] attribute — mutation within this context would be flagged", 0.50));

        // GCI0038 -- DI anti-pattern: service locator or direct injectable instantiation
        {
            var diPatterns = new[] { ".GetService<", ".GetRequiredService<", "_serviceProvider.GetService", "_serviceProvider.GetRequiredService" };
            var newInjectableRegex = new Regex(@"=\s*new [A-Z][a-zA-Z]*(Service|Repository|Manager|Handler)\(", RegexOptions.Compiled);
            if (addedLines.Any(l =>
                    !l.TrimStart().StartsWith("//") &&
                    (diPatterns.Any(p => l.Contains(p, StringComparison.Ordinal)) ||
                     newInjectableRegex.IsMatch(l))))
            {
                labels.Add(MakeLabel("GCI0038", "Diff contains a service locator call or direct instantiation of an injectable type", 0.60));
            }
        }

        // GCI0039 -- Direct HttpClient instantiation (IHttpClientFactory bypass)
        if (addedLines.Any(l =>
                !l.TrimStart().StartsWith("//") &&
                l.Contains("new HttpClient(", StringComparison.Ordinal)))
        {
            labels.Add(MakeLabel("GCI0039", "Diff instantiates HttpClient directly, bypassing IHttpClientFactory", 0.65));
        }

        // GCI0041 -- Silenced tests in test files
        {
            var silenceTokens = new[] { "[Ignore", "[Skip", ".Skip(", "[Fact(Skip", "[Theory(Skip" };
            bool isInTestPath = pathLines.Any(l =>
                l.Contains("Test", StringComparison.OrdinalIgnoreCase) ||
                l.Contains("Spec", StringComparison.OrdinalIgnoreCase));
            if (isInTestPath && addedLines.Any(l =>
                    silenceTokens.Any(s => l.Contains(s, StringComparison.OrdinalIgnoreCase))))
            {
                labels.Add(MakeLabel("GCI0041", "Diff silences or skips a test in a test file", 0.65));
            }
        }

        // GCI0042 -- TODO/FIXME/HACK or NotImplementedException in non-comment lines
        if (addedLines.Any(l =>
                !l.TrimStart().StartsWith("///") &&
                !l.TrimStart().StartsWith("//") &&
                (l.Contains("TODO", StringComparison.OrdinalIgnoreCase) ||
                 l.Contains("FIXME", StringComparison.OrdinalIgnoreCase) ||
                 l.Contains("HACK", StringComparison.OrdinalIgnoreCase) ||
                 l.Contains("throw new NotImplementedException", StringComparison.Ordinal))))
        {
            labels.Add(MakeLabel("GCI0042", "Diff contains a TODO/FIXME/HACK marker or NotImplementedException stub", 0.70));
        }

        // GCI0043 -- Null-forgiving operator or nullable pragma disable
        {
            var nullableCodes = new[] { "nullable", "CS8600", "CS8601", "CS8602", "CS8603", "CS8604" };
            bool hasPragma = addedLines.Any(l =>
                l.Contains("#pragma warning disable", StringComparison.OrdinalIgnoreCase) &&
                nullableCodes.Any(c => l.Contains(c, StringComparison.OrdinalIgnoreCase)));
            bool hasNullForgiving = addedLines.Any(l =>
                !l.TrimStart().StartsWith("//") &&
                (l.Contains("!.", StringComparison.Ordinal) || l.Contains("!;", StringComparison.Ordinal) || l.Contains("!,", StringComparison.Ordinal)));
            if (hasPragma || hasNullForgiving)
                labels.Add(MakeLabel("GCI0043", "Diff disables nullable warnings or uses null-forgiving operator on added lines", 0.60));
        }

        // GCI0044 -- LINQ call inside a loop construct
        {
            var linqMethods = new[] { ".Where(", ".Select(", ".FirstOrDefault(", ".Any(", ".Count(" };
            var loopKeywords = new[] { "for (", "foreach (", "while (" };
            bool inLoop = false;
            foreach (var line in addedLines)
            {
                if (loopKeywords.Any(k => line.Contains(k, StringComparison.Ordinal))) inLoop = true;
                if (inLoop && linqMethods.Any(m => line.Contains(m, StringComparison.Ordinal)))
                {
                    labels.Add(MakeLabel("GCI0044", "Diff contains a LINQ call inside a loop construct on added lines", 0.50));
                    break;
                }
                if (line.TrimStart().StartsWith("}")) inLoop = false;
            }
        }

        // GCI0045 -- New interface definition (potential single-use interface)
        if (addedLines.Any(l => Regex.IsMatch(l, @"\binterface\s+I[A-Z]")))
            labels.Add(MakeLabel("GCI0045", "Diff adds a new interface definition (potential single-use abstraction)", 0.45));

        // GCI0046 -- Service locator pattern or mixed sync/async method names
        {
            var slPatterns = new[] { ".GetService<", ".GetRequiredService<", "ServiceLocator.Current" };
            if (addedLines.Any(l =>
                    !l.TrimStart().StartsWith("//") &&
                    slPatterns.Any(p => l.Contains(p, StringComparison.Ordinal))))
            {
                labels.Add(MakeLabel("GCI0046", "Diff uses service locator pattern on added lines", 0.55));
            }
        }

        // GCI0047 -- Contradictory CRUD verb rename (e.g. GetFoo renamed to DeleteFoo)
        // Uses actual removed lines from the diff to detect CRUD verbs in both old and new code.
        {
            var crudPattern = @"\b(Get|Set|Add|Remove|Delete|Create|Update|Find|Fetch|Load|Save|Insert)\w*\s*\(";
            var hasCrudAdded   = addedLines.Any(l => Regex.IsMatch(l, crudPattern));
            var hasCrudRemoved = removedLines.Any(l => Regex.IsMatch(l, crudPattern));
            if (hasCrudAdded && hasCrudRemoved)
                labels.Add(MakeLabel("GCI0047", "Diff renames CRUD-verb methods — potential naming contract contradiction", 0.45));
        }

        // GCI0049 -- Float/double equality comparison
        if (addedLines.Any(l =>
                !l.TrimStart().StartsWith("//") &&
                (Regex.IsMatch(l, @"(?:==|!=)\s*(?:[-+]?\d*\.\d+|\d+\.\d+)[fFdD]?\b") ||
                 Regex.IsMatch(l, @"\b(?:float|double)\b.*(?:==|!=)", RegexOptions.IgnoreCase))))
        {
            labels.Add(MakeLabel("GCI0049", "Diff contains floating-point equality comparison on added lines", 0.60));
        }
    }

    // -- Tightened GCI0006 helper ----------------------------------------------

    // Match: field/property/variable set to null (e.g. `_foo = null;`, `this.Bar = null;`)
    // but NOT: nullable type declarations (`string? foo = null`), null-coalescing (`??=`), or
    // conditional null checks (`if (x == null)`).
    private static readonly Regex MeaningfulNullAssign = new(
        @"(?<!\?)\b\w[\w\.]*\s*=\s*null\s*;",
        RegexOptions.Compiled);

    // Null-forgiving operator in non-trivial position (not just `!` on a cast/param check)
    private static readonly Regex NullForgivingNonTrivial = new(
        @"\w+!\.\w+\(",
        RegexOptions.Compiled);

    private static bool IsMeaningfulNullPattern(string line)
    {
        // Skip comments and null-checks
        var trimmed = line.TrimStart();
        if (trimmed.StartsWith("//") || trimmed.StartsWith("*") || trimmed.StartsWith("/*"))
            return false;
        if (trimmed.Contains("== null") || trimmed.Contains("!= null") || trimmed.Contains("?? "))
            return false;
        // Skip nullable declarations: `Type? name = null;`
        if (Regex.IsMatch(trimmed, @"\?\s+\w+\s*=\s*null\s*;"))
            return false;

        return MeaningfulNullAssign.IsMatch(line) || NullForgivingNonTrivial.IsMatch(line);
    }

    // -- Tightened GCI0007 helper ----------------------------------------------

    // Credential keyword assigned to a quoted string literal — the real risky pattern
    private static readonly Regex CredentialAssignToLiteral = new(
        @"(password|secret|api_key|apikey|private_key|privatekey|client_secret|access_token|auth_token)\s*[=:]\s*""[^""]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Hard-coded credential-looking value (base64 or long alphanumeric after = "")
    private static readonly Regex HardcodedCredentialValue = new(
        @"=\s*""[A-Za-z0-9+/]{20,}={0,2}""",
        RegexOptions.Compiled);

    private static bool IsCredentialAssignment(string line)
    {
        // Skip test/mock values and comments
        var trimmed = line.TrimStart();
        if (trimmed.StartsWith("//") || trimmed.StartsWith("*"))
            return false;
        // Skip obvious test placeholder strings
        if (line.Contains("fake", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("mock", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("test",  StringComparison.OrdinalIgnoreCase) ||
            line.Contains("dummy", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("placeholder", StringComparison.OrdinalIgnoreCase))
            return false;

        return CredentialAssignToLiteral.IsMatch(line) || HardcodedCredentialValue.IsMatch(line);
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

    private static ExpectedFinding MakeNegativeLabel(string ruleId, string reason, double confidence) =>
        new()
        {
            RuleId             = ruleId,
            ShouldTrigger      = false,
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

    private static List<string> ExtractRemovedLines(string diffText)
    {
        return diffText.Split('\n')
            .Where(l => l.StartsWith('-') && !l.StartsWith("---"))
            .Select(l => l[1..])
            .ToList();
    }

    private static List<string> ExtractPathLines(string diffText)
    {
        return diffText.Split('\n')
            .Where(l => l.StartsWith("--- ") || l.StartsWith("+++ ") || l.StartsWith("diff --git"))
            .ToList();
    }

    private static string ExtractFileDiffHunk(string diffText, string? filePath, int maxChars = 800)
    {
        if (string.IsNullOrEmpty(filePath))
            return diffText.Length > maxChars ? diffText[..maxChars] : diffText;

        var normalized = filePath.Replace('\\', '/');

        // Find the diff header for this specific file
        var searchTarget = $"diff --git a/{normalized}";
        var startIdx = diffText.IndexOf(searchTarget, StringComparison.OrdinalIgnoreCase);

        if (startIdx < 0)
        {
            // Try matching just the filename in case paths differ slightly
            var fileName = Path.GetFileName(normalized);
            var lines = diffText.Split('\n');
            var cumLen = 0;
            foreach (var line in lines)
            {
                if (line.StartsWith("diff --git", StringComparison.Ordinal)
                    && line.Contains(fileName, StringComparison.OrdinalIgnoreCase))
                {
                    startIdx = cumLen;
                    break;
                }
                cumLen += line.Length + 1; // +1 for newline
            }
        }

        if (startIdx < 0)
            return diffText.Length > maxChars ? diffText[..maxChars] : diffText;

        // Find the end of this file's section (next diff --git or end of string)
        var nextDiff = diffText.IndexOf("\ndiff --git ", startIdx + 10, StringComparison.Ordinal);
        var section  = nextDiff > 0 ? diffText[startIdx..nextDiff] : diffText[startIdx..];

        return section.Length > maxChars ? section[..maxChars] : section;
    }

    private static IReadOnlySet<string> ExtractCommentPaths(JsonElement root)
    {
        var paths = new HashSet<string>(StringComparer.Ordinal);

        void ExtractFromArray(JsonElement arr)
        {
            foreach (var el in arr.EnumerateArray())
                if (el.TryGetProperty("path", out var path))
                {
                    var p = path.GetString();
                    if (!string.IsNullOrEmpty(p))
                        paths.Add(p.Replace('\\', '/').ToLowerInvariant());
                }
        }

        if (root.ValueKind == JsonValueKind.Array)
            ExtractFromArray(root);
        else if (root.ValueKind == JsonValueKind.Object)
            foreach (var prop in root.EnumerateObject())
                if (prop.Value.ValueKind == JsonValueKind.Array)
                    ExtractFromArray(prop.Value);

        return paths;
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

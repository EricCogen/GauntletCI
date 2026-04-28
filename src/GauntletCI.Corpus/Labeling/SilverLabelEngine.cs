// SPDX-License-Identifier: Elastic-2.0
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using GauntletCI.Corpus.Interfaces;
using GauntletCI.Corpus.Models;
using GauntletCI.Corpus.Normalization;

namespace GauntletCI.Corpus.Labeling;

/// <summary>
/// Infers preliminary <see cref="ExpectedFinding"/> labels from diff content and review
/// comments using pattern-matching heuristics. Gold labels (human-reviewed) always take precedence.
/// </summary>
/// <remarks>
/// For each rule that has a heuristic, the engine emits BOTH positive labels (heuristic matched →
/// ShouldTrigger = true) and negative labels (heuristic didn't match → ShouldTrigger = false).
/// Negative labels are emitted at lower confidence (0.4) to enable real precision computation.
/// Rules without any heuristic receive no label: precision/recall stays Unknown for those rules.
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
        "GCI0003", "GCI0004", "GCI0006", "GCI0010",
        "GCI0012", "GCI0015", "GCI0016", "GCI0021", "GCI0022",
        "GCI0024", "GCI0029", "GCI0032", "GCI0035", "GCI0036", "GCI0038",
        "GCI0039", "GCI0041", "GCI0042", "GCI0043", "GCI0044",
        "GCI0045", "GCI0046", "GCI0047", "GCI0048", "GCI0049",
        "GCI0050", "GCI0053",
    };

    // Review comment keyword -> (ruleId, reason, confidence) mapping
    private static readonly (string[] Keywords, string RuleId, string Reason, double Confidence)[] CommentRules =
    [
        (["needs tests", "add test", "missing test", "no test", "untested"],
            "GCI0041", "Review comment requests tests or identifies missing test coverage", 0.65),
        (["null", "can this be null", "null reference", "nullreferenceexception", "nullable"],
            "GCI0006", "Review comment mentions null/nullable concern", 0.6),
        (["breaking change", "backwards compat", "backward compat", "semver", "api break"],
            "GCI0004", "Review comment mentions breaking change", 0.65),
        ([".result", ".wait()", "async", "blocking", "deadlock", "configureawait"],
            "GCI0016", "Review comment mentions async/blocking concern", 0.65),
        (["hardcoded", "hard-coded", "magic string", "magic number"],
            "GCI0010", "Review comment mentions hardcoded value or magic number/string", 0.6),
        (["exception", "catch", "swallowing", "ignored exception"],
            "GCI0032", "Review comment mentions exception handling concern", 0.6),
        // Note: "thread safe / concurrent / lock" keywords intentionally removed from GCI0016.
        // GCI0016 scope is async execution model violations only (dropped static mutable field
        // check). Thread-safety review comments signal concerns the rule no longer detects.
        (["secret", "password", "credential", "api key", "api_key"],
            "GCI0012", "Review comment mentions credential/secret concern", 0.75),
        (["idempotent", "idempotency", "idempotency key", "duplicate request", "retry safe", "insert duplicate", "upsert"],
            "GCI0022", "Review comment mentions idempotency, retry safety, or duplicate-insert concern", 0.65),
        (["migration", "schema change", "db migration", "database migration"],
            "GCI0021", "Review comment mentions migration concern", 0.65),
        (["contradictory method", "wrong method name", "naming inversion", "method semantics", "misleading name", "method name contradicts"],
            "GCI0047", "Review comment flags a contradictory or misleading method name", 0.65),

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

        (["mass assignment", "over-posting", "sql ignore", "on conflict do nothing", "input binding", "unchecked cast", "data integrity"],
            "GCI0015", "Review comment mentions mass assignment, SQL IGNORE pattern, or data integrity concern", 0.60),

        (["layer violation", "architecture violation", "cross layer", "wrong layer", "should not reference", "dependency inversion"],
            "GCI0035", "Review comment flags an architecture layer boundary violation", 0.65),

        (["insecure random", "use securerandom", "cryptographic rng", "system.random", "randomnumbergenerator", "not cryptographically"],
            "GCI0048", "Review comment flags use of System.Random in a security context", 0.70),

        (["sql truncation", "column too short", "string length", "max length", "nvarchar too small", "data truncation"],
            "GCI0050", "Review comment mentions SQL column truncation or short string column width", 0.60),

        (["lockfile only", "lockfile changed without", "no source change", "why is the lockfile", "bump without source"],
            "GCI0053", "Review comment mentions lockfile change without accompanying source changes", 0.55),
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
        var prodCsLines         = ExtractAddedLinesFromProductionCsFiles(diffText);
        var prodCsRemovedLines  = ExtractRemovedLinesFromProductionCsFiles(diffText);
        var labels       = new List<ExpectedFinding>();

        ApplyDiffHeuristics(addedLines, removedLines, pathLines, prodCsLines, prodCsRemovedLines, labels, diffText);

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

    private static void ApplyDiffHeuristics(List<string> addedLines, List<string> removedLines, List<string> pathLines, List<string> prodCsLines, List<string> prodCsRemovedLines, List<ExpectedFinding> labels, string rawDiff = "")
    {
        // GCI0016 -- Async execution model violations. Mirrors the four checks in the rule exactly.
        // .GetAwaiter().GetResult() is unambiguous; .Result only counts with Task/Async context;
        // .Wait() counts with Task/CancellationToken context; async void skips event-handler sigs;
        // lock(this) and Thread.Sleep in non-test paths are also flagged.
        {
            bool isTestPath = pathLines.Any(l =>
                l.Contains("Test", StringComparison.OrdinalIgnoreCase) ||
                l.Contains("Spec", StringComparison.OrdinalIgnoreCase));

            bool hasBlockingCall = addedLines.Any(l =>
                l.Contains(".GetAwaiter().GetResult()", StringComparison.Ordinal) ||
                l.Contains(".Wait()", StringComparison.Ordinal) ||
                (l.Contains(".Result", StringComparison.Ordinal) &&
                    (l.Contains("Task", StringComparison.Ordinal) ||
                     l.Contains("Async", StringComparison.Ordinal))) ||
                (l.Contains(".Wait(", StringComparison.Ordinal) &&
                    (l.Contains("Task", StringComparison.Ordinal) ||
                     l.Contains("CancellationToken", StringComparison.Ordinal))));

            bool hasAsyncVoid = addedLines.Any(l =>
                l.Contains("async void ", StringComparison.Ordinal) &&
                !l.Contains("EventArgs", StringComparison.Ordinal) &&
                !l.Contains("object sender", StringComparison.Ordinal));

            bool hasLockThis = addedLines.Any(l =>
                l.Contains("lock(this)", StringComparison.Ordinal) ||
                l.Contains("lock (this)", StringComparison.Ordinal));

            bool hasThreadSleep = !isTestPath && addedLines.Any(l =>
                l.Contains("Thread.Sleep(", StringComparison.Ordinal) &&
                !l.TrimStart().StartsWith("//"));

            if (hasBlockingCall || hasAsyncVoid || hasLockThis || hasThreadSleep)
                labels.Add(MakeLabel("GCI0016", "Diff contains async execution model violation (blocking call, async void, lock(this), or Thread.Sleep)", 0.65));
        }

        // GCI0012 -- Secret/credential exposure + weak cryptography + SQL injection
        // Use production-CS-only lines to avoid FNs from test helper passwords, sample
        // JWTs in test classes, and SHA/MD5 uses that are intentional in test code.
        // When the diff has no production C# files, no GCI0012 label is emitted: the
        // rule also only processes production .cs files.
        if (prodCsLines.Count > 0)
        {
            bool hasCredential = prodCsLines.Any(IsCredentialAssignment);

            bool hasWeakHash = prodCsLines.Any(l =>
                !l.TrimStart().StartsWith("//") &&
                (l.Contains("MD5.Create()", StringComparison.Ordinal) ||
                 l.Contains("SHA1.Create()", StringComparison.Ordinal) ||
                 l.Contains("new MD5CryptoServiceProvider(", StringComparison.Ordinal) ||
                 l.Contains("new SHA1Managed(", StringComparison.Ordinal) ||
                 l.Contains("new SHA1CryptoServiceProvider(", StringComparison.Ordinal)));

            bool hasSqlInjection = prodCsLines.Any(l =>
                !l.TrimStart().StartsWith("//") &&
                SqlStringLiteralStart.IsMatch(l) &&
                (l.Contains(" + ", StringComparison.Ordinal) ||
                 (l.Contains("{", StringComparison.Ordinal) && l.Contains("$\"", StringComparison.Ordinal)) ||
                 l.Contains("string.Format(", StringComparison.Ordinal)));

            if (hasCredential || hasWeakHash || hasSqlInjection)
            {
                var reason = hasWeakHash   ? "Diff adds use of weak hashing algorithm (MD5 or SHA1) in production code"
                           : hasSqlInjection ? "Diff builds SQL string via concatenation or interpolation in production code"
                           : "Diff contains credential keyword assigned to a literal string value on added production lines";
                labels.Add(MakeLabel("GCI0012", reason, 0.7));
            }
        }

        // GCI0003 -- Non-private method signature changed in production code
        // Fire when production .cs removes AND re-adds a public/protected/internal member
        // with a parenthesized signature: the rule's primary detection path.
        {
            static bool IsSigLine(string l)
            {
                var t = l.TrimStart();
                return (t.StartsWith("public ",    StringComparison.Ordinal) ||
                        t.StartsWith("protected ", StringComparison.Ordinal) ||
                        t.StartsWith("internal ",  StringComparison.Ordinal)) && t.Contains('(');
            }
            if (prodCsRemovedLines.Any(IsSigLine) && prodCsLines.Any(IsSigLine))
                labels.Add(MakeLabel("GCI0003", "Diff removes and re-adds a non-private method signature in production code", 0.60));
        }

        // GCI0032 -- Empty or comment-only catch block (exception swallowing)
        if (HasEmptyCatch(addedLines))
            labels.Add(MakeLabel("GCI0032", "Diff contains an empty or comment-only catch block on added lines", 0.65));

        // GCI0021 -- Serialization attribute removed from production CS, or EF migration schema operation removed
        // Migration detection: check if removed lines from a non-test EF migration .cs file contain actual
        // schema operations (migrationBuilder.Drop*, AlterColumn, etc.): not just any modification to files
        // in a migrations directory (that would match scaffolding/processor changes which are not schema risks).
        bool hasMigrationModified = false;
        if (pathLines.Any(l => l.StartsWith("--- a/", StringComparison.Ordinal) &&
                                IsEfMigrationCsFile(l[6..].TrimEnd('\r'))))
        {
            // Require that removed lines from migration files contain actual EF schema operations
            var migrationRemovedLines = prodCsRemovedLines;
            hasMigrationModified = migrationRemovedLines.Any(l =>
                l.Contains("migrationBuilder.Drop",  StringComparison.OrdinalIgnoreCase) ||
                l.Contains("migrationBuilder.Alter", StringComparison.OrdinalIgnoreCase) ||
                l.Contains("migrationBuilder.Rename", StringComparison.OrdinalIgnoreCase) ||
                l.Contains("migrationBuilder.Create", StringComparison.OrdinalIgnoreCase));
        }

        bool hasRemovedSerializationAttr = prodCsRemovedLines.Any(l =>
        {
            var t = l.TrimStart();
            return t.StartsWith("[JsonProperty",    StringComparison.OrdinalIgnoreCase) ||
                   t.StartsWith("[JsonPropertyName", StringComparison.OrdinalIgnoreCase) ||
                   t.StartsWith("[DataMember",      StringComparison.OrdinalIgnoreCase) ||
                   t.StartsWith("[Column(",         StringComparison.OrdinalIgnoreCase) ||
                   t.StartsWith("[BsonElement",     StringComparison.OrdinalIgnoreCase) ||
                   t.StartsWith("[ForeignKey",      StringComparison.OrdinalIgnoreCase);
        });

        if (hasRemovedSerializationAttr || hasMigrationModified)
        {
            labels.Add(MakeLabel("GCI0021", "Diff removes a serialization attribute from production C# or modifies an EF migration file", 0.60));
        }

        // GCI0004 -- Breaking change signals
        // Use production-CS-only lines to match the rule's file-scope filters (no test files, no .md docs).
        // Fire for [Obsolete] ADDED (active deprecation) OR [Obsolete] REMOVED (guard stripped).
        bool hasObsoleteAdded   = prodCsLines.Any(l => l.Contains("[Obsolete", StringComparison.OrdinalIgnoreCase));
        bool hasObsoleteRemoved = prodCsRemovedLines.Any(l => l.Contains("[Obsolete", StringComparison.OrdinalIgnoreCase));
        if (hasObsoleteAdded || hasObsoleteRemoved)
        {
            var reason = hasObsoleteAdded
                ? "Production C# adds [Obsolete] attribute (API being deprecated)"
                : "Production C# removes [Obsolete] attribute (deprecation guard stripped)";
            labels.Add(MakeLabel("GCI0004", reason, 0.60));
        }

        // GCI0006 -- Possible null dereference
        // Mirror the rule signal: unsafe .Value access on an added C# line without a null guard in
        // surrounding added lines, OR a public/protected non-constructor method with nullable params
        // lacking a validation statement in the following lines.
        {
            bool triggered = false;
            for (int i = 0; i < addedLines.Count && !triggered; i++)
            {
                var line = addedLines[i];
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("//") || trimmed.StartsWith("*") || trimmed.StartsWith("/*")) continue;

                // --- Signal 1: unsafe .Value access ---
                if (HasLabelerUnsafeValueAccess(line))
                {
                    // Skip: IOptions<T>.Value (DI pattern, always non-null)
                    if (line.Contains("IOptions", StringComparison.Ordinal)) continue;
                    // Skip: .Value = (LHS assignment, not reading)
                    if (Regex.IsMatch(line, @"\.Value\s*=(?!=|>)")) continue;
                    // Skip: KVP iteration (.Key word-bounded on same line)
                    if (HasLabelerDotKeyAccess(line)) continue;
                    // Check: no null guard in surrounding 10 added lines
                    int start = Math.Max(0, i - 5);
                    int end   = Math.Min(addedLines.Count, i + 5);
                    bool guarded = addedLines[start..end]
                        .Any(l => l.Contains("HasValue", StringComparison.Ordinal) ||
                                  l.Contains("is not null", StringComparison.Ordinal) ||
                                  Regex.IsMatch(l, @"\.Value\s*(==|!=|is)\s*null") ||
                                  (l.Contains(".Success", StringComparison.Ordinal) &&
                                   HasSharedRoot(line, l)));
                    if (!guarded) triggered = true;
                }

                // --- Signal 2: public/protected non-constructor method with nullable params missing guard ---
                if (!triggered &&
                    (trimmed.StartsWith("public ", StringComparison.Ordinal) ||
                     trimmed.StartsWith("protected ", StringComparison.Ordinal)) &&
                    trimmed.Contains('(') && trimmed.Contains(')') &&
                    Regex.IsMatch(line, @"\b(string|object)\?") &&
                    !IsLabelerConstructor(trimmed) &&
                    !trimmed.Contains("partial", StringComparison.Ordinal))
                {
                    int end = Math.Min(addedLines.Count, i + 10);
                    bool hasValidation = addedLines[i..end]
                        .Any(l => l.Contains("null", StringComparison.Ordinal) ||
                                  l.Contains("ArgumentNullException", StringComparison.Ordinal) ||
                                  l.Contains("ThrowIfNull", StringComparison.Ordinal) ||
                                  l.Contains("Guard.", StringComparison.Ordinal));
                    if (!hasValidation) triggered = true;
                }
            }
            if (triggered)
                labels.Add(MakeLabel("GCI0006", "Added C# code accesses .Value without a null guard or has unvalidated nullable parameters", 0.6));
        }

        // GCI0010 -- Hardcoded configuration value
        // Require localhost/IP URLs, connection strings, or port/host assignments to literals.
        // Broad public HTTPS URLs (documentation, CDN) are excluded to reduce noise.
        if (addedLines.Any(l =>
                !l.TrimStart().StartsWith("//") &&
                (Regex.IsMatch(l, @"""https?://(?:localhost|127\.0\.0\.1|\d+\.\d+\.\d+\.\d+)[:/]") ||
                 Regex.IsMatch(l, @"(connectionString|connStr|ConnectionString|DbConnection)\s*=\s*""", RegexOptions.IgnoreCase) ||
                 Regex.IsMatch(l, @"(port|host|endpoint)\s*=\s*\d{3,}", RegexOptions.IgnoreCase) ||
                 Regex.IsMatch(l, @"(port|host|endpoint)\s*=\s*""[^""]{4,}""", RegexOptions.IgnoreCase))))
        {
            labels.Add(MakeLabel("GCI0010", "Diff contains hardcoded localhost URL, connection string, or host/port literal", 0.6));
        }

        // GCI0022 -- Idempotency and retry safety
        // Fire when an HttpPost endpoint is added without idempotency signals,
        // OR when a raw INSERT INTO appears without an upsert guard.
        {
            var idempotencySignals = new[] { "IdempotencyKey", "Idempotency-Key", "idempotencyKey", "idempotent", "dedup", "Dedup", "RequestId", "requestId", "MessageId", "messageId" };
            var upsertPatterns     = new[] { "ON DUPLICATE KEY", "ON CONFLICT", "INSERT OR REPLACE", "INSERT OR IGNORE", "MERGE INTO", "UPSERT" };
            bool hasHttpPostAdded  = addedLines.Any(l =>
            {
                var t = l.Trim();
                return t.Equals("[HttpPost]", StringComparison.Ordinal) ||
                       t.StartsWith("[HttpPost(", StringComparison.Ordinal);
            });
            bool hasInsertWithoutUpsert = addedLines.Any(l =>
                l.Contains("INSERT INTO", StringComparison.Ordinal) &&
                !upsertPatterns.Any(p => l.Contains(p, StringComparison.OrdinalIgnoreCase)));
            if (hasHttpPostAdded || hasInsertWithoutUpsert)
            {
                bool hasIdempotencySignal = addedLines.Any(l =>
                    idempotencySignals.Any(sig => l.Contains(sig, StringComparison.OrdinalIgnoreCase)));
                if (!hasIdempotencySignal)
                    labels.Add(MakeLabel("GCI0022", "Diff adds an [HttpPost] endpoint or raw INSERT INTO without idempotency/upsert guard", 0.60));
            }
        }

        // --- Rules added after initial corpus labeling ---

        // GCI0024 -- Resource allocated without disposal (no using/try-finally)
        // Mirror the rule's detection logic: suffix heuristic + explicit types list, with the same
        // skip guards (caller-owns return, callee-owns method-arg, static singleton, lambda body).
        {
            var labeler0024Explicit = new[]
            {
                "new FileStream(", "new StreamWriter(", "new StreamReader(", "new MemoryStream(",
                "new SqlConnection(", "new SqlCommand(", "new SqlDataReader(",
                "new TcpClient(", "new UdpClient(", "new Socket(",
                "new Mutex(", "new Semaphore(", "new SemaphoreSlim(",
                "new EventWaitHandle(", "new ManualResetEvent(",
                "new BinaryWriter(", "new BinaryReader(",
                "new GZipStream(", "new DeflateStream(", "new CryptoStream(",
                "new X509Certificate(", "new RSACryptoServiceProvider(",
            };
            var labeler0024Suffixes = new[]
            {
                "Stream", "Reader", "Writer", "Connection", "Client",
                "Listener", "Channel", "Context", "Provider", "Session", "Transaction",
                "Certificate", "Scope", "Timer",
            };
            var labeler0024NonDisposable = new HashSet<string>(StringComparer.Ordinal)
            {
                "SyntaxContext", "AnalysisContext", "SemanticContext",
                "SyntaxNodeAnalysisContext", "OperationAnalysisContext", "CodeBlockAnalysisContext",
                "InvocationContext",
                "HttpContext", "RouteContext", "FilterContext", "ActionContext",
                "AuthorizationFilterContext", "ResourceExecutingContext", "ResourceExecutedContext",
                "ResultExecutingContext", "ResultExecutedContext", "ExceptionContext",
                "ValidationContext", "NavigationContext",
                "PropagationContext", "ActivityContext", "SpanContext",
                "MemberSelectionContext", "EquivalencyValidationContext", "CreatorPropertyContext",
                "StrategyBuilderContext", "SelectionContext",
                "SynchronizationContext", "DispatcherSynchronizationContext",
                "DispatcherQueueSynchronizationContext",
            };
            var labeler0024Rx = new Regex(@"new ([A-Z][A-Za-z0-9]+)\(", RegexOptions.Compiled);

            bool HasUnsafeDisposableAllocation(string l)
            {
                var trimmed = l.TrimStart();
                if (trimmed.StartsWith("//") || trimmed.StartsWith("*") || trimmed.StartsWith("/*"))
                    return false;
                if (l.Contains("using ", StringComparison.Ordinal)) return false;
                if (trimmed.StartsWith("return ", StringComparison.Ordinal)) return false;
                if (l.Contains("static ", StringComparison.Ordinal)) return false;
                // Lambda body: `(...) => new X(`: ownership transfers into the lambda's caller.
                int arrowIdx = l.IndexOf("=>", StringComparison.Ordinal);
                int newIdx   = l.IndexOf("new ", StringComparison.Ordinal);
                if (arrowIdx >= 0 && newIdx >= 0 && newIdx > arrowIdx) return false;
                // Fast path: explicit known types
                if (labeler0024Explicit.Any(t => l.Contains(t, StringComparison.Ordinal))) return true;
                // Suffix heuristic
                var m = labeler0024Rx.Match(l);
                if (!m.Success) return false;
                var typeName = m.Groups[1].Value;
                if (!labeler0024Suffixes.Any(s => typeName.EndsWith(s, StringComparison.Ordinal)))
                    return false;
                if (labeler0024NonDisposable.Contains(typeName)) return false;
                // Callee-owns skip: unmatched `(` before `new typeName` means arg in a method call
                int idx = l.IndexOf("new " + typeName, StringComparison.Ordinal);
                if (idx > 0)
                {
                    var before = l[..idx];
                    int opens  = before.Count(c => c == '(');
                    int closes = before.Count(c => c == ')');
                    if (opens > closes) return false;
                }
                return true;
            }

            if (prodCsLines.Any(HasUnsafeDisposableAllocation))
                labels.Add(MakeLabel("GCI0024", "Diff allocates a disposable resource without a using statement on added lines", 0.60));
        }

        // GCI0029 -- PII term in a log call
        {
            var piiLogPrefixes = new[] { "_logger.", "logger.", "Logger.", "_log.", "log.", "Log.Information", "Log.Warning", "Log.Error", "Log.Debug" };
            var piiTerms       = new[] { "email", "ssn", "phonenumber", "creditcard", "dateofbirth", "passport", "bankaccount", "nationalid", "taxid", "dob", "birthdate", "zipcode", "postalcode", "geolocation" };
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

        // GCI0036 -- mutation in a visible getter block (or mutation within [Pure]-annotated context)
        {
            bool hasGetterMutation = HasGetterMutationInDiff(rawDiff, pathLines);
            if (hasGetterMutation)
                labels.Add(MakeLabel("GCI0036",
                    "Diff adds a field assignment inside a property getter block",
                    0.60));
        }

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

        // GCI0039 -- Direct HttpClient instantiation in non-test .cs files.
        // Per-file iteration mirrors the rule's IsTestFile guard so test-only
        // new HttpClient() (e.g., RestSharp tests, google-api tests) are not labeled Positive.
        {
            bool hasDirectHttpClient39 = false;
            bool inNonTestCs39 = false;
            foreach (var rawLine in rawDiff.Split('\n'))
            {
                var t39 = rawLine.TrimEnd('\r');
                if (t39.StartsWith("+++ b/"))
                {
                    var path39 = t39[6..].Trim();
                    bool isCsFile39 = path39.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
                    bool isTest39 = path39.Contains("test", StringComparison.OrdinalIgnoreCase)
                        || path39.Contains("spec", StringComparison.OrdinalIgnoreCase);
                    inNonTestCs39 = isCsFile39 && !isTest39;
                    continue;
                }
                if (!inNonTestCs39) continue;
                if (!t39.StartsWith('+') || t39.StartsWith("+++")) continue;
                var c39 = t39[1..];
                if (c39.TrimStart().StartsWith("//")) continue;
                if (c39.Contains("new HttpClient(", StringComparison.Ordinal))
                { hasDirectHttpClient39 = true; break; }
            }
            if (hasDirectHttpClient39)
                labels.Add(MakeLabel("GCI0039", "Diff instantiates HttpClient directly in a non-test C# file, bypassing IHttpClientFactory", 0.65));
        }

        // GCI0041 -- Silenced tests in test files.
        // Uses per-file tracking so .Skip() in source files (stream/reader methods) and
        // [SkipLocalsInit] attributes never match. Only added lines inside test paths count.
        {
            var silenceTokens = new[] { "[Ignore", "[Skip]", "[Skip(", ".Skip(", "[Fact(Skip", "[Theory(Skip" };
            bool hasSilencedTest = false;
            bool inTestFile = false;
            foreach (var diffLine in rawDiff.Split('\n'))
            {
                var trimmed = diffLine.TrimEnd('\r');
                if (trimmed.StartsWith("+++ b/", StringComparison.Ordinal))
                {
                    var filePath = trimmed[6..];
                    inTestFile = (filePath.Contains("test", StringComparison.OrdinalIgnoreCase) ||
                                  filePath.Contains("spec", StringComparison.OrdinalIgnoreCase)) &&
                                 !filePath.Contains("testdata", StringComparison.OrdinalIgnoreCase);
                    continue;
                }
                if (!inTestFile) continue;
                if (!trimmed.StartsWith('+') || trimmed.StartsWith("+++")) continue;
                var content = trimmed[1..];
                if (silenceTokens.Any(s => content.Contains(s, StringComparison.OrdinalIgnoreCase)))
                {
                    hasSilencedTest = true;
                    break;
                }
            }
            if (hasSilencedTest)
                labels.Add(MakeLabel("GCI0041", "Diff silences or skips a test in a test file", 0.65));
        }

        // GCI0042 -- TODO/FIXME/HACK marker or NotImplementedException in non-test .cs files
        // Per-file iteration mirrors the rule: only non-test .cs files, comment markers must
        // be the first token after // (prevents "hvc1 hack variant"-style prose matches).
        {
            bool hasStub42 = false;
            bool inNonTestCs42 = false;
            foreach (var rawLine in rawDiff.Split('\n'))
            {
                var trimmed42 = rawLine.TrimEnd('\r');
                if (trimmed42.StartsWith("+++ b/"))
                {
                    var path42 = trimmed42[6..].Trim();
                    bool isCsFile = path42.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
                    bool isTest42 = path42.Contains("test", StringComparison.OrdinalIgnoreCase)
                        || path42.Contains("spec", StringComparison.OrdinalIgnoreCase);
                    inNonTestCs42 = isCsFile && !isTest42;
                    continue;
                }
                if (!inNonTestCs42) continue;
                if (!trimmed42.StartsWith('+') || trimmed42.StartsWith("+++")) continue;
                var content42 = trimmed42[1..];
                var ct42 = content42.TrimStart();
                if (ct42.StartsWith("///", StringComparison.Ordinal)) continue;
                if (ct42.StartsWith("//", StringComparison.Ordinal))
                {
                    var body42 = ct42[2..].TrimStart();
                    if (body42.StartsWith("TODO", StringComparison.OrdinalIgnoreCase) ||
                        body42.StartsWith("FIXME", StringComparison.OrdinalIgnoreCase) ||
                        body42.StartsWith("HACK", StringComparison.OrdinalIgnoreCase))
                    { hasStub42 = true; break; }
                }
                else if (content42.Contains("TODO", StringComparison.OrdinalIgnoreCase) ||
                         content42.Contains("FIXME", StringComparison.OrdinalIgnoreCase) ||
                         content42.Contains("HACK", StringComparison.OrdinalIgnoreCase) ||
                         content42.Contains("throw new NotImplementedException", StringComparison.Ordinal))
                { hasStub42 = true; break; }
            }
            if (hasStub42)
                labels.Add(MakeLabel("GCI0042", "Diff contains a TODO/FIXME/HACK marker or NotImplementedException stub in a non-test C# file", 0.70));
        }

        // GCI0043 -- Null-forgiving / nullable pragma disable / unchecked as-cast
        // Mirror all three rule checks with the same skip guards and thresholds.
        {
            var nullableCodes43 = new[] { "nullable", "CS8600", "CS8601", "CS8602", "CS8603", "CS8604" };
            var nullCheckPatterns43 = new[] { "is null", "== null", "!= null", "?? ", "is not null" };

            // Check 1: Pragma disable for nullable: mirrors CheckPragmaDisable (per-line, no threshold)
            bool hasPragma43 = prodCsLines.Any(l =>
                l.Contains("#pragma warning disable", StringComparison.OrdinalIgnoreCase) &&
                nullableCodes43.Any(c => l.Contains(c, StringComparison.OrdinalIgnoreCase)));

            // Check 2: Null-forgiving: mirrors CheckNullForgiving's >1 threshold (matchingLines.Count <= 1 returns early)
            int nullForgivingCount = prodCsLines.Count(l =>
                !l.TrimStart().StartsWith("//") &&
                !l.Contains("GetValueForOption(", StringComparison.Ordinal) &&
                (l.Contains("!.", StringComparison.Ordinal) ||
                 l.Contains("!;", StringComparison.Ordinal) ||
                 l.Contains("!,", StringComparison.Ordinal)));
            bool hasNullForgiving43 = nullForgivingCount > 1;

            // Check 3: Unchecked as-cast: mirrors CheckUncheckedAsCast with same skip guards
            bool hasUncheckedAsCast43 = false;
            var asCastLines = prodCsLines.ToList();
            for (int i = 0; i < asCastLines.Count && !hasUncheckedAsCast43; i++)
            {
                var l = asCastLines[i];
                if (!l.Contains(" as ", StringComparison.Ordinal)) continue;
                var t = l.TrimStart();
                if (t.StartsWith("//") || t.StartsWith("///") || t.StartsWith("*")) continue;
                var ap = l.IndexOf(" as ", StringComparison.Ordinal);
                var afterAs43 = l[(ap + 4)..].TrimStart();
                // as object: always safe
                if (afterAs43.StartsWith("object", StringComparison.Ordinal) &&
                    (afterAs43.Length == 6 || (!char.IsLetterOrDigit(afterAs43[6]) && afterAs43[6] != '_'))) continue;
                // (x as T)?.: null-conditional, safe
                if (l[(ap + 4)..].Contains(")?.", StringComparison.Ordinal)) continue;
                // .Value boundary: owned by GCI0006
                if (l.Contains(".Value", StringComparison.Ordinal)) continue;
                // ±2 null-check window
                int s43 = Math.Max(0, i - 2), e43 = Math.Min(asCastLines.Count - 1, i + 2);
                bool hasNullCheck43 = false;
                for (int j = s43; j <= e43; j++)
                    if (nullCheckPatterns43.Any(p => asCastLines[j].Contains(p, StringComparison.Ordinal)))
                    { hasNullCheck43 = true; break; }
                if (!hasNullCheck43) hasUncheckedAsCast43 = true;
            }

            if (hasPragma43 || hasNullForgiving43 || hasUncheckedAsCast43)
                labels.Add(MakeLabel("GCI0043", "Diff disables nullable warnings, uses multiple null-forgiving operators, or has unchecked as-cast on added lines", 0.60));
        }

        // GCI0044 -- Performance hotpath risk: Thread.Sleep / LINQ in loop / unbounded .Add in loop
        // Mirror the rule's three checks.  LINQ and .Add checks need loop context from unchanged
        // diff lines; parse non-removed lines from rawDiff (same approach as the rule's lookback).
        {
            // Check 1: Thread.Sleep: no loop context required
            if (prodCsLines.Any(l => l.Contains("Thread.Sleep(", StringComparison.Ordinal)))
            {
                labels.Add(MakeLabel("GCI0044", "Diff adds Thread.Sleep in production code", 0.65));
            }
            else
            {
                // Build non-removed lines list from rawDiff, scoped to non-test C# files,
                // preserving context lines so loop keywords on unchanged code are visible.
                bool inTestFile44 = false;
                bool inCsFile44   = false;
                var nonRemoved44  = new List<(bool IsAdded, string Content)>();
                foreach (var dl in rawDiff.Split('\n'))
                {
                    if (dl.StartsWith("+++ b/", StringComparison.Ordinal))
                    {
                        var fp = dl[6..].TrimEnd('\r');
                        inCsFile44   = fp.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
                        inTestFile44 = fp.Contains("test", StringComparison.OrdinalIgnoreCase) ||
                                       fp.Contains("spec", StringComparison.OrdinalIgnoreCase);
                        continue;
                    }
                    if (!inCsFile44 || inTestFile44) continue;
                    if (dl.StartsWith("---") || dl.StartsWith("+++") || dl.StartsWith("@@")) continue;
                    if (dl.StartsWith("-")) continue;
                    nonRemoved44.Add((dl.StartsWith("+"), dl.StartsWith("+") ? dl[1..] : dl));
                }

                var linqMethods44        = new[] { ".Where(", ".Select(", ".FirstOrDefault(", ".Any(", ".Count(" };
                var loopKeywordsAll44    = new[] { "for (", "foreach (", "while (" };
                var unboundedLoops44     = new[] { "for (", "while (" };

                bool triggered44 = false;
                for (int i = 0; i < nonRemoved44.Count && !triggered44; i++)
                {
                    if (!nonRemoved44[i].IsAdded) continue;
                    var lc = nonRemoved44[i].Content;

                    bool hasLinq = linqMethods44.Any(m => lc.Contains(m, StringComparison.Ordinal));
                    // Unsafe.Add(ref ...) is pointer arithmetic: neutralise before checking .Add(
                    bool hasCollectionAdd = lc.Replace("Unsafe.Add(", "UNSAFE_PTR(")
                                             .Contains(".Add(", StringComparison.Ordinal);

                    if (!hasLinq && !hasCollectionAdd) continue;

                    var keywords = hasLinq ? loopKeywordsAll44 : unboundedLoops44;
                    int lookback = Math.Max(0, i - 10);
                    for (int j = lookback; j < i; j++)
                    {
                        if (keywords.Any(k => nonRemoved44[j].Content.Contains(k, StringComparison.Ordinal)))
                        {
                            triggered44 = true;
                            break;
                        }
                    }
                }

                if (triggered44)
                    labels.Add(MakeLabel("GCI0044", "Diff adds LINQ call or unbounded .Add inside a loop on production code", 0.55));
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

        // GCI0047 -- Contradictory CRUD verb rename: requires the SAME base name to appear in
        // both removed and added lines with DIFFERENT verb prefixes from the same ContradictoryPairs
        // set used by the rule (e.g. GetUser removed, DeleteUser added). Uses production .cs files
        // only and requires a method signature context (access modifier) to avoid call-site noise.
        // Cross-file renames (method removed from one file, added to another) are detectable here
        // since we collect globally; the rule fires per-file so those produce FNs by design.
        {
            var crudSigPattern = @"(?:public|private|protected|internal)\s+(?:(?:static|async|virtual|override|sealed)\s+)*[\w<>\[\]?]+\s+((?:Get|Set|Add|Remove|Delete|Create|Update|Find|Fetch|Load|Save|Insert)(\w*))\s*\(";
            // Mirror GCI0047_NamingContractAlignment.ContradictoryPairs exactly.
            var contradictoryPairs = new HashSet<(string, string)>
            {
                ("Get","Delete"), ("Delete","Get"), ("Get","Remove"), ("Remove","Get"),
                ("Add","Remove"), ("Remove","Add"), ("Add","Delete"), ("Delete","Add"),
                ("Create","Delete"), ("Delete","Create"), ("Create","Remove"), ("Remove","Create"),
                ("Insert","Delete"), ("Delete","Insert"), ("Insert","Remove"), ("Remove","Insert"),
                ("Save","Delete"), ("Delete","Save"), ("Save","Remove"), ("Remove","Save"),
                ("Find","Delete"), ("Delete","Find"), ("Find","Remove"), ("Remove","Find"),
                ("Fetch","Delete"), ("Delete","Fetch"), ("Fetch","Remove"), ("Remove","Fetch"),
                ("Load","Delete"), ("Delete","Load"), ("Load","Remove"), ("Remove","Load"),
            };
            var removedBases = prodCsRemovedLines
                .Select(l => Regex.Match(l, crudSigPattern))
                .Where(m => m.Success)
                .Select(m => (Verb: m.Groups[1].Value[..^m.Groups[2].Value.Length], Base: m.Groups[2].Value))
                .ToList();
            var addedBases = prodCsLines
                .Select(l => Regex.Match(l, crudSigPattern))
                .Where(m => m.Success)
                .Select(m => (Verb: m.Groups[1].Value[..^m.Groups[2].Value.Length], Base: m.Groups[2].Value))
                .ToList();
            var addedVerbBases   = new HashSet<(string, string)>(addedBases.Select(a  => (a.Verb, a.Base)));
            var removedVerbBases = new HashSet<(string, string)>(removedBases.Select(r => (r.Verb, r.Base)));
            bool hasContradiction = removedBases.Any(r =>
                // Guard: skip if the removed method still exists in added lines (not renamed away)
                !addedVerbBases.Contains((r.Verb, r.Base)) &&
                addedBases.Any(a =>
                    string.Equals(r.Base, a.Base, StringComparison.OrdinalIgnoreCase) &&
                    // Guard: skip if the added method already existed in removed lines (not newly introduced)
                    !removedVerbBases.Contains((a.Verb, a.Base)) &&
                    contradictoryPairs.Contains((r.Verb, a.Verb))));
            if (hasContradiction)
                labels.Add(MakeLabel("GCI0047", "Diff renames a CRUD-verb method to a semantically opposing verb on the same base name", 0.65));
        }

        // GCI0049 -- Float/double equality comparison
        if (addedLines.Any(l =>
                !l.TrimStart().StartsWith("//") &&
                (Regex.IsMatch(l, @"(?:==|!=)\s*(?:[-+]?\d*\.\d+|\d+\.\d+)[fFdD]?\b") ||
                 Regex.IsMatch(l, @"\b(?:float|double)\b.*(?:==|!=)", RegexOptions.IgnoreCase))))
        {
            labels.Add(MakeLabel("GCI0049", "Diff contains floating-point equality comparison on added lines", 0.60));
        }

        // GCI0015 -- Data integrity risk: SQL IGNORE pattern or HTTP input binding in production code.
        {
            var httpSignals0015 = new[] { "Request.Form", "Request.Query", "Request.Body", "HttpContext.Request", "[FromBody]", "[FromForm]", "[FromQuery]" };
            var sqlIgnore0015   = new[] { "INSERT IGNORE", "ON CONFLICT DO NOTHING", "INSERT OR IGNORE" };
            bool hasSqlIgnore0015 = addedLines.Any(l =>
                sqlIgnore0015.Any(p => l.Contains(p, StringComparison.OrdinalIgnoreCase)));
            bool hasHttpInput0015 = prodCsLines.Any(l =>
                httpSignals0015.Any(s => l.Contains(s, StringComparison.Ordinal)));
            if (hasSqlIgnore0015 || hasHttpInput0015)
                labels.Add(MakeLabel("GCI0015", "Diff contains SQL IGNORE pattern or HTTP input binding in production code", 0.55));
        }

        // GCI0035 -- Architecture layer guard: added using directive importing an infrastructure namespace
        // inside a file under a domain or application layer path.
        {
            bool triggered0035 = false;
            bool inDomainOrApp = false;
            foreach (var rawLine in rawDiff.Split('\n'))
            {
                var t35 = rawLine.TrimEnd('\r');
                if (t35.StartsWith("+++ b/", StringComparison.Ordinal))
                {
                    var lower35 = t35[6..].ToLowerInvariant();
                    inDomainOrApp = lower35.Contains("/domain/") || lower35.Contains("/application/")
                                 || lower35.Contains(".domain.") || lower35.Contains(".application.");
                    continue;
                }
                if (!inDomainOrApp) continue;
                if (!t35.StartsWith('+') || t35.StartsWith("+++")) continue;
                var c35 = t35[1..].TrimStart();
                if (!c35.StartsWith("using ", StringComparison.Ordinal)) continue;
                if (c35.Contains(".Infrastructure.", StringComparison.OrdinalIgnoreCase) ||
                    c35.Contains(".Persistence.", StringComparison.OrdinalIgnoreCase) ||
                    c35.Contains(".Data.", StringComparison.OrdinalIgnoreCase) ||
                    c35.Contains(".Database.", StringComparison.OrdinalIgnoreCase))
                { triggered0035 = true; break; }
            }
            if (triggered0035)
                labels.Add(MakeLabel("GCI0035", "Diff adds a using directive importing an infrastructure namespace in a domain/application layer file", 0.60));
        }

        // GCI0048 -- System.Random instantiation near a security-sensitive identifier in production code.
        {
            var secTerms0048 = new[] { "token", "secret", "password", "apikey", "api_key", "privatekey", "private_key", "accesskey", "access_key", "salt", "nonce", "credential", "passphrase", "hmac" };
            var prodList0048 = prodCsLines.ToList();
            bool triggered0048 = false;
            for (int i = 0; i < prodList0048.Count && !triggered0048; i++)
            {
                var l48 = prodList0048[i];
                if (!Regex.IsMatch(l48, @"\bnew\s+Random\s*\(", RegexOptions.IgnoreCase)) continue;
                if (l48.TrimStart().StartsWith("//")) continue;
                int s48 = Math.Max(0, i - 5), e48 = Math.Min(prodList0048.Count - 1, i + 5);
                for (int j = s48; j <= e48 && !triggered0048; j++)
                {
                    var lower48 = prodList0048[j].ToLowerInvariant();
                    if (secTerms0048.Any(t => lower48.Contains(t))) triggered0048 = true;
                }
            }
            if (triggered0048)
                labels.Add(MakeLabel("GCI0048", "Diff instantiates System.Random near a security-sensitive identifier in production code", 0.70));
        }

        // GCI0050 -- SQL column truncation risk: short nvarchar/varchar or StringLength/MaxLength in schema files.
        {
            var schemaTokens0050 = new[] { "/migration", "migration.cs", "schema", "dbcontext", "entityconfig", "modelbuilder", "fluent" };
            bool inSchema0050    = false;
            bool triggered0050   = false;
            var varcharRx0050    = new Regex(@"\bn?varchar\s*\(\s*(\d+)\s*\)", RegexOptions.IgnoreCase);
            var attrRx0050       = new Regex(@"\[(?:StringLength|MaxLength)\s*\(\s*(\d+)", RegexOptions.IgnoreCase);
            var fluentRx0050     = new Regex(@"\bHasMaxLength\s*\(\s*(\d+)\s*\)");
            foreach (var rawLine in rawDiff.Split('\n'))
            {
                var t50 = rawLine.TrimEnd('\r');
                if (t50.StartsWith("+++ b/", StringComparison.Ordinal))
                {
                    var lower50 = t50[6..].ToLowerInvariant();
                    inSchema0050 = lower50.EndsWith(".cs") && schemaTokens0050.Any(tok => lower50.Contains(tok));
                    continue;
                }
                if (!inSchema0050) continue;
                if (!t50.StartsWith('+') || t50.StartsWith("+++")) continue;
                var c50 = t50[1..];
                var tr50 = c50.TrimStart();
                if (tr50.StartsWith("//") || tr50.StartsWith("--") || tr50.StartsWith("*")) continue;
                foreach (var rx50 in new[] { varcharRx0050, attrRx0050, fluentRx0050 })
                {
                    var m50 = rx50.Match(c50);
                    if (m50.Success && int.TryParse(m50.Groups[1].Value, out int n50) && n50 < 100)
                    { triggered0050 = true; break; }
                }
                if (triggered0050) break;
            }
            if (triggered0050)
                labels.Add(MakeLabel("GCI0050", "Diff adds a short string column definition (< 100 chars) in a schema or migration file", 0.65));
        }

        // GCI0053 -- Lockfile changed without source changes.
        {
            var lockfileNames0053 = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "packages.lock.json", "package-lock.json", "yarn.lock", "Pipfile.lock",
                "go.sum", "Cargo.lock", "Directory.Packages.props", "pnpm-lock.yaml", "poetry.lock",
            };
            var sourceExts0053 = new[] { ".cs", ".ts", ".js", ".py", ".go", ".rs" };
            bool hasLockfile0053 = pathLines.Any(l =>
            {
                if (!l.StartsWith("+++ b/", StringComparison.Ordinal)) return false;
                var path = l[6..].TrimEnd('\r');
                return lockfileNames0053.Contains(Path.GetFileName(path)) ||
                       Path.GetExtension(path).Equals(".lock", StringComparison.OrdinalIgnoreCase);
            });
            if (hasLockfile0053)
            {
                bool hasSource0053 = pathLines.Any(l =>
                {
                    if (!l.StartsWith("+++ b/", StringComparison.Ordinal)) return false;
                    return sourceExts0053.Contains(Path.GetExtension(l[6..].TrimEnd('\r')).ToLowerInvariant());
                });
                if (!hasSource0053)
                    labels.Add(MakeLabel("GCI0053", "Diff modifies a lockfile without any accompanying source file changes", 0.60));
            }
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

    // -- GCI0006 labeler helpers -----------------------------------------------

    // Returns true when the line contains an unsafe .Value access (not safe variants).
    private static bool HasLabelerUnsafeValueAccess(string line)
    {
        int pos = 0;
        while (pos < line.Length)
        {
            int idx = line.IndexOf(".Value", pos, StringComparison.Ordinal);
            if (idx < 0) return false;
            int after = idx + 6;
            // .Values, .ValueOrDefault etc.: not a bare .Value access
            if (after < line.Length && (char.IsLetterOrDigit(line[after]) || line[after] == '_'))
            { pos = after; continue; }
            // .Value!: null-forgiving
            if (after < line.Length && line[after] == '!') { pos = after; continue; }
            // .Value?: null-conditional
            if (after < line.Length && line[after] == '?') { pos = after; continue; }
            // ?.Value: null-conditional
            if (idx > 0 && line[idx - 1] == '?') { pos = after; continue; }
            return true;
        }
        return false;
    }

    // Returns true when the line has a .Key (word-boundary) access, indicating KVP iteration.
    private static bool HasLabelerDotKeyAccess(string content)
    {
        int pos = 0;
        while (pos < content.Length)
        {
            int idx = content.IndexOf(".Key", pos, StringComparison.Ordinal);
            if (idx < 0) return false;
            int after = idx + 4;
            if (after >= content.Length ||
                (!char.IsLetterOrDigit(content[after]) && content[after] != '_'))
                return true;
            pos = after;
        }
        return false;
    }

    // Returns true when a method signature has no return type token before the opening paren
    // (i.e., it is a constructor).  Strips visibility/modifier keywords first.
    private static readonly string[] LabelerCtorSkipKeywords =
        ["public", "protected", "internal", "private", "static", "async", "virtual",
         "override", "sealed", "new", "extern", "abstract"];

    private static bool IsLabelerConstructor(string trimmedLine)
    {
        var part = trimmedLine;
        bool stripped = true;
        while (stripped)
        {
            stripped = false;
            foreach (var kw in LabelerCtorSkipKeywords)
            {
                if (!part.StartsWith(kw + " ", StringComparison.Ordinal)) continue;
                part = part[(kw.Length + 1)..].TrimStart();
                stripped = true;
            }
        }
        int paren = part.IndexOf('(');
        if (paren <= 0) return false;
        return !part[..paren].Contains(' ');
    }

    // Returns true when valueLine and guardLine share the same root identifier.
    private static bool HasSharedRoot(string valueLine, string guardLine)
    {
        int vi = valueLine.IndexOf(".Value", StringComparison.Ordinal);
        if (vi <= 0) return false;
        int s = vi - 1;
        while (s > 0 && valueLine[s - 1] is char c2 &&
               (char.IsLetterOrDigit(c2) || c2 is '_' or '.' or '[' or ']')) s--;
        var expr = valueLine[s..vi];
        int b = expr.IndexOfAny(['.', '[']);
        var root = b > 0 ? expr[..b] : expr;
        root = new string(root.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        return root.Length > 0 && guardLine.Contains(root + ".Success", StringComparison.Ordinal);
    }

    // -- Tightened GCI0007 helper ----------------------------------------------

    // SQL string literal starting with a DML keyword (trailing space prevents matching
    // LINQ .Select(), C# identifiers like UpdateEntityType, or English words like "selection").
    private static readonly Regex SqlStringLiteralStart = new(
        @"(?:=|return|\(|,)\s*(?:@|\$)?""(?:SELECT |INSERT |UPDATE |DELETE )",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Credential keyword assigned to a quoted string literal: the real risky pattern
    private static readonly Regex CredentialAssignToLiteral = new(
        @"(password|secret|api_key|apikey|private_key|privatekey|client_secret|access_token|auth_token)\s*[=:]\s*""[^""]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Hard-coded credential-looking value (base64 or long alphanumeric after = "")
    // Requires at least one digit to avoid matching pure-CamelCase identifiers like
    // "SignatureVerificationFailed" or "ResolvePackageAssets" which are constant names, not secrets.
    private static readonly Regex HardcodedCredentialValue = new(
        @"=\s*""(?=[A-Za-z0-9+/]*[0-9])[A-Za-z0-9+/]{20,}={0,2}""",
        RegexOptions.Compiled);

    /// <summary>
    /// Mirrors the getter-mutation scan in GCI0036 by walking all diff lines (context + added)
    /// and checking whether a `+` line contains a field/property assignment inside a getter block.
    /// Resets state only at file headers, not at hunk boundaries, to match rule behavior.
    /// </summary>
    private static bool HasGetterMutationInDiff(string rawDiff, List<string> pathLines)
    {
        if (string.IsNullOrEmpty(rawDiff)) return false;

        int braceDepth = 0;
        bool inGetter = false;
        int getterExitDepth = -1;
        int getterStartIdx = -1;
        bool expectGetterBrace = false;
        bool skipCurrentFile = false;

        var diffLines = rawDiff.Split('\n');
        for (int i = 0; i < diffLines.Length; i++)
        {
            var rawLine = diffLines[i];

            // File headers reset state; track per-file test status
            if (rawLine.StartsWith("diff ", StringComparison.Ordinal) ||
                rawLine.StartsWith("index ", StringComparison.Ordinal) ||
                rawLine.StartsWith("--- ", StringComparison.Ordinal))
            {
                braceDepth = 0; inGetter = false; getterExitDepth = -1; getterStartIdx = -1; expectGetterBrace = false;
                continue;
            }
            if (rawLine.StartsWith("+++ ", StringComparison.Ordinal))
            {
                braceDepth = 0; inGetter = false; getterExitDepth = -1; getterStartIdx = -1; expectGetterBrace = false;
                skipCurrentFile = rawLine.Contains("Test", StringComparison.OrdinalIgnoreCase) ||
                                  rawLine.Contains("Spec", StringComparison.OrdinalIgnoreCase) ||
                                  rawLine.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase) ||
                                  rawLine.Contains(".g.cs", StringComparison.OrdinalIgnoreCase);
                continue;
            }
            if (rawLine.StartsWith("@@")) continue; // hunk header -- no state reset (mirrors rule)

            bool isAdded = rawLine.Length > 0 && rawLine[0] == '+';
            bool isRemoved = rawLine.Length > 0 && rawLine[0] == '-';
            if (isRemoved) continue;

            var content = rawLine.Length > 0 ? rawLine[1..] : "";
            var trimmed = content.TrimStart();

            // Getter start -- inline brace
            if (trimmed.StartsWith("get {", StringComparison.Ordinal) ||
                trimmed.Contains(" get {", StringComparison.Ordinal))
            {
                getterExitDepth = braceDepth;
                getterStartIdx = i;
                inGetter = true;
                expectGetterBrace = false;
            }
            // Getter on its own line
            else if (trimmed == "get" ||
                (trimmed.Length > 4 && trimmed.EndsWith(" get", StringComparison.Ordinal) && !trimmed.Contains('{')))
            {
                expectGetterBrace = true;
            }
            // Deferred opening brace
            else if (expectGetterBrace && (trimmed == "{" || trimmed.StartsWith("{ ", StringComparison.Ordinal)))
            {
                getterExitDepth = braceDepth;
                getterStartIdx = i;
                inGetter = true;
                expectGetterBrace = false;
            }
            else
            {
                expectGetterBrace = false;
            }

            bool inPureContext = inGetter;

            foreach (char c in content)
            {
                if (c == '{') braceDepth++;
                else if (c == '}') braceDepth--;
            }

            if (inGetter && braceDepth <= getterExitDepth)
            {
                inGetter = false;
                getterStartIdx = -1;
            }

            // Check for field/property assignment on an added line inside a getter
            if (isAdded && inPureContext && !trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                if (!trimmed.StartsWith("var ", StringComparison.Ordinal) &&
                    !trimmed.StartsWith("for ", StringComparison.Ordinal) &&
                    !trimmed.StartsWith("for(", StringComparison.Ordinal))
                {
                    for (int k = 0; k < trimmed.Length; k++)
                    {
                        if (trimmed[k] != '=') continue;
                        char prev = k > 0 ? trimmed[k - 1] : '\0';
                        char next = k + 1 < trimmed.Length ? trimmed[k + 1] : '\0';
                        if (prev is '=' or '!' or '<' or '>') continue;
                        if (next is '=' or '>') continue;
                        var lhs = trimmed[..k].TrimEnd('+', '-', '*', '/', '%', '|', '&', '^', ' ');
                        if (!lhs.Contains(' ') && !IsLocalVariableInLabelerScope(diffLines, getterStartIdx, i, lhs)
                            && !IsNullGuardedInLabelerScope(diffLines, i, lhs))
                        {
                            if (!skipCurrentFile) return true;
                        }
                    }
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Returns true when <paramref name="lhsName"/> was declared as a local variable in the
    /// getter scope starting at <paramref name="scopeStart"/> in <paramref name="diffLines"/>.
    /// Mirrors the rule's <c>IsLocalVariableInScope</c> helper using raw diff line content.
    /// </summary>
    private static bool IsLocalVariableInLabelerScope(string[] diffLines, int scopeStart, int idx, string lhsName)
    {
        if (scopeStart < 0 || string.IsNullOrEmpty(lhsName)) return false;

        // Private-field naming conventions → always a field
        if (lhsName.StartsWith("_", StringComparison.Ordinal) ||
            lhsName.StartsWith("m_", StringComparison.Ordinal)) return false;

        // Dotted or indexed → can't be a plain local
        if (lhsName.Contains('.') || lhsName.Contains('[')) return false;

        for (int j = scopeStart; j < idx && j < diffLines.Length; j++)
        {
            var raw = diffLines[j];
            if (raw.Length == 0) continue;
            // Strip diff prefix (+, -, space)
            var content = raw[0] is '+' or '-' or ' ' ? raw[1..] : raw;
            if (string.IsNullOrWhiteSpace(content)) continue;

            int pos = -1;
            while ((pos = content.IndexOf(lhsName, pos + 1, StringComparison.Ordinal)) >= 0)
            {
                if (pos == 0 || content[pos - 1] != ' ') continue;
                int afterPos = pos + lhsName.Length;
                if (afterPos < content.Length &&
                    content[afterPos] is not (' ' or '=' or ';' or ',')) continue;
                var before = content[..pos].TrimEnd();
                if (before.Length == 0) continue;
                char lastChar = before[^1];
                if (char.IsLetterOrDigit(lastChar) || lastChar is '>' or ']' or '?')
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Returns true when the assignment LHS is preceded within 20 lines by a null check
    /// on the same name: mirrors the rule's <c>IsNullGuardedAssignment</c> logic.
    /// </summary>
    private static bool IsNullGuardedInLabelerScope(string[] diffLines, int idx, string lhsName)
    {
        if (string.IsNullOrEmpty(lhsName)) return false;

        // Strip this. prefix for matching
        var name = lhsName.Contains('.')
            ? lhsName[(lhsName.LastIndexOf('.') + 1)..]
            : lhsName;
        if (string.IsNullOrEmpty(name)) return false;

        int scanned = 0;
        for (int j = idx - 1; j >= 0 && scanned < 20; j--)
        {
            var raw = diffLines[j];
            if (raw.Length == 0) continue;
            var content = raw[0] is '+' or '-' or ' ' ? raw[1..] : raw;
            var trimmed = content.TrimStart();
            if (string.IsNullOrEmpty(trimmed)) continue;
            scanned++;

            if (trimmed.Contains(name, StringComparison.Ordinal) &&
                (trimmed.Contains("== null",     StringComparison.Ordinal) ||
                 trimmed.Contains("is null",     StringComparison.Ordinal) ||
                 trimmed.Contains("!= null",     StringComparison.Ordinal) ||
                 trimmed.Contains("is not null", StringComparison.Ordinal) ||
                 trimmed.Contains("ReferenceEquals", StringComparison.Ordinal)))
            {
                return true;
            }
        }
        return false;
    }

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

    /// <summary>
    /// Extracts added lines only from production <c>.cs</c> files, skipping test, generated,
    /// and non-production (benchmark/sample/example) files. Used for GCI0012 checks where
    /// test-file credential/hash patterns are intentional.
    /// Returns an empty list when the diff has no eligible production CS files.
    /// </summary>
    private static List<string> ExtractAddedLinesFromProductionCsFiles(string diffText)
    {
        var result = new List<string>();
        var currentFile = string.Empty;
        var inProductionCs = false;

        foreach (var line in diffText.Split('\n'))
        {
            if (line.StartsWith("+++ b/", StringComparison.Ordinal))
            {
                currentFile = line[6..].TrimEnd('\r');
                inProductionCs = currentFile.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) &&
                                 !TestFileClassifier.IsTestFile(currentFile) &&
                                 !IsGeneratedCsFile(currentFile) &&
                                 !IsBenchmarkOrSampleFile(currentFile);
                continue;
            }
            if (inProductionCs && line.StartsWith('+') && !line.StartsWith("+++"))
                result.Add(line[1..]);
        }
        return result;
    }

    /// <summary>
    /// Extracts removed lines only from production <c>.cs</c> files, skipping test, generated,
    /// and non-production (benchmark/sample/example) files. Used for GCI0021 checks.
    /// </summary>
    private static List<string> ExtractRemovedLinesFromProductionCsFiles(string diffText)
    {
        var result = new List<string>();
        var inProductionCs = false;
        string pendingOldFile = string.Empty;

        foreach (var line in diffText.Split('\n'))
        {
            // Buffer old-file path; don't commit until we see +++ to know if file is deleted.
            if (line.StartsWith("--- a/", StringComparison.Ordinal))
            {
                pendingOldFile = line[6..].TrimEnd('\r');
                inProductionCs = false;
                continue;
            }

            // Commit decision: file is modified/added (not deleted).
            if (line.StartsWith("+++ b/", StringComparison.Ordinal))
            {
                inProductionCs = pendingOldFile.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) &&
                                 !TestFileClassifier.IsTestFile(pendingOldFile) &&
                                 !IsGeneratedCsFile(pendingOldFile) &&
                                 !IsBenchmarkOrSampleFile(pendingOldFile);
                pendingOldFile = string.Empty;
                continue;
            }

            // File was deleted entirely: the rule never processes deleted files, so skip.
            if (line.StartsWith("+++ /dev/null", StringComparison.Ordinal))
            {
                inProductionCs = false;
                pendingOldFile = string.Empty;
                continue;
            }

            if (inProductionCs && line.StartsWith('-') && !line.StartsWith("---"))
                result.Add(line[1..]);
        }
        return result;
    }

    /// <summary>
    /// Returns true if <paramref name="path"/> is an EF Core migration CS file:
    /// a <c>.cs</c> file with a directory segment exactly named <c>migrations</c>.
    /// </summary>
    private static bool IsEfMigrationCsFile(string path)
    {
        if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) return false;
        if (TestFileClassifier.IsTestFile(path)) return false;
        var segments = path.ToLowerInvariant().Split(['/', '\\']);
        return segments.Take(segments.Length - 1).Any(seg => seg == "migrations");
    }

    private static bool IsGeneratedCsFile(string path)
    {
        var lower = path.ToLowerInvariant();
        return lower.EndsWith(".g.cs") ||
               lower.EndsWith(".g.i.cs") ||
               lower.EndsWith(".designer.cs") ||
               lower.EndsWith("assemblyinfo.cs") ||
               lower.Contains("/obj/") ||
               lower.Contains("/generated/");
    }

    private static bool IsBenchmarkOrSampleFile(string path)
    {
        // Check each directory segment (exclude the file name at the end)
        var segments = path.ToLowerInvariant().Split(['/', '\\']);
        foreach (var seg in segments.Take(segments.Length - 1))
        {
            if (seg is "benchmark" or "benchmarks" or "sample" or "samples" or "example" or "examples")
                return true;
            if (seg.EndsWith(".benchmark") || seg.EndsWith(".benchmarks") ||
                seg.EndsWith(".sample") || seg.EndsWith(".samples"))
                return true;
        }
        return false;
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

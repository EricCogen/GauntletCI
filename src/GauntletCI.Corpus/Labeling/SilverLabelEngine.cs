// SPDX-License-Identifier: Elastic-2.0
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using GauntletCI.Corpus.Interfaces;
using GauntletCI.Corpus.Labeling.Strategies;
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
    private readonly IReadOnlyList<IInferenceStrategy> _strategies;

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
        ([".result", ".wait()", "async", "blocking", "deadlock", "configureawait", "socket", "thread pool", "concurrency", "cpu bound"],
            "GCI0016", "Review comment mentions async/blocking concern or concurrent execution model", 0.65),
        (["hardcoded", "hard-coded", "magic string", "magic number"],
            "GCI0010", "Review comment mentions hardcoded value or magic number/string", 0.6),
        (["exception", "catch", "swallowing", "ignored exception"],
            "GCI0032", "Review comment mentions exception handling concern", 0.6),
        // Note: "thread safe / concurrent / lock" keywords intentionally removed from GCI0016.
        // GCI0016 scope is async execution model violations only (dropped static mutable field
        // check). Thread-safety review comments signal concerns the rule no longer detects.
        // Async domain expansion: Added "socket", "thread pool", "concurrency", "cpu bound" keywords
        // to improve detection of async execution model violations and related resource issues.
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

        // Initialize strategy registry in execution order
        _strategies = new IInferenceStrategy[]
        {
            new SecurityPatternStrategy(),
            new AsyncPatternStrategy(),
            new ExceptionHandlingPatternStrategy(),
            new DataIntegrityPatternStrategy(),
            new NullabilityPatternStrategy(),
            new EdgeCasePatternStrategy(),
        };
    }

    /// <summary>
    /// Infers heuristic labels from diff text alone using registered strategies.
    /// Returns only positive labels; negative labels are generated by ApplyToFixtureAsync
    /// for rules with no positive signal.
    /// </summary>
    public Task<IReadOnlyList<ExpectedFinding>> InferLabelsAsync(
        string fixtureId, string diffText, CancellationToken ct = default)
    {
        var addedLines = ExtractAddedLines(diffText);
        var removedLines = ExtractRemovedLines(diffText);
        var pathLines = ExtractPathLines(diffText);
        var prodCsLines = ExtractAddedLinesFromProductionCsFiles(diffText);
        var prodCsRemovedLines = ExtractRemovedLinesFromProductionCsFiles(diffText);
        var labels = new List<ExpectedFinding>();

        // Create context for strategies to analyze
        var context = new DiffAnalysisContext
        {
            AddedLines = addedLines,
            RemovedLines = removedLines,
            PathLines = pathLines,
            ProductionAddedLines = prodCsLines,
            ProductionRemovedLines = prodCsRemovedLines,
            RawDiff = diffText,
        };

        // Execute all strategies and collect findings (positive labels only)
        foreach (var strategy in _strategies)
        {
            var findings = strategy.Apply(fixtureId, context);
            labels.AddRange(findings);
        }

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
            using var doc = JsonDocument.Parse(reviewCommentsJson);
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
        var inferred = (await InferLabelsAsync(fixtureId, diffText, ct).ConfigureAwait(false)).ToList();

        IReadOnlyList<string> commentBodies = [];
        IReadOnlySet<string> commentPaths = new HashSet<string>(StringComparer.Ordinal);

        var reviewCommentsJson = await _store.TryReadReviewCommentsAsync(fixtureId, ct).ConfigureAwait(false);
        if (reviewCommentsJson is not null)
        {
            try
            {
                using var doc = JsonDocument.Parse(reviewCommentsJson);
                commentBodies = ExtractCommentBodies(doc.RootElement);
                commentPaths = ExtractCommentPaths(doc.RootElement);

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
        var actualFindings = await _store.ReadActualFindingsAsync(fixtureId, ct).ConfigureAwait(false);

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
                        RuleId = finding.RuleId,
                        ShouldTrigger = true,
                        ExpectedConfidence = 0.55,
                        Reason = $"[file-path correlation] Reviewer commented on '{finding.FilePath}'",
                        LabelSource = LabelSource.FilePathCorrelation,
                        IsInconclusive = false,
                    });
                    positiveRuleIdsTier12.Add(finding.RuleId);
                }
            }
        }

        // ── Phase 21 Coordination: Async Execution Model ─────────────────────
        ApplyAsyncExecutionCoordination(inferred);

        // ── Phase 21 Coordination: Exception Handling ──────────────────────────
        ApplyExceptionHandlingCoordination(inferred);

        // ── Phase 21 Coordination: Resource Management ──────────────────────────
        ApplyResourceManagementCoordination(inferred);

        // ── Phase 21 Coordination: Data Security ──────────────────────────────
        ApplyDataSecurityCoordination(inferred);

        // ── Phase 23 Coordination: Performance & GC ───────────────────────────
        ApplyPhase23P4PerformanceCoordination(inferred);

        // ── Phase 23 Coordination: Serialization Safety ─────────────────────
        ApplyPhase23P5SerializationCoordination(inferred);

        // ── Phase 23 Coordination: Dependency Injection ─────────────────────
        ApplyPhase23P6DependencyInjectionCoordination(inferred);

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
                    ct).ConfigureAwait(false);

                if (result is not null && !result.IsInconclusive)
                {
                    inferred.Add(new ExpectedFinding
                    {
                        RuleId = finding.RuleId,
                        ShouldTrigger = result.ShouldTrigger,
                        ExpectedConfidence = result.Confidence,
                        Reason = $"[llm] {result.Reason}",
                        LabelSource = LabelSource.LlmReview,
                        IsInconclusive = false,
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

        // Collect all rule IDs from registered strategies
        var allStrategyRuleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var strategy in _strategies)
        {
            foreach (var ruleId in strategy.RuleIds)
                allStrategyRuleIds.Add(ruleId);
        }

        // Generate negative labels for strategies' rules with no positive signal
        foreach (var ruleId in allStrategyRuleIds)
        {
            if (!positiveRuleIdsFinal.Contains(ruleId))
            {
                inferred.Add(MakeNegativeLabel(ruleId,
                    "Strategy found no signal for this rule on diff or review comments", 0.40));
            }
        }

        var existingLabels = await _store.ReadExpectedFindingsAsync(fixtureId, ct).ConfigureAwait(false);

        // When overwriting, strip stale heuristic labels for rules removed from strategies.
        if (overwriteExisting)
            existingLabels = existingLabels
                .Where(l => l.LabelSource != LabelSource.Heuristic || allStrategyRuleIds.Contains(l.RuleId))
                .ToList();

        var merged = MergeLabels(existingLabels, inferred, overwriteExisting);
        await _store.SaveExpectedFindingsAsync(fixtureId, merged, ct).ConfigureAwait(false);
        return merged.Count;
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

    // Returns true when a method signature has no return type token before the opening paren
    // (i.e., it is a constructor).  Strips visibility/modifier keywords first.
    private static readonly string[] LabelerCtorSkipKeywords =
        ["public", "protected", "internal", "private", "static", "async", "virtual",
         "override", "sealed", "new", "extern", "abstract"];

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
                (trimmed.Contains("== null", StringComparison.Ordinal) ||
                 trimmed.Contains("is null", StringComparison.Ordinal) ||
                 trimmed.Contains("!= null", StringComparison.Ordinal) ||
                 trimmed.Contains("is not null", StringComparison.Ordinal) ||
                 trimmed.Contains("ReferenceEquals", StringComparison.Ordinal)))
            {
                return true;
            }
        }
        return false;
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
            RuleId = ruleId,
            ShouldTrigger = true,
            ExpectedConfidence = confidence,
            Reason = reason,
            LabelSource = LabelSource.Heuristic,
            IsInconclusive = false,
        };

    private static ExpectedFinding MakeNegativeLabel(string ruleId, string reason, double confidence) =>
        new()
        {
            RuleId = ruleId,
            ShouldTrigger = false,
            ExpectedConfidence = confidence,
            Reason = reason,
            LabelSource = LabelSource.Heuristic,
            IsInconclusive = false,
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
        var section = nextDiff > 0 ? diffText[startIdx..nextDiff] : diffText[startIdx..];

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

    /// <summary>
    /// Phase 21 Rule Coordination: When async violations (GCI0016) are detected,
    /// boost confidence on related HttpClient (GCI0039) and GC pressure (GCI0044) labels
    /// to reflect the increased risk of cascading performance and resource issues.
    /// </summary>
    private void ApplyAsyncExecutionCoordination(List<ExpectedFinding> labels)
    {
        // If GCI0016 (async violation) fires, check if GCI0039 or GCI0044 are present
        var hasGci0016 = labels.Any(l => l.RuleId == "GCI0016" && l.ShouldTrigger);

        if (!hasGci0016)
            return;

        // Boost confidence on GCI0039 (HttpClient safety) - blocking calls + HttpClient = socket exhaustion risk
        var gci0039Index = labels.FindIndex(l => l.RuleId == "GCI0039");
        if (gci0039Index >= 0)
        {
            var gci0039 = labels[gci0039Index];
            if (gci0039.ShouldTrigger)
            {
                // Already triggered - boost confidence from 0.65 to 0.80 to reflect coordination
                if (gci0039.ExpectedConfidence < 0.80)
                {
                    labels[gci0039Index] = new ExpectedFinding
                    {
                        RuleId = gci0039.RuleId,
                        ShouldTrigger = gci0039.ShouldTrigger,
                        ExpectedConfidence = 0.80,
                        Reason = $"[coordination] {gci0039.Reason} + GCI0016 async violation increases risk",
                        LabelSource = gci0039.LabelSource,
                        IsInconclusive = gci0039.IsInconclusive,
                    };
                }
            }
            else
            {
                // Negative label present - raise confidence floor to trigger if blocking call + potential HttpClient issue
                // Change to positive with coordination signal
                labels[gci0039Index] = new ExpectedFinding
                {
                    RuleId = gci0039.RuleId,
                    ShouldTrigger = true,
                    ExpectedConfidence = 0.70,
                    Reason = "[coordination] GCI0016 async violation may correlate with HttpClient resource issues",
                    LabelSource = LabelSource.Heuristic,
                    IsInconclusive = false,
                };
            }
        }

        // Boost confidence on GCI0044 (GC pressure) - blocking calls during allocation = worse GC impact
        var gci0044Index = labels.FindIndex(l => l.RuleId == "GCI0044");
        if (gci0044Index >= 0)
        {
            var gci0044 = labels[gci0044Index];
            if (gci0044.ShouldTrigger)
            {
                // Already triggered - boost confidence from 0.60 to 0.75 to reflect coordination
                if (gci0044.ExpectedConfidence < 0.75)
                {
                    labels[gci0044Index] = new ExpectedFinding
                    {
                        RuleId = gci0044.RuleId,
                        ShouldTrigger = gci0044.ShouldTrigger,
                        ExpectedConfidence = 0.75,
                        Reason = $"[coordination] {gci0044.Reason} + GCI0016 async violation increases GC risk",
                        LabelSource = gci0044.LabelSource,
                        IsInconclusive = gci0044.IsInconclusive,
                    };
                }
            }
            // For negative labels, keep as-is for now but coordination logic is in place
        }
    }

    private void ApplyExceptionHandlingCoordination(List<ExpectedFinding> labels)
    {
        // ── Coordination Pattern 1: Exception Swallowing + Breaking Changes ──
        // When GCI0032 (exception swallowing) + GCI0003 (breaking change) both fire,
        // it signals compound risk: callers will fail AND won't have proper error handling.

        var hasGci0032 = labels.Any(l => l.RuleId == "GCI0032" && l.ShouldTrigger);
        var hasGci0003 = labels.Any(l => l.RuleId == "GCI0003" && l.ShouldTrigger);

        if (hasGci0032 && hasGci0003)
        {
            // Boost GCI0003 (breaking change) - breaking change with poor exception handling is worse
            var gci0003Index = labels.FindIndex(l => l.RuleId == "GCI0003");
            if (gci0003Index >= 0)
            {
                var gci0003 = labels[gci0003Index];
                if (gci0003.ExpectedConfidence < 0.85)
                {
                    labels[gci0003Index] = new ExpectedFinding
                    {
                        RuleId = gci0003.RuleId,
                        ShouldTrigger = gci0003.ShouldTrigger,
                        ExpectedConfidence = 0.85,
                        Reason = "[coordination] Breaking change + GCI0032 exception swallowing = upgrade risk for callers",
                        LabelSource = gci0003.LabelSource,
                        IsInconclusive = gci0003.IsInconclusive,
                    };
                }
            }

            // Boost GCI0032 (exception swallowing) - more serious when breaking change occurs
            var gci0032Index = labels.FindIndex(l => l.RuleId == "GCI0032");
            if (gci0032Index >= 0)
            {
                var gci0032 = labels[gci0032Index];
                if (gci0032.ExpectedConfidence < 0.75)
                {
                    labels[gci0032Index] = new ExpectedFinding
                    {
                        RuleId = gci0032.RuleId,
                        ShouldTrigger = gci0032.ShouldTrigger,
                        ExpectedConfidence = 0.75,
                        Reason = "[coordination] Exception swallowing + GCI0003 breaking change = callers can't handle upgrade",
                        LabelSource = gci0032.LabelSource,
                        IsInconclusive = gci0032.IsInconclusive,
                    };
                }
            }
        }

        // ── Coordination Pattern 2: Exception Swallowing + Async Violations ───
        // When GCI0032 (exception swallowing) + GCI0016 (async violation) both fire,
        // it signals dangerous combination: async context loss + silent failures.

        var hasGci0016 = labels.Any(l => l.RuleId == "GCI0016" && l.ShouldTrigger);

        if (hasGci0032 && hasGci0016)
        {
            // Boost GCI0016 (async violation) - async violation with exception swallowing is worse
            var gci0016Index = labels.FindIndex(l => l.RuleId == "GCI0016");
            if (gci0016Index >= 0)
            {
                var gci0016 = labels[gci0016Index];
                if (gci0016.ExpectedConfidence < 0.88)
                {
                    labels[gci0016Index] = new ExpectedFinding
                    {
                        RuleId = gci0016.RuleId,
                        ShouldTrigger = gci0016.ShouldTrigger,
                        ExpectedConfidence = 0.88,
                        Reason = "[coordination] Async violation + GCI0032 exception swallowing = undebuggable silent failures",
                        LabelSource = gci0016.LabelSource,
                        IsInconclusive = gci0016.IsInconclusive,
                    };
                }
            }

            // Boost GCI0032 (exception swallowing) - more serious when async violation occurs
            var gci0032Index = labels.FindIndex(l => l.RuleId == "GCI0032");
            if (gci0032Index >= 0)
            {
                var gci0032 = labels[gci0032Index];
                if (gci0032.ExpectedConfidence < 0.78)
                {
                    labels[gci0032Index] = new ExpectedFinding
                    {
                        RuleId = gci0032.RuleId,
                        ShouldTrigger = gci0032.ShouldTrigger,
                        ExpectedConfidence = 0.78,
                        Reason = "[coordination] Exception swallowing in async context (GCI0016) loses all error information",
                        LabelSource = gci0032.LabelSource,
                        IsInconclusive = gci0032.IsInconclusive,
                    };
                }
            }
        }
    }

    private void ApplyResourceManagementCoordination(List<ExpectedFinding> labels)
    {
        // ── Coordination Pattern: Resource Lifecycle + Data Integrity ───────────
        // When GCI0024 (resource leak) + GCI0015 (data integrity) both fire,
        // it signals compound risk: resource not disposed DURING data corruption.
        // Result: cascading failure where either makes the other worse.
        //
        // Examples:
        // - SqlConnection not disposed + unchecked cast = pool exhaustion + wrong data
        // - FileStream not disposed + integer overflow = handle leak + wrong file position
        // - DbContext not disposed + mass assignment = connection pressure + privilege escalation

        var hasGci0024 = labels.Any(l => l.RuleId == "GCI0024" && l.ShouldTrigger);
        var hasGci0015 = labels.Any(l => l.RuleId == "GCI0015" && l.ShouldTrigger);

        if (hasGci0024 && hasGci0015)
        {
            // Boost GCI0024 (resource lifecycle) - resource leak confirmed by concurrent data corruption
            var gci0024Index = labels.FindIndex(l => l.RuleId == "GCI0024");
            if (gci0024Index >= 0)
            {
                var gci0024 = labels[gci0024Index];
                if (gci0024.ExpectedConfidence < 0.80)
                {
                    labels[gci0024Index] = new ExpectedFinding
                    {
                        RuleId = gci0024.RuleId,
                        ShouldTrigger = gci0024.ShouldTrigger,
                        ExpectedConfidence = 0.80,
                        Reason = "[coordination] Resource leak + GCI0015 data integrity risk = cascading failure. Resource not disposed during data corruption.",
                        LabelSource = gci0024.LabelSource,
                        IsInconclusive = gci0024.IsInconclusive,
                    };
                }
            }

            // Boost GCI0015 (data integrity) - data corruption confirmed by concurrent resource leak
            var gci0015Index = labels.FindIndex(l => l.RuleId == "GCI0015");
            if (gci0015Index >= 0)
            {
                var gci0015 = labels[gci0015Index];
                if (gci0015.ExpectedConfidence < 0.75)
                {
                    labels[gci0015Index] = new ExpectedFinding
                    {
                        RuleId = gci0015.RuleId,
                        ShouldTrigger = gci0015.ShouldTrigger,
                        ExpectedConfidence = 0.75,
                        Reason = "[coordination] Data integrity risk + GCI0024 resource leak = compound risk. Data corruption occurs while resource remains open.",
                        LabelSource = gci0015.LabelSource,
                        IsInconclusive = gci0015.IsInconclusive,
                    };
                }
            }
        }
    }

    /// <summary>
    /// Phase 21.3 P3 (Data Security) Coordination: Detects sensitive data exposure patterns.
    /// When GCI0015 (unvalidated data write) + GCI0029 (PII in logging) both fire,
    /// it signals GDPR risk: unvalidated data + sensitive exposure = critical compliance violation.
    /// </summary>
    private void ApplyDataSecurityCoordination(List<ExpectedFinding> labels)
    {
        // ── Coordination Pattern 1: Unvalidated Write + PII Exposure ──
        // When GCI0015 (data integrity) + GCI0029 (PII in logs) both fire,
        // it's a critical security violation: unvalidated data flows into logging.

        var hasGci0015 = labels.Any(l => l.RuleId == "GCI0015" && l.ShouldTrigger);
        var hasGci0029 = labels.Any(l => l.RuleId == "GCI0029" && l.ShouldTrigger);

        if (hasGci0015 && hasGci0029)
        {
            // Boost GCI0015 (data integrity) - unvalidated data + PII = critical violation
            var gci0015Index = labels.FindIndex(l => l.RuleId == "GCI0015");
            if (gci0015Index >= 0)
            {
                var gci0015 = labels[gci0015Index];
                if (gci0015.ExpectedConfidence < 0.88)
                {
                    labels[gci0015Index] = new ExpectedFinding
                    {
                        RuleId = gci0015.RuleId,
                        ShouldTrigger = gci0015.ShouldTrigger,
                        ExpectedConfidence = 0.88,
                        Reason = "[coordination] Unvalidated write + GCI0029 PII exposure = GDPR compliance risk. Sensitive data flows without validation.",
                        LabelSource = gci0015.LabelSource,
                        IsInconclusive = gci0015.IsInconclusive,
                    };
                }
            }

            // Boost GCI0029 (PII in logs) - more serious when data is unvalidated
            var gci0029Index = labels.FindIndex(l => l.RuleId == "GCI0029");
            if (gci0029Index >= 0)
            {
                var gci0029 = labels[gci0029Index];
                if (gci0029.ExpectedConfidence < 0.82)
                {
                    labels[gci0029Index] = new ExpectedFinding
                    {
                        RuleId = gci0029.RuleId,
                        ShouldTrigger = gci0029.ShouldTrigger,
                        ExpectedConfidence = 0.82,
                        Reason = "[coordination] PII in logs + GCI0015 unvalidated write = data leakage. Sensitive data exposed without validation layer.",
                        LabelSource = gci0029.LabelSource,
                        IsInconclusive = gci0029.IsInconclusive,
                    };
                }
            }
        }

        // ── Coordination Pattern 2: Credentials + Data Integrity ──
        // When GCI0012 (hardcoded secrets) + GCI0015 (data integrity) both fire,
        // it's a severe security failure: credentials stored in unvalidated data path.

        var hasGci0012 = labels.Any(l => l.RuleId == "GCI0012" && l.ShouldTrigger);

        if (hasGci0012 && hasGci0015)
        {
            // Boost GCI0012 (secrets) - hardcoded credential in unvalidated data context is critical
            var gci0012Index = labels.FindIndex(l => l.RuleId == "GCI0012");
            if (gci0012Index >= 0)
            {
                var gci0012 = labels[gci0012Index];
                if (gci0012.ExpectedConfidence < 0.90)
                {
                    labels[gci0012Index] = new ExpectedFinding
                    {
                        RuleId = gci0012.RuleId,
                        ShouldTrigger = gci0012.ShouldTrigger,
                        ExpectedConfidence = 0.90,
                        Reason = "[coordination] Hardcoded credential + GCI0015 unvalidated write = critical exposure. Secrets in unvalidated data flow.",
                        LabelSource = gci0012.LabelSource,
                        IsInconclusive = gci0012.IsInconclusive,
                    };
                }
            }

            // Boost GCI0015 (data integrity) - data integrity failure with exposed secrets
            var gci0015Index2 = labels.FindIndex(l => l.RuleId == "GCI0015");
            if (gci0015Index2 >= 0)
            {
                var gci0015 = labels[gci0015Index2];
                if (gci0015.ExpectedConfidence < 0.86)
                {
                    labels[gci0015Index2] = new ExpectedFinding
                    {
                        RuleId = gci0015.RuleId,
                        ShouldTrigger = gci0015.ShouldTrigger,
                        ExpectedConfidence = 0.86,
                        Reason = "[coordination] Data integrity failure + GCI0012 hardcoded credentials = credential exposure. Secrets stored in unvalidated path.",
                        LabelSource = gci0015.LabelSource,
                        IsInconclusive = gci0015.IsInconclusive,
                    };
                }
            }
        }
    }

    /// <summary>
    /// Phase 23.1 P4 (Performance & GC) Coordination: Detects GC pressure patterns.
    /// When GCI0044 (GC pressure from blocking) and GCI0035 (excessive allocation) co-occur,
    /// boost both to reflect compound performance risk.
    /// </summary>
    private static void ApplyPhase23P4PerformanceCoordination(List<ExpectedFinding> labels)
    {
        // Find GCI0044 (GC pressure from blocking calls)
        var gci0044Index = labels.FindIndex(l => l.RuleId == "GCI0044");
        var gci0044 = gci0044Index >= 0 ? labels[gci0044Index] : null;

        // Find GCI0035 (excessive allocation / large collections)
        var gci0035Index = labels.FindIndex(l => l.RuleId == "GCI0035");
        var gci0035 = gci0035Index >= 0 ? labels[gci0035Index] : null;

        // Both rules detected with sufficient confidence: compound risk
        if (gci0044?.ShouldTrigger == true && gci0044.ExpectedConfidence >= 0.50 &&
            gci0035?.ShouldTrigger == true && gci0035.ExpectedConfidence >= 0.50)
        {
            // Boost GCI0044: blocking calls increase GC pressure under memory load
            // From 0.60 → 0.78 (+30% boost for compound risk)
            if (gci0044Index >= 0 && gci0044.ExpectedConfidence < 0.78)
            {
                labels[gci0044Index] = new ExpectedFinding
                {
                    RuleId = gci0044.RuleId,
                    ShouldTrigger = gci0044.ShouldTrigger,
                    ExpectedConfidence = 0.78,
                    Reason = "[coordination] GCI0044 GC pressure + GCI0035 excessive allocation = performance cliff. Blocking calls + memory load = Gen2 stalls.",
                    LabelSource = gci0044.LabelSource,
                    IsInconclusive = gci0044.IsInconclusive,
                };
            }

            // Boost GCI0035: allocation pressure compounds blocking/GC issues
            // From 0.65 → 0.85 (+31% boost for compound risk)
            if (gci0035Index >= 0 && gci0035.ExpectedConfidence < 0.85)
            {
                labels[gci0035Index] = new ExpectedFinding
                {
                    RuleId = gci0035.RuleId,
                    ShouldTrigger = gci0035.ShouldTrigger,
                    ExpectedConfidence = 0.85,
                    Reason = "[coordination] GCI0035 excessive allocation + GCI0044 GC pressure = memory stalls. Large collections + blocking calls trigger Gen2 collections.",
                    LabelSource = gci0035.LabelSource,
                    IsInconclusive = gci0035.IsInconclusive,
                };
            }
        }
    }

    /// <summary>
    /// Phase 23.2 P5 (Serialization Safety) Coordination: Detects unsafe deserialization patterns.
    /// When GCI0039 (unsafe HttpClient) and GCI0048 (insecure serialization) co-occur,
    /// boost both to reflect compound security risk (unvalidated external input + unsafe deserialization = RCE).
    /// </summary>
    private static void ApplyPhase23P5SerializationCoordination(List<ExpectedFinding> labels)
    {
        // Find GCI0039 (unsafe HttpClient without factory or timeouts)
        var gci0039Index = labels.FindIndex(l => l.RuleId == "GCI0039");
        var gci0039 = gci0039Index >= 0 ? labels[gci0039Index] : null;

        // Find GCI0048 (insecure deserialization with TypeNameHandling)
        var gci0048Index = labels.FindIndex(l => l.RuleId == "GCI0048");
        var gci0048 = gci0048Index >= 0 ? labels[gci0048Index] : null;

        // Both rules detected with sufficient confidence: compound security risk
        if (gci0039?.ShouldTrigger == true && gci0039.ExpectedConfidence >= 0.55 &&
            gci0048?.ShouldTrigger == true && gci0048.ExpectedConfidence >= 0.60)
        {
            // Boost GCI0039: unsafe HTTP client compounds deserialization risk
            // From 0.70 → 0.90 (+29% boost for remote code execution risk)
            if (gci0039Index >= 0 && gci0039.ExpectedConfidence < 0.90)
            {
                labels[gci0039Index] = new ExpectedFinding
                {
                    RuleId = gci0039.RuleId,
                    ShouldTrigger = gci0039.ShouldTrigger,
                    ExpectedConfidence = 0.90,
                    Reason = "[coordination] GCI0039 unsafe HttpClient + GCI0048 insecure deserialization = RCE risk. Untrusted external data flows directly to unsafe deserialization.",
                    LabelSource = gci0039.LabelSource,
                    IsInconclusive = gci0039.IsInconclusive,
                };
            }

            // Boost GCI0048: unsafe deserialization from unvalidated sources
            // From 0.65 → 0.92 (+42% boost for critical security risk)
            if (gci0048Index >= 0 && gci0048.ExpectedConfidence < 0.92)
            {
                labels[gci0048Index] = new ExpectedFinding
                {
                    RuleId = gci0048.RuleId,
                    ShouldTrigger = gci0048.ShouldTrigger,
                    ExpectedConfidence = 0.92,
                    Reason = "[coordination] GCI0048 insecure deserialization + GCI0039 unsafe HttpClient = RCE. Unvalidated remote data deserialized with TypeNameHandling enabled.",
                    LabelSource = gci0048.LabelSource,
                    IsInconclusive = gci0048.IsInconclusive,
                };
            }
        }
    }

    /// <summary>
    /// Phase 23.3 P6 (Dependency Injection) Coordination: Detects service locator + async scope issues.
    /// When GCI0045 (service locator) and GCI0016 (async violations) co-occur,
    /// boost both to reflect compound risk of scope mismatch causing deadlocks in DI container.
    /// </summary>
    private static void ApplyPhase23P6DependencyInjectionCoordination(List<ExpectedFinding> labels)
    {
        // Find GCI0045 (service locator anti-pattern)
        var gci0045Index = labels.FindIndex(l => l.RuleId == "GCI0045");
        var gci0045 = gci0045Index >= 0 ? labels[gci0045Index] : null;

        // Find GCI0016 (async violations / blocking calls in async context)
        var gci0016Index = labels.FindIndex(l => l.RuleId == "GCI0016");
        var gci0016 = gci0016Index >= 0 ? labels[gci0016Index] : null;

        // Both rules detected with sufficient confidence: scope mismatch risk
        if (gci0045?.ShouldTrigger == true && gci0045.ExpectedConfidence >= 0.60 &&
            gci0016?.ShouldTrigger == true && gci0016.ExpectedConfidence >= 0.55)
        {
            // Boost GCI0045: service locator in async context increases scope risk
            // From 0.60 → 0.82 (+37% boost for deadlock risk)
            if (gci0045Index >= 0 && gci0045.ExpectedConfidence < 0.82)
            {
                labels[gci0045Index] = new ExpectedFinding
                {
                    RuleId = gci0045.RuleId,
                    ShouldTrigger = gci0045.ShouldTrigger,
                    ExpectedConfidence = 0.82,
                    Reason = "[coordination] GCI0045 service locator + GCI0016 async violations = deadlock. Scope mismatch when service locator used in async context.",
                    LabelSource = gci0045.LabelSource,
                    IsInconclusive = gci0045.IsInconclusive,
                };
            }

            // Boost GCI0016: blocking calls in async context with service locator
            // From 0.65 → 0.88 (+35% boost for scope-related deadlock)
            if (gci0016Index >= 0 && gci0016.ExpectedConfidence < 0.88)
            {
                labels[gci0016Index] = new ExpectedFinding
                {
                    RuleId = gci0016.RuleId,
                    ShouldTrigger = gci0016.ShouldTrigger,
                    ExpectedConfidence = 0.88,
                    Reason = "[coordination] GCI0016 blocking async + GCI0045 service locator = deadlock. Blocking calls on scoped service obtained from locator.",
                    LabelSource = gci0016.LabelSource,
                    IsInconclusive = gci0016.IsInconclusive,
                };
            }
        }
    }
}

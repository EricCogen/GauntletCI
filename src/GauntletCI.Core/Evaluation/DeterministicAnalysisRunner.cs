// SPDX-License-Identifier: Elastic-2.0
// Copyright (c) Eric Cogen. All rights reserved.

using System.Text.RegularExpressions;
using GauntletCI.Core.Models;

namespace GauntletCI.Core.Evaluation;

public sealed class DeterministicAnalysisRunner
{
    private static readonly HashSet<string> NonCodeLanguages = new(StringComparer.OrdinalIgnoreCase)
    {
        "md",
        "txt",
        "rst",
        "adoc",
        "json",
        "yaml",
        "yml",
        "toml",
        "lock",
        "csv",
        "png",
        "jpg",
        "jpeg",
        "gif",
        "svg",
    };

    // Captures both old-file (-) and new-file (+) starting line numbers from a hunk header.
    private static readonly Regex HunkHeaderPattern = new(
        @"@@ -(?<oldline>\d+)(?:,\d+)? \+(?<newline>\d+)(?:,\d+)? @@",
        RegexOptions.Compiled);

    private static readonly DeterministicSignalSpec WeakCoverageSignal = new(
        SignalId: "DET-COV-001",
        RuleId: "GCI005",
        RuleName: "Test Coverage Relevance",
        Severity: "medium",
        Confidence: "Medium",
        FindingText: "Code behavior changed without meaningful new test assertions on the changed path.",
        WhyItMatters: "Behavior can regress while tests still pass when new execution paths are not asserted.",
        SuggestedAction: "Add assertions that exercise and verify the changed runtime path.",
        Kind: DeterministicSignalKind.Metadata,
        Pattern: null,
        PatternOptions: RegexOptions.None,
        ExcludedFilePathPatterns: [],
        ExcludedSnippetPatterns: []);

    private static readonly IReadOnlyList<LineMatchSignal> AddedLineSignals =
        CreateSignalSpecsForKind(CreateAddedLineSignalSpecs(), DeterministicSignalKind.AddedLineRegex);

    private static readonly IReadOnlyList<LineMatchSignal> RemovedLineSignals =
        CreateSignalSpecsForKind(CreateRemovedLineSignalSpecs(), DeterministicSignalKind.RemovedLineRegex);

    public IReadOnlyList<Finding> Analyze(string diff, DiffMetadata metadata)
    {
        List<Finding> findings = [];

        if (ShouldFlagWeakCoverage(metadata))
        {
            findings.Add(CreateFinding(
                WeakCoverageSignal,
                $"diff metadata: files_changed={metadata.FilesChanged}, test_files_changed={metadata.TestFilesChanged}, assertion_like_test_lines_added={metadata.TestAssertionLinesAdded}"));
        }

        foreach (LineMatchSignal signal in AddedLineSignals)
        {
            LineMatch? match = FindFirstMatchingLine(diff, signal, '+');
            if (match is not null)
            {
                findings.Add(CreateFinding(signal.Spec, $"{match.FilePath}:{match.LineNumber} {match.Snippet}"));
            }
        }

        foreach (LineMatchSignal signal in RemovedLineSignals)
        {
            LineMatch? match = FindFirstMatchingLine(diff, signal, '-');
            if (match is not null)
            {
                findings.Add(CreateFinding(signal.Spec, $"{match.FilePath}:{match.LineNumber} {match.Snippet}"));
            }
        }

        return findings;
    }

    private static Finding CreateFinding(DeterministicSignalSpec spec, string evidence)
    {
        return new Finding(
            RuleId: spec.RuleId,
            RuleName: spec.RuleName,
            Severity: spec.Severity,
            FindingText: spec.FindingText,
            Evidence: $"signal={spec.SignalId}; {evidence}",
            WhyItMatters: spec.WhyItMatters,
            SuggestedAction: spec.SuggestedAction,
            Confidence: spec.Confidence);
    }

    private static bool ShouldFlagWeakCoverage(DiffMetadata metadata)
    {
        if (!IsLikelyCodeChange(metadata))
        {
            return false;
        }

        bool productionCodeTouched = metadata.FilesChanged > metadata.TestFilesChanged;
        if (!productionCodeTouched)
        {
            return false;
        }

        if (metadata.TestFilesChanged == 0)
        {
            return true;
        }

        return metadata.TestsChangedWithoutAssertions;
    }

    private static bool IsLikelyCodeChange(DiffMetadata metadata)
    {
        if (metadata.LinesAdded + metadata.LinesRemoved == 0)
        {
            return false;
        }

        return metadata.Languages.Any(language => !NonCodeLanguages.Contains(language));
    }

    // Scans diff lines with the given prefix ('+' for added, '-' for removed) and returns the
    // first line matching the signal's pattern, respecting file path inclusion/exclusion filters.
    private static LineMatch? FindFirstMatchingLine(string diff, LineMatchSignal signal, char linePrefix)
    {
        string currentFile = "unknown";
        int currentTrackingLine = 0;
        bool insideHunk = false;
        bool currentFileExcluded = false;
        bool currentFileIncluded = true;

        char otherPrefix = linePrefix == '+' ? '-' : '+';
        string ownHeaderPrefix = new string(linePrefix, 3);   // "+++" or "---"
        string otherHeaderPrefix = new string(otherPrefix, 3);
        string hunkGroup = linePrefix == '+' ? "newline" : "oldline";

        foreach (string raw in diff.Split('\n'))
        {
            string line = raw.TrimEnd('\r');

            if (line.StartsWith("diff --git ", StringComparison.Ordinal))
            {
                currentFile = ParseFilePath(line);
                insideHunk = false;
                currentTrackingLine = 0;
                currentFileExcluded = signal.ExcludedFilePathPatterns.Any(e => e.IsMatch(currentFile));
                currentFileIncluded = signal.IncludedFilePathPatterns.Count == 0 ||
                                      signal.IncludedFilePathPatterns.Any(e => e.IsMatch(currentFile));
                continue;
            }

            Match hunkHeader = HunkHeaderPattern.Match(line);
            if (hunkHeader.Success)
            {
                insideHunk = true;
                currentTrackingLine = int.Parse(hunkHeader.Groups[hunkGroup].Value);
                continue;
            }

            if (!insideHunk)
            {
                continue;
            }

            if (line.StartsWith(linePrefix) && !line.StartsWith(ownHeaderPrefix, StringComparison.Ordinal))
            {
                string snippet = line[1..].Trim();
                if (!currentFileExcluded &&
                    currentFileIncluded &&
                    !IsCommentLike(snippet) &&
                    !signal.ExcludedSnippetPatterns.Any(e => e.IsMatch(snippet)) &&
                    signal.Pattern.IsMatch(snippet))
                {
                    return new LineMatch(currentFile, currentTrackingLine, snippet.Length > 160 ? snippet[..160] : snippet);
                }

                currentTrackingLine++;
                continue;
            }

            if (line.StartsWith(otherPrefix) && !line.StartsWith(otherHeaderPrefix, StringComparison.Ordinal))
            {
                // Other side's line — don't advance our tracking line number.
                continue;
            }

            if (line.StartsWith('\\'))
            {
                // "\ No newline at end of file" — git meta-marker, not real content.
                continue;
            }

            currentTrackingLine++; // Context line — both sides advance.
        }

        return null;
    }

    // Matches "diff --git a/<path> b/<path>" where both paths are identical (standard diff).
    // The backreference (\1) ensures we find the correct split point even when paths contain spaces.
    private static readonly Regex DiffGitFilePathPattern =
        new(@"^diff --git a/(.+) b/\1$", RegexOptions.Compiled);

    private static string ParseFilePath(string diffHeader)
    {
        Match m = DiffGitFilePathPattern.Match(diffHeader);
        if (m.Success)
        {
            return m.Groups[1].Value;
        }

        // Fallback for renames/copies: the new path follows the last " b/" token.
        int bIdx = diffHeader.LastIndexOf(" b/", StringComparison.Ordinal);
        if (bIdx >= 0)
        {
            return diffHeader[(bIdx + 3)..];
        }

        return "unknown";
    }

    private static bool IsCommentLike(string snippet)
    {
        if (snippet.StartsWith("//", StringComparison.Ordinal)) return true;
        if (snippet.StartsWith("/*", StringComparison.Ordinal)) return true;
        if (snippet.StartsWith('*')) return true;
        // Treat '#' as a comment character (Python/Shell/YAML) but not for C# preprocessor directives.
        if (snippet.StartsWith('#') &&
            !snippet.StartsWith("#pragma", StringComparison.OrdinalIgnoreCase) &&
            !snippet.StartsWith("#nullable", StringComparison.OrdinalIgnoreCase) &&
            !snippet.StartsWith("#if", StringComparison.OrdinalIgnoreCase) &&
            !snippet.StartsWith("#else", StringComparison.OrdinalIgnoreCase) &&
            !snippet.StartsWith("#endif", StringComparison.OrdinalIgnoreCase) &&
            !snippet.StartsWith("#region", StringComparison.OrdinalIgnoreCase) &&
            !snippet.StartsWith("#endregion", StringComparison.OrdinalIgnoreCase) &&
            !snippet.StartsWith("#define", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static IReadOnlyList<DeterministicSignalSpec> CreateAddedLineSignalSpecs()
    {
        IReadOnlyList<string> commonPathExclusions =
        [
            @"(^|/)(obj|bin|dist|coverage|node_modules|vendor|\.git)/",
            @"(^|/)migrations?/.*snapshot"
        ];
        IReadOnlyList<string> testAndBenchmarkPathExclusions =
        [
            @"(^|/)(test|tests|benchmark|benchmarks?)/"
        ];
        IReadOnlyList<string> desktopUiPathExclusions =
        [
            @"\.xaml\.cs$",
            @"(^|/)(winforms|wpf|maui)/"
        ];
        IReadOnlyList<string> testFileInclusionPatterns =
        [
            @"(^|/)(test|tests)/",
            @"[Tt]ests?\.cs$",
            @"(^|/)[^/]*[Ss]pecs?\.cs$"
        ];
        IReadOnlyList<string> placeholderSecretSnippetExclusions =
        [
            @"\b(example|sample|placeholder|dummy|changeme)\b",
            @"\b(your[_\-\s]?(api[_-]?key|secret|token|password))\b",
            @"\b(fake[-_ ]?token|test[-_ ]?key)\b"
        ];

        return
        [
            new DeterministicSignalSpec(
                SignalId: "DET-CONC-001",
                RuleId: "GCI016",
                RuleName: "Concurrency and State Risk",
                Severity: "high",
                Confidence: "High",
                FindingText: "Blocking async/synchronization pattern was introduced in changed code.",
                WhyItMatters: "Sync-over-async patterns can deadlock request flows or starve concurrency under load.",
                SuggestedAction: "Use async/await end-to-end and avoid blocking waits like .Result, .Wait(), and GetAwaiter().GetResult().",
                Kind: DeterministicSignalKind.AddedLineRegex,
                Pattern: @"(\.Result\b|GetAwaiter\(\)\.GetResult\(|\.Wait\(\)|Thread\.Sleep\(|time\.sleep\()",
                PatternOptions: RegexOptions.Compiled | RegexOptions.IgnoreCase,
                ExcludedFilePathPatterns: commonPathExclusions,
                ExcludedSnippetPatterns: []),
            new DeterministicSignalSpec(
                SignalId: "DET-CONC-002",
                RuleId: "GCI016",
                RuleName: "Concurrency and State Risk",
                Severity: "high",
                Confidence: "Medium",
                FindingText: "An async void method declaration was introduced in changed runtime code.",
                WhyItMatters: "async void methods cannot be awaited and can surface exceptions outside normal async request flow.",
                SuggestedAction: "Return Task/Task<T> for async methods so errors and completion are observable by callers.",
                Kind: DeterministicSignalKind.AddedLineRegex,
                Pattern: @"\basync\s+void\s+[A-Za-z_][A-Za-z0-9_]*\s*\(",
                PatternOptions: RegexOptions.Compiled | RegexOptions.IgnoreCase,
                ExcludedFilePathPatterns: commonPathExclusions.Concat(testAndBenchmarkPathExclusions).Concat(desktopUiPathExclusions).ToArray(),
                ExcludedSnippetPatterns: []),
            new DeterministicSignalSpec(
                SignalId: "DET-ROLL-001",
                RuleId: "GCI014",
                RuleName: "Rollback Safety",
                Severity: "high",
                Confidence: "High",
                FindingText: "Destructive schema change was introduced without proof of rollback-safe recovery.",
                WhyItMatters: "Drops can permanently remove production data and make rollback incomplete or unsafe.",
                SuggestedAction: "Use staged/backfill migration strategy and document an explicit rollback path before destructive schema removal.",
                Kind: DeterministicSignalKind.AddedLineRegex,
                Pattern: @"\b(DROP\s+COLUMN|DROP\s+TABLE|remove_column\b|drop_table\b)\b",
                PatternOptions: RegexOptions.Compiled | RegexOptions.IgnoreCase,
                ExcludedFilePathPatterns: commonPathExclusions,
                ExcludedSnippetPatterns: []),
            new DeterministicSignalSpec(
                SignalId: "DET-SEC-001",
                RuleId: "GCI012",
                RuleName: "Security Risk",
                Severity: "high",
                Confidence: "High",
                FindingText: "A credential-like literal was added directly in code/config changes.",
                WhyItMatters: "Hardcoded credentials can leak through source control and are difficult to rotate safely.",
                SuggestedAction: "Move secrets to secure environment/config providers and inject at runtime.",
                Kind: DeterministicSignalKind.AddedLineRegex,
                Pattern: @"(\b(api[_-]?key|secret|token|password|passwd|connectionstring)\b.{0,24}[:=]\s*[""'][^""'\s][^""']{2,}[""']|\bAuthorization\s*[:=]\s*[""']?Bearer\s+[A-Za-z0-9\-\._]+)",
                PatternOptions: RegexOptions.Compiled | RegexOptions.IgnoreCase,
                ExcludedFilePathPatterns: commonPathExclusions,
                ExcludedSnippetPatterns: placeholderSecretSnippetExclusions),
            new DeterministicSignalSpec(
                SignalId: "DET-SEC-002",
                RuleId: "GCI012",
                RuleName: "Security Risk",
                Severity: "high",
                Confidence: "Medium",
                FindingText: "Dynamic SQL appears to be built via string concatenation in changed code.",
                WhyItMatters: "Concatenating user-influenced values into SQL strings can open direct injection paths.",
                SuggestedAction: "Use parameterized queries or prepared statements for all dynamic values.",
                Kind: DeterministicSignalKind.AddedLineRegex,
                Pattern: @"(""[^""]*(SELECT|INSERT|UPDATE|DELETE)\b[^""]*""\s*\+\s*[A-Za-z_][A-Za-z0-9_]*|\b(ExecuteSqlRaw|ExecuteSqlCommand|SqlCommand)\s*\([^\n]*\+[^\n]*\))",
                PatternOptions: RegexOptions.Compiled | RegexOptions.IgnoreCase,
                ExcludedFilePathPatterns: commonPathExclusions,
                ExcludedSnippetPatterns: []),
            new DeterministicSignalSpec(
                SignalId: "DET-SEC-003",
                RuleId: "GCI012",
                RuleName: "Security Risk",
                Severity: "high",
                Confidence: "High",
                FindingText: "Weak cryptographic primitive usage was introduced in changed code.",
                WhyItMatters: "Obsolete algorithms can be vulnerable to practical attacks and compromise integrity guarantees.",
                SuggestedAction: "Use approved modern primitives (for example SHA-256/512 and authenticated encryption).",
                Kind: DeterministicSignalKind.AddedLineRegex,
                Pattern: @"\b(MD5(Create)?|SHA1(Create)?|DES(Create)?|DESCryptoServiceProvider)\b",
                PatternOptions: RegexOptions.Compiled | RegexOptions.IgnoreCase,
                ExcludedFilePathPatterns: commonPathExclusions,
                ExcludedSnippetPatterns: []),
            new DeterministicSignalSpec(
                SignalId: "DET-SEC-004",
                RuleId: "GCI012",
                RuleName: "Security Risk",
                Severity: "medium",
                Confidence: "Medium",
                FindingText: "Overly permissive CORS policy call was introduced in changed code.",
                WhyItMatters: "AllowAny* CORS policies can unintentionally expose privileged endpoints to untrusted origins.",
                SuggestedAction: "Scope CORS to explicit origins, methods, and headers appropriate for the deployment surface.",
                Kind: DeterministicSignalKind.AddedLineRegex,
                Pattern: @"\bAllowAny(Origin|Method|Header)\s*\(",
                PatternOptions: RegexOptions.Compiled | RegexOptions.IgnoreCase,
                ExcludedFilePathPatterns: commonPathExclusions,
                ExcludedSnippetPatterns: []),
            new DeterministicSignalSpec(
                SignalId: "DET-SEC-005",
                RuleId: "GCI012",
                RuleName: "Security Risk",
                Severity: "high",
                Confidence: "High",
                FindingText: "TLS certificate validation appears to be explicitly bypassed in changed code.",
                WhyItMatters: "Disabling certificate validation can allow man-in-the-middle interception of sensitive traffic.",
                SuggestedAction: "Remove permissive certificate callbacks and rely on trusted certificates and proper validation.",
                Kind: DeterministicSignalKind.AddedLineRegex,
                Pattern: @"(\bDangerousAcceptAnyServerCertificateValidator\b|\bServerCertificateCustomValidationCallback\s*=\s*(?:[^;]*=>\s*true\b|delegate\s*\([^)]*\)\s*\{\s*return\s+true\s*;\s*\}))",
                PatternOptions: RegexOptions.Compiled | RegexOptions.IgnoreCase,
                ExcludedFilePathPatterns: commonPathExclusions.Concat(testAndBenchmarkPathExclusions).ToArray(),
                ExcludedSnippetPatterns: []),
            new DeterministicSignalSpec(
                SignalId: "DET-SEC-006",
                RuleId: "GCI012",
                RuleName: "Security Risk",
                Severity: "high",
                Confidence: "High",
                FindingText: "Legacy insecure deserialization primitive usage was introduced in changed code.",
                WhyItMatters: "BinaryFormatter-style serializers have known exploitation paths when processing untrusted payloads.",
                SuggestedAction: "Use safe serializers such as System.Text.Json with explicit trusted type boundaries.",
                Kind: DeterministicSignalKind.AddedLineRegex,
                Pattern: @"\b(BinaryFormatter|NetDataContractSerializer)\b",
                PatternOptions: RegexOptions.Compiled | RegexOptions.IgnoreCase,
                ExcludedFilePathPatterns: commonPathExclusions.Concat(testAndBenchmarkPathExclusions).ToArray(),
                ExcludedSnippetPatterns: []),
            new DeterministicSignalSpec(
                SignalId: "DET-SEC-007",
                RuleId: "GCI012",
                RuleName: "Security Risk",
                Severity: "high",
                Confidence: "Medium",
                FindingText: "JWT token validation appears to disable key integrity or lifetime checks.",
                WhyItMatters: "Disabled token validation can allow forged or expired tokens to be accepted.",
                SuggestedAction: "Keep ValidateIssuerSigningKey and ValidateLifetime enabled outside tightly controlled test-only code.",
                Kind: DeterministicSignalKind.AddedLineRegex,
                Pattern: @"\b(ValidateLifetime|ValidateIssuerSigningKey)\s*=\s*false\b",
                PatternOptions: RegexOptions.Compiled | RegexOptions.IgnoreCase,
                ExcludedFilePathPatterns: commonPathExclusions.Concat(testAndBenchmarkPathExclusions).ToArray(),
                ExcludedSnippetPatterns: []),
            new DeterministicSignalSpec(
                SignalId: "DET-PERF-001",
                RuleId: "GCI011",
                RuleName: "Performance Risk",
                Severity: "medium",
                Confidence: "Medium",
                FindingText: "A new HttpClient instance is created in changed code instead of using pooled/managed lifetime.",
                WhyItMatters: "Per-call HttpClient instantiation can increase connection churn and cause socket exhaustion under load.",
                SuggestedAction: "Use DI-managed HttpClientFactory or a long-lived shared HttpClient instance.",
                Kind: DeterministicSignalKind.AddedLineRegex,
                Pattern: @"\bnew\s+HttpClient\s*\(",
                PatternOptions: RegexOptions.Compiled | RegexOptions.IgnoreCase,
                ExcludedFilePathPatterns: commonPathExclusions.Concat(testAndBenchmarkPathExclusions).ToArray(),
                ExcludedSnippetPatterns: []),
            new DeterministicSignalSpec(
                SignalId: "DET-PERF-002",
                RuleId: "GCI011",
                RuleName: "Performance Risk",
                Severity: "low",
                Confidence: "Low",
                FindingText: "A LINQ .Count() call is used only for empty/non-empty checks in changed code.",
                WhyItMatters: "Using Count() for emptiness checks can force unnecessary enumeration on non-materialized collections.",
                SuggestedAction: "Prefer .Any() for sequence emptiness checks or .Count property for materialized collections.",
                Kind: DeterministicSignalKind.AddedLineRegex,
                Pattern: @"\.Count\(\)\s*(>|!=|==|<=|>=)\s*0\b",
                PatternOptions: RegexOptions.Compiled | RegexOptions.IgnoreCase,
                ExcludedFilePathPatterns: commonPathExclusions,
                ExcludedSnippetPatterns: []),
            new DeterministicSignalSpec(
                SignalId: "DET-REL-001",
                RuleId: "GCI007",
                RuleName: "Error Handling Integrity",
                Severity: "medium",
                Confidence: "High",
                FindingText: "An empty catch block was introduced in changed code.",
                WhyItMatters: "Silent exception swallowing hides failures and degrades runtime diagnosability.",
                SuggestedAction: "Handle the exception explicitly with logging/telemetry or rethrow to preserve failure visibility.",
                Kind: DeterministicSignalKind.AddedLineRegex,
                Pattern: @"\bcatch\s*\([^)]*\)\s*\{\s*\}",
                PatternOptions: RegexOptions.Compiled | RegexOptions.IgnoreCase,
                ExcludedFilePathPatterns: commonPathExclusions,
                ExcludedSnippetPatterns: []),
            new DeterministicSignalSpec(
                SignalId: "DET-REL-002",
                RuleId: "GCI007",
                RuleName: "Error Handling Integrity",
                Severity: "medium",
                Confidence: "High",
                FindingText: "A stale exception rethrow pattern (throw ex;) was introduced in changed code.",
                WhyItMatters: "Rethrowing the caught variable resets stack context and obscures root-cause debugging.",
                SuggestedAction: "Use bare throw; within catch blocks to preserve original stack trace information.",
                Kind: DeterministicSignalKind.AddedLineRegex,
                Pattern: @"\bthrow\s+(ex|exception)\s*;",
                PatternOptions: RegexOptions.Compiled | RegexOptions.IgnoreCase,
                ExcludedFilePathPatterns: commonPathExclusions,
                ExcludedSnippetPatterns: []),
            new DeterministicSignalSpec(
                SignalId: "DET-REL-003",
                RuleId: "GCI007",
                RuleName: "Error Handling Integrity",
                Severity: "low",
                Confidence: "Low",
                FindingText: "A broad catch(Exception) handler was introduced in changed code.",
                WhyItMatters: "Overly broad exception catches can mask programming faults and hide unexpected fatal conditions.",
                SuggestedAction: "Catch specific exception types and rethrow unknown/unrecoverable failures.",
                Kind: DeterministicSignalKind.AddedLineRegex,
                Pattern: @"\bcatch\s*\(\s*Exception(\s+[A-Za-z_][A-Za-z0-9_]*)?\s*\)",
                PatternOptions: RegexOptions.Compiled | RegexOptions.IgnoreCase,
                ExcludedFilePathPatterns: commonPathExclusions,
                ExcludedSnippetPatterns: []),
            new DeterministicSignalSpec(
                SignalId: "DET-API-001",
                RuleId: "GCI010",
                RuleName: "Hardcoding and Configuration",
                Severity: "low",
                Confidence: "Low",
                FindingText: "Development-mode environment branching was introduced in changed runtime code.",
                WhyItMatters: "Environment-specific debug branching can drift into production paths and create brittle configuration behavior.",
                SuggestedAction: "Guard debug-only behavior behind explicit, auditable configuration and ensure production defaults are safe.",
                Kind: DeterministicSignalKind.AddedLineRegex,
                Pattern: @"\b(IsDevelopment\s*\(|ASPNETCORE_ENVIRONMENT[^\n]*Development)",
                PatternOptions: RegexOptions.Compiled | RegexOptions.IgnoreCase,
                ExcludedFilePathPatterns: commonPathExclusions,
                IncludedFilePathPatterns: [],
                ExcludedSnippetPatterns: []),

            // --- Concurrency (new) ---
            new DeterministicSignalSpec(
                SignalId: "DET-CONC-003",
                RuleId: "GCI016",
                RuleName: "Concurrency and State Risk",
                Severity: "medium",
                Confidence: "Medium",
                FindingText: "A [ThreadStatic] field attribute was introduced in changed code.",
                WhyItMatters: "[ThreadStatic] fields are not initialised per-thread by the instance initialiser; default values silently differ between threads and can cause subtle data corruption.",
                SuggestedAction: "Use ThreadLocal<T> instead, which supports per-thread factory initialisation.",
                Kind: DeterministicSignalKind.AddedLineRegex,
                Pattern: @"\[ThreadStatic\b",
                PatternOptions: RegexOptions.Compiled | RegexOptions.IgnoreCase,
                ExcludedFilePathPatterns: commonPathExclusions.Concat(testAndBenchmarkPathExclusions).ToArray(),
                IncludedFilePathPatterns: [],
                ExcludedSnippetPatterns: []),

            // --- Disposal / Resource Management (new) ---
            new DeterministicSignalSpec(
                SignalId: "DET-DISP-002",
                RuleId: "GCI007",
                RuleName: "Error Handling Integrity",
                Severity: "medium",
                Confidence: "Medium",
                FindingText: "GC.SuppressFinalize was introduced without a visible Dispose implementation on the same type.",
                WhyItMatters: "Calling GC.SuppressFinalize outside the standard Dispose pattern can suppress finalizer cleanup without guaranteeing managed resources are released.",
                SuggestedAction: "Ensure GC.SuppressFinalize is called only inside a Dispose(bool) method that fully releases all managed and unmanaged resources.",
                Kind: DeterministicSignalKind.AddedLineRegex,
                Pattern: @"\bGC\.SuppressFinalize\s*\(",
                PatternOptions: RegexOptions.Compiled | RegexOptions.IgnoreCase,
                ExcludedFilePathPatterns: commonPathExclusions.Concat(testAndBenchmarkPathExclusions).ToArray(),
                IncludedFilePathPatterns: [],
                ExcludedSnippetPatterns: []),

            // --- Performance (new) ---
            new DeterministicSignalSpec(
                SignalId: "DET-PERF-004",
                RuleId: "GCI011",
                RuleName: "Performance Risk",
                Severity: "low",
                Confidence: "Low",
                FindingText: "MethodImplOptions.NoInlining was introduced in changed code.",
                WhyItMatters: "Preventing inlining on hot-path methods can measurably increase call overhead and reduce JIT optimisation opportunities.",
                SuggestedAction: "Remove NoInlining unless profiling confirms inlining causes a measurable problem (e.g. bloated code size or unwanted tail-call suppression).",
                Kind: DeterministicSignalKind.AddedLineRegex,
                Pattern: @"MethodImplOptions\.NoInlining",
                PatternOptions: RegexOptions.Compiled | RegexOptions.IgnoreCase,
                ExcludedFilePathPatterns: commonPathExclusions,
                IncludedFilePathPatterns: [],
                ExcludedSnippetPatterns: []),

            // --- API Contract (new) ---
            new DeterministicSignalSpec(
                SignalId: "DET-API-004",
                RuleId: "GCI004",
                RuleName: "Breaking Change Risk",
                Severity: "medium",
                Confidence: "Medium",
                FindingText: "A sealed class or record was introduced in changed code.",
                WhyItMatters: "Sealing a type that callers could previously subclass is a binary and source-level breaking change for consumers.",
                SuggestedAction: "Confirm no external or internal consumers subclass this type before sealing, or introduce the restriction in a major version bump.",
                Kind: DeterministicSignalKind.AddedLineRegex,
                Pattern: @"\bsealed\s+(class|record)\b",
                PatternOptions: RegexOptions.Compiled | RegexOptions.IgnoreCase,
                ExcludedFilePathPatterns: commonPathExclusions.Concat(testAndBenchmarkPathExclusions).ToArray(),
                IncludedFilePathPatterns: [],
                ExcludedSnippetPatterns: []),

            // --- Testing (new, test-files only) ---
            new DeterministicSignalSpec(
                SignalId: "DET-TEST-002",
                RuleId: "GCI005",
                RuleName: "Test Coverage Relevance",
                Severity: "medium",
                Confidence: "Medium",
                FindingText: "A test skip/ignore attribute was introduced in changed test code.",
                WhyItMatters: "Silently skipped tests stop providing the coverage guarantee they were written to give without any visible failure.",
                SuggestedAction: "Remove the skip/ignore attribute or replace the test with one that reliably verifies the intended behaviour.",
                Kind: DeterministicSignalKind.AddedLineRegex,
                Pattern: @"\[(Ignore|Skip)\b",
                PatternOptions: RegexOptions.Compiled | RegexOptions.IgnoreCase,
                ExcludedFilePathPatterns: commonPathExclusions,
                IncludedFilePathPatterns: testFileInclusionPatterns,
                ExcludedSnippetPatterns: []),
            new DeterministicSignalSpec(
                SignalId: "DET-TEST-003",
                RuleId: "GCI005",
                RuleName: "Test Coverage Relevance",
                Severity: "low",
                Confidence: "Low",
                FindingText: "A try-catch block was introduced inside a test method.",
                WhyItMatters: "Swallowing exceptions inside tests allows failures to pass silently; the test framework should surface exceptions as failures.",
                SuggestedAction: "Remove the try-catch and let the test framework catch the exception, or use Assert.Throws to assert expected exceptions explicitly.",
                Kind: DeterministicSignalKind.AddedLineRegex,
                Pattern: @"\btry\s*\{",
                PatternOptions: RegexOptions.Compiled | RegexOptions.IgnoreCase,
                ExcludedFilePathPatterns: commonPathExclusions,
                IncludedFilePathPatterns: testFileInclusionPatterns,
                ExcludedSnippetPatterns: []),

            // --- Security (new) ---
            new DeterministicSignalSpec(
                SignalId: "DET-SEC-009",
                RuleId: "GCI012",
                RuleName: "Security Risk",
                Severity: "high",
                Confidence: "High",
                FindingText: "The [SuppressUnmanagedCodeSecurity] attribute was introduced in changed code.",
                WhyItMatters: "This attribute bypasses the CLR security walk for P/Invoke calls, removing the runtime check that prevents untrusted callers from executing native code.",
                SuggestedAction: "Remove [SuppressUnmanagedCodeSecurity] unless the P/Invoke surface is tightly controlled and the performance gain has been explicitly justified and reviewed.",
                Kind: DeterministicSignalKind.AddedLineRegex,
                Pattern: @"\[SuppressUnmanagedCodeSecurity\b",
                PatternOptions: RegexOptions.Compiled | RegexOptions.IgnoreCase,
                ExcludedFilePathPatterns: commonPathExclusions,
                IncludedFilePathPatterns: [],
                ExcludedSnippetPatterns: []),
            new DeterministicSignalSpec(
                SignalId: "DET-SEC-011",
                RuleId: "GCI012",
                RuleName: "Security Risk",
                Severity: "high",
                Confidence: "High",
                FindingText: "An [AllowAnonymous] attribute was introduced in changed code.",
                WhyItMatters: "AllowAnonymous overrides any controller-level or global [Authorize] policy, exposing that endpoint to unauthenticated callers.",
                SuggestedAction: "Confirm that unauthenticated access is intentional and that the endpoint exposes no sensitive data or side effects.",
                Kind: DeterministicSignalKind.AddedLineRegex,
                Pattern: @"\[AllowAnonymous\b",
                PatternOptions: RegexOptions.Compiled | RegexOptions.IgnoreCase,
                ExcludedFilePathPatterns: commonPathExclusions,
                IncludedFilePathPatterns: [],
                ExcludedSnippetPatterns: []),
            new DeterministicSignalSpec(
                SignalId: "DET-SEC-012",
                RuleId: "GCI012",
                RuleName: "Security Risk",
                Severity: "medium",
                Confidence: "Medium",
                FindingText: "A non-localhost http:// URL literal was introduced in changed code.",
                WhyItMatters: "Plain HTTP transmits data without encryption; an accidental http:// in a configuration or request can silently downgrade a previously TLS-protected call.",
                SuggestedAction: "Replace http:// with https:// or move the URL to configuration and enforce TLS at the transport layer.",
                Kind: DeterministicSignalKind.AddedLineRegex,
                Pattern: @"[""']http://(?!localhost(?![\w.])|127\.0\.0\.1\b)[^""'\s]{3,}[""']",
                PatternOptions: RegexOptions.Compiled | RegexOptions.IgnoreCase,
                ExcludedFilePathPatterns: commonPathExclusions.Concat(testAndBenchmarkPathExclusions).ToArray(),
                IncludedFilePathPatterns: [],
                ExcludedSnippetPatterns: []),
            new DeterministicSignalSpec(
                SignalId: "DET-SEC-013",
                RuleId: "GCI012",
                RuleName: "Security Risk",
                Severity: "medium",
                Confidence: "Medium",
                FindingText: "Process.Start was introduced in changed code.",
                WhyItMatters: "Launching a child process with data derived from user input or external sources can be a command injection vector.",
                SuggestedAction: "Ensure Process.Start arguments are constructed from trusted, validated values only; never interpolate user-controlled input directly into process arguments.",
                Kind: DeterministicSignalKind.AddedLineRegex,
                Pattern: @"\bProcess\.Start\s*\(",
                PatternOptions: RegexOptions.Compiled | RegexOptions.IgnoreCase,
                ExcludedFilePathPatterns: commonPathExclusions.Concat(testAndBenchmarkPathExclusions).ToArray(),
                IncludedFilePathPatterns: [],
                ExcludedSnippetPatterns: []),

            // --- Code Quality (new) ---
            new DeterministicSignalSpec(
                SignalId: "DET-QUAL-001",
                RuleId: "GCI013",
                RuleName: "Observability and Debuggability",
                Severity: "low",
                Confidence: "Low",
                FindingText: "A [SuppressMessage] attribute was introduced in changed code.",
                WhyItMatters: "Suppressing analyser warnings hides the signal that the warning exists; the underlying issue is not fixed and may be silently re-introduced.",
                SuggestedAction: "Fix the underlying issue the analyser is flagging rather than suppressing the diagnostic.",
                Kind: DeterministicSignalKind.AddedLineRegex,
                Pattern: @"\[SuppressMessage\s*\(",
                PatternOptions: RegexOptions.Compiled | RegexOptions.IgnoreCase,
                ExcludedFilePathPatterns: commonPathExclusions,
                IncludedFilePathPatterns: [],
                ExcludedSnippetPatterns: []),
            new DeterministicSignalSpec(
                SignalId: "DET-QUAL-002",
                RuleId: "GCI013",
                RuleName: "Observability and Debuggability",
                Severity: "low",
                Confidence: "Low",
                FindingText: "A #pragma warning disable directive was introduced in changed code.",
                WhyItMatters: "Blanket pragma disables suppress warnings for entire scopes and can mask future regressions introduced in the same area.",
                SuggestedAction: "Fix the underlying warning, or at minimum add a #pragma warning restore immediately after the affected line to limit the suppression scope.",
                Kind: DeterministicSignalKind.AddedLineRegex,
                Pattern: @"#pragma\s+warning\s+disable",
                PatternOptions: RegexOptions.Compiled | RegexOptions.IgnoreCase,
                ExcludedFilePathPatterns: commonPathExclusions,
                IncludedFilePathPatterns: [],
                ExcludedSnippetPatterns: []),
            new DeterministicSignalSpec(
                SignalId: "DET-QUAL-004",
                RuleId: "GCI008",
                RuleName: "Complexity Control",
                Severity: "medium",
                Confidence: "Medium",
                FindingText: "The dynamic keyword was introduced in changed code.",
                WhyItMatters: "dynamic defers type resolution to runtime, bypasses compile-time safety, and makes refactoring and static analysis unreliable.",
                SuggestedAction: "Use a concrete type, an interface, or generics instead of dynamic.",
                Kind: DeterministicSignalKind.AddedLineRegex,
                Pattern: @"\bdynamic\s+[A-Za-z_]",
                PatternOptions: RegexOptions.Compiled,
                ExcludedFilePathPatterns: commonPathExclusions.Concat(testAndBenchmarkPathExclusions).ToArray(),
                IncludedFilePathPatterns: [],
                ExcludedSnippetPatterns: []),
            new DeterministicSignalSpec(
                SignalId: "DET-QUAL-005",
                RuleId: "GCI008",
                RuleName: "Complexity Control",
                Severity: "medium",
                Confidence: "High",
                FindingText: "A goto statement was introduced in changed code.",
                WhyItMatters: "goto creates non-local control flow that is difficult to reason about, increases cyclomatic complexity, and can introduce subtle ordering bugs.",
                SuggestedAction: "Replace goto with structured control flow (break, continue, early return, or an extraction to a helper method).",
                Kind: DeterministicSignalKind.AddedLineRegex,
                Pattern: @"\bgoto\s+[A-Za-z_]",
                PatternOptions: RegexOptions.Compiled | RegexOptions.IgnoreCase,
                ExcludedFilePathPatterns: commonPathExclusions,
                IncludedFilePathPatterns: [],
                ExcludedSnippetPatterns: []),
            new DeterministicSignalSpec(
                SignalId: "DET-QUAL-006",
                RuleId: "GCI004",
                RuleName: "Breaking Change Risk",
                Severity: "low",
                Confidence: "Low",
                FindingText: "[CLSCompliant(false)] was introduced in changed code.",
                WhyItMatters: "Marking a public member as not CLS-compliant prevents it from being used by any CLS-compliant language (e.g. Visual Basic .NET) and can silently break cross-language consumers.",
                SuggestedAction: "Design public APIs to be CLS-compliant; use CLS-compliant alternatives (e.g. uint → long) or restrict the member to internal visibility.",
                Kind: DeterministicSignalKind.AddedLineRegex,
                Pattern: @"\[CLSCompliant\s*\(\s*false\s*\)",
                PatternOptions: RegexOptions.Compiled | RegexOptions.IgnoreCase,
                ExcludedFilePathPatterns: commonPathExclusions,
                IncludedFilePathPatterns: [],
                ExcludedSnippetPatterns: []),

            // --- Infrastructure (new) ---
            new DeterministicSignalSpec(
                SignalId: "DET-INFRA-002",
                RuleId: "GCI016",
                RuleName: "Concurrency and State Risk",
                Severity: "medium",
                Confidence: "High",
                FindingText: "Timeout.Infinite was introduced in changed code.",
                WhyItMatters: "An infinite timeout means a hung downstream call will block the thread or task permanently, preventing graceful shutdown and draining thread-pool resources.",
                SuggestedAction: "Replace Timeout.Infinite with an explicit, configurable timeout value appropriate for the operation's SLA.",
                Kind: DeterministicSignalKind.AddedLineRegex,
                Pattern: @"\bTimeout\.Infinite\b",
                PatternOptions: RegexOptions.Compiled | RegexOptions.IgnoreCase,
                ExcludedFilePathPatterns: commonPathExclusions.Concat(testAndBenchmarkPathExclusions).ToArray(),
                IncludedFilePathPatterns: [],
                ExcludedSnippetPatterns: []),
            new DeterministicSignalSpec(
                SignalId: "DET-INFRA-004",
                RuleId: "GCI003",
                RuleName: "Behavioral Change Detection",
                Severity: "high",
                Confidence: "High",
                FindingText: "Environment.Exit or Environment.FailFast was introduced in changed code.",
                WhyItMatters: "Both methods terminate the process immediately, bypassing finally blocks, IDisposable cleanup, and graceful-shutdown hooks; this can corrupt in-flight state and prevent clean restart.",
                SuggestedAction: "Propagate failures through exceptions or return values and allow the host (e.g. ASP.NET Core, Worker Service) to handle shutdown.",
                Kind: DeterministicSignalKind.AddedLineRegex,
                Pattern: @"\bEnvironment\.(Exit|FailFast)\s*\(",
                PatternOptions: RegexOptions.Compiled | RegexOptions.IgnoreCase,
                ExcludedFilePathPatterns: commonPathExclusions,
                IncludedFilePathPatterns: [],
                ExcludedSnippetPatterns: [])
        ];
    }

    private static IReadOnlyList<DeterministicSignalSpec> CreateRemovedLineSignalSpecs()
    {
        IReadOnlyList<string> commonPathExclusions =
        [
            @"(^|/)(obj|bin|dist|coverage|node_modules|vendor|\.git)/",
            @"(^|/)migrations?/.*snapshot"
        ];
        IReadOnlyList<string> testAndBenchmarkPathExclusions =
        [
            @"(^|/)(test|tests|benchmark|benchmarks?)/"
        ];
        IReadOnlyList<string> testFileInclusionPatterns =
        [
            @"(^|/)(test|tests)/",
            @"[Tt]ests?\.cs$",
            @"(^|/)[^/]*[Ss]pecs?\.cs$"
        ];

        return
        [
            // --- Concurrency ---
            new DeterministicSignalSpec(
                SignalId: "DET-CONC-004",
                RuleId: "GCI016",
                RuleName: "Concurrency and State Risk",
                Severity: "high",
                Confidence: "High",
                FindingText: "A lock statement was removed from changed code.",
                WhyItMatters: "Removing a lock without a replacement synchronisation mechanism can introduce race conditions on shared state.",
                SuggestedAction: "Verify that the removed lock's invariant is upheld by another mechanism, or restore it.",
                Kind: DeterministicSignalKind.RemovedLineRegex,
                Pattern: @"\block\s*\(",
                PatternOptions: RegexOptions.Compiled | RegexOptions.IgnoreCase,
                ExcludedFilePathPatterns: commonPathExclusions.Concat(testAndBenchmarkPathExclusions).ToArray(),
                IncludedFilePathPatterns: [],
                ExcludedSnippetPatterns: []),
            new DeterministicSignalSpec(
                SignalId: "DET-CONC-005",
                RuleId: "GCI016",
                RuleName: "Concurrency and State Risk",
                Severity: "medium",
                Confidence: "Medium",
                FindingText: "A Parallel.ForEach call was removed from changed code.",
                WhyItMatters: "Replacing parallel iteration with sequential processing reduces throughput and may indicate an unintentional regression in performance-sensitive code.",
                SuggestedAction: "Confirm the removal is deliberate; if parallelism was removed for correctness reasons, document the shared-state hazard that required it.",
                Kind: DeterministicSignalKind.RemovedLineRegex,
                Pattern: @"\bParallel\.ForEach\b",
                PatternOptions: RegexOptions.Compiled | RegexOptions.IgnoreCase,
                ExcludedFilePathPatterns: commonPathExclusions.Concat(testAndBenchmarkPathExclusions).ToArray(),
                IncludedFilePathPatterns: [],
                ExcludedSnippetPatterns: []),
            new DeterministicSignalSpec(
                SignalId: "DET-CONC-006",
                RuleId: "GCI016",
                RuleName: "Concurrency and State Risk",
                Severity: "high",
                Confidence: "High",
                FindingText: "A thread-safe collection type (ConcurrentDictionary/Bag/Queue/Stack) was removed from changed code.",
                WhyItMatters: "Replacing a thread-safe collection with a non-thread-safe equivalent without external synchronisation is a direct race condition.",
                SuggestedAction: "Restore the concurrent collection or add explicit locking around all access sites.",
                Kind: DeterministicSignalKind.RemovedLineRegex,
                Pattern: @"\bConcurrent(Dictionary|Bag|Queue|Stack)\s*<",
                PatternOptions: RegexOptions.Compiled | RegexOptions.IgnoreCase,
                ExcludedFilePathPatterns: commonPathExclusions.Concat(testAndBenchmarkPathExclusions).ToArray(),
                IncludedFilePathPatterns: [],
                ExcludedSnippetPatterns: []),
            new DeterministicSignalSpec(
                SignalId: "DET-CONC-007",
                RuleId: "GCI016",
                RuleName: "Concurrency and State Risk",
                Severity: "medium",
                Confidence: "Medium",
                FindingText: "An ImmutableArray/ImmutableList/ImmutableDictionary type was removed from changed code.",
                WhyItMatters: "Replacing an immutable collection with a mutable one can introduce shared-state mutation bugs when the instance crosses thread boundaries.",
                SuggestedAction: "Restore the immutable collection or add synchronisation around all mutation and read sites.",
                Kind: DeterministicSignalKind.RemovedLineRegex,
                Pattern: @"\bImmutable(Array|List|Dictionary|HashSet|Queue|Stack|SortedDictionary|SortedSet)\s*[<.]",
                PatternOptions: RegexOptions.Compiled | RegexOptions.IgnoreCase,
                ExcludedFilePathPatterns: commonPathExclusions.Concat(testAndBenchmarkPathExclusions).ToArray(),
                IncludedFilePathPatterns: [],
                ExcludedSnippetPatterns: []),
            new DeterministicSignalSpec(
                SignalId: "DET-CONC-008",
                RuleId: "GCI016",
                RuleName: "Concurrency and State Risk",
                Severity: "medium",
                Confidence: "Medium",
                FindingText: "The volatile keyword was removed from a field in changed code.",
                WhyItMatters: "volatile guarantees visibility of writes across CPU cores; removing it can cause threads to read stale cached values from processor registers.",
                SuggestedAction: "Restore volatile or replace it with an equivalent synchronisation primitive (e.g. Interlocked, a lock, or a memory barrier).",
                Kind: DeterministicSignalKind.RemovedLineRegex,
                Pattern: @"\bvolatile\b",
                PatternOptions: RegexOptions.Compiled,
                ExcludedFilePathPatterns: commonPathExclusions.Concat(testAndBenchmarkPathExclusions).ToArray(),
                IncludedFilePathPatterns: [],
                ExcludedSnippetPatterns: []),
            new DeterministicSignalSpec(
                SignalId: "DET-INFRA-003",
                RuleId: "GCI016",
                RuleName: "Concurrency and State Risk",
                Severity: "medium",
                Confidence: "Medium",
                FindingText: "A CancellationToken parameter or usage was removed from changed code.",
                WhyItMatters: "Removing CancellationToken support means the operation can no longer be cancelled; long-running calls will block shutdown and consume resources past their deadline.",
                SuggestedAction: "Restore CancellationToken propagation through the call chain.",
                Kind: DeterministicSignalKind.RemovedLineRegex,
                Pattern: @"\bCancellationToken\b",
                PatternOptions: RegexOptions.Compiled,
                ExcludedFilePathPatterns: commonPathExclusions.Concat(testAndBenchmarkPathExclusions).ToArray(),
                IncludedFilePathPatterns: [],
                ExcludedSnippetPatterns: []),

            // --- State Mutation ---
            new DeterministicSignalSpec(
                SignalId: "DET-MUTAT-001",
                RuleId: "GCI016",
                RuleName: "Concurrency and State Risk",
                Severity: "medium",
                Confidence: "Medium",
                FindingText: "The readonly keyword was removed from a field declaration in changed code.",
                WhyItMatters: "readonly enforces assignment-once semantics; removing it allows the field to be mutated after construction, breaking immutability invariants and potentially introducing race conditions.",
                SuggestedAction: "Restore readonly, or document why mutation after construction is now required.",
                Kind: DeterministicSignalKind.RemovedLineRegex,
                Pattern: @"\breadonly\b",
                PatternOptions: RegexOptions.Compiled,
                ExcludedFilePathPatterns: commonPathExclusions.Concat(testAndBenchmarkPathExclusions).ToArray(),
                IncludedFilePathPatterns: [],
                ExcludedSnippetPatterns: []),

            // --- Error Handling / Reliability ---
            new DeterministicSignalSpec(
                SignalId: "DET-REL-004",
                RuleId: "GCI007",
                RuleName: "Error Handling Integrity",
                Severity: "high",
                Confidence: "High",
                FindingText: "A finally block was removed from changed code.",
                WhyItMatters: "finally guarantees cleanup regardless of whether an exception is thrown; its removal risks resource leaks and inconsistent state on the exception path.",
                SuggestedAction: "Restore the finally block or convert the cleanup to a using/IDisposable pattern that provides equivalent guarantees.",
                Kind: DeterministicSignalKind.RemovedLineRegex,
                Pattern: @"\bfinally\b",
                PatternOptions: RegexOptions.Compiled,
                ExcludedFilePathPatterns: commonPathExclusions,
                IncludedFilePathPatterns: [],
                ExcludedSnippetPatterns: []),
            new DeterministicSignalSpec(
                SignalId: "DET-REL-005",
                RuleId: "GCI007",
                RuleName: "Error Handling Integrity",
                Severity: "high",
                Confidence: "High",
                FindingText: "A retry or resilience policy was removed from changed code.",
                WhyItMatters: "Removing retry logic means transient failures (network blips, throttling) that were previously recovered automatically will now surface as hard errors.",
                SuggestedAction: "Restore the retry policy or replace it with an equivalent resilience strategy appropriate for the dependency's failure characteristics.",
                Kind: DeterministicSignalKind.RemovedLineRegex,
                Pattern: @"\b(Retry|RetryPolicy|WaitAndRetry|WaitAndRetryAsync|AddResiliencePipeline|ResiliencePipeline)\b",
                PatternOptions: RegexOptions.Compiled | RegexOptions.IgnoreCase,
                ExcludedFilePathPatterns: commonPathExclusions.Concat(testAndBenchmarkPathExclusions).ToArray(),
                IncludedFilePathPatterns: [],
                ExcludedSnippetPatterns: []),
            new DeterministicSignalSpec(
                SignalId: "DET-INFRA-001",
                RuleId: "GCI007",
                RuleName: "Error Handling Integrity",
                Severity: "high",
                Confidence: "High",
                FindingText: "A circuit breaker policy was removed from changed code.",
                WhyItMatters: "Without a circuit breaker, a failing downstream dependency will continue to receive requests, amplifying load and degrading the whole system during an outage.",
                SuggestedAction: "Restore the circuit breaker or replace it with an equivalent policy that prevents cascade failure when the dependency becomes unhealthy.",
                Kind: DeterministicSignalKind.RemovedLineRegex,
                Pattern: @"\b(CircuitBreaker|CircuitBreakerPolicy|AddCircuitBreaker)\b",
                PatternOptions: RegexOptions.Compiled | RegexOptions.IgnoreCase,
                ExcludedFilePathPatterns: commonPathExclusions.Concat(testAndBenchmarkPathExclusions).ToArray(),
                IncludedFilePathPatterns: [],
                ExcludedSnippetPatterns: []),

            // --- Disposal ---
            new DeterministicSignalSpec(
                SignalId: "DET-DISP-001",
                RuleId: "GCI007",
                RuleName: "Error Handling Integrity",
                Severity: "high",
                Confidence: "High",
                FindingText: "A using statement or using-var declaration was removed from changed code.",
                WhyItMatters: "Removing using without an explicit Dispose call leaks the resource; this manifests as file-handle exhaustion, unreleased connections, or memory pressure under load.",
                SuggestedAction: "Restore the using block/declaration, or add a try/finally with an explicit Dispose call in the cleanup branch.",
                Kind: DeterministicSignalKind.RemovedLineRegex,
                Pattern: @"\busing\s*\(|\busing\s+var\b",
                PatternOptions: RegexOptions.Compiled | RegexOptions.IgnoreCase,
                ExcludedFilePathPatterns: commonPathExclusions,
                IncludedFilePathPatterns: [],
                ExcludedSnippetPatterns: []),

            // --- Performance ---
            new DeterministicSignalSpec(
                SignalId: "DET-PERF-003",
                RuleId: "GCI011",
                RuleName: "Performance Risk",
                Severity: "medium",
                Confidence: "Medium",
                FindingText: "ArrayPool usage was removed from changed code.",
                WhyItMatters: "Allocating new arrays instead of renting from the pool increases GC pressure and can cause Gen2 collections in throughput-sensitive paths.",
                SuggestedAction: "Restore ArrayPool<T>.Shared.Rent/Return or use a Memory<T>/Span<T> approach to avoid heap allocations on the hot path.",
                Kind: DeterministicSignalKind.RemovedLineRegex,
                Pattern: @"\bArrayPool\s*[.<]",
                PatternOptions: RegexOptions.Compiled | RegexOptions.IgnoreCase,
                ExcludedFilePathPatterns: commonPathExclusions.Concat(testAndBenchmarkPathExclusions).ToArray(),
                IncludedFilePathPatterns: [],
                ExcludedSnippetPatterns: []),

            // --- API Contract ---
            new DeterministicSignalSpec(
                SignalId: "DET-API-002",
                RuleId: "GCI004",
                RuleName: "Breaking Change Risk",
                Severity: "medium",
                Confidence: "Low",
                FindingText: "A public member declaration was removed from changed code.",
                WhyItMatters: "Removing a public method, property, or type is a binary and source-level breaking change for any caller not in this repository.",
                SuggestedAction: "Mark the member [Obsolete] first and allow a deprecation window, or confirm no external callers exist before deleting.",
                Kind: DeterministicSignalKind.RemovedLineRegex,
                Pattern: @"\bpublic\s+",
                PatternOptions: RegexOptions.Compiled,
                ExcludedFilePathPatterns: commonPathExclusions.Concat(testAndBenchmarkPathExclusions).ToArray(),
                IncludedFilePathPatterns: [],
                ExcludedSnippetPatterns: []),
            new DeterministicSignalSpec(
                SignalId: "DET-API-003",
                RuleId: "GCI004",
                RuleName: "Breaking Change Risk",
                Severity: "low",
                Confidence: "Low",
                FindingText: "An [Obsolete] attribute was removed from changed code.",
                WhyItMatters: "Removing [Obsolete] without also removing the member re-exposes a deprecated surface as a first-class API; callers that avoided it based on the warning may be surprised by its return.",
                SuggestedAction: "Either remove the member entirely or keep the [Obsolete] attribute until callers have migrated.",
                Kind: DeterministicSignalKind.RemovedLineRegex,
                Pattern: @"\[Obsolete\b",
                PatternOptions: RegexOptions.Compiled | RegexOptions.IgnoreCase,
                ExcludedFilePathPatterns: commonPathExclusions,
                IncludedFilePathPatterns: [],
                ExcludedSnippetPatterns: []),

            // --- Testing ---
            new DeterministicSignalSpec(
                SignalId: "DET-TEST-001",
                RuleId: "GCI005",
                RuleName: "Test Coverage Relevance",
                Severity: "medium",
                Confidence: "Medium",
                FindingText: "A test method attribute ([Fact]/[Test]/[TestMethod]) was removed from a test file.",
                WhyItMatters: "Deleting a test removes a standing coverage guarantee; if the corresponding production code still exists, the behaviour it verified is now unverified.",
                SuggestedAction: "Confirm the removed test is either superseded by a stronger replacement or that the production behaviour it verified has also been removed.",
                Kind: DeterministicSignalKind.RemovedLineRegex,
                Pattern: @"\[(Fact|Test|TestMethod)\b",
                PatternOptions: RegexOptions.Compiled | RegexOptions.IgnoreCase,
                ExcludedFilePathPatterns: commonPathExclusions,
                IncludedFilePathPatterns: testFileInclusionPatterns,
                ExcludedSnippetPatterns: []),

            // --- Security ---
            new DeterministicSignalSpec(
                SignalId: "DET-SEC-008",
                RuleId: "GCI012",
                RuleName: "Security Risk",
                Severity: "high",
                Confidence: "High",
                FindingText: "An [Authorize] attribute was removed from changed code.",
                WhyItMatters: "Removing [Authorize] from a controller or action drops the authentication/authorisation gate, potentially exposing protected operations to anonymous callers.",
                SuggestedAction: "Confirm the endpoint is intentionally public, or restore the [Authorize] attribute with an appropriate policy.",
                Kind: DeterministicSignalKind.RemovedLineRegex,
                Pattern: @"\[Authorize\b",
                PatternOptions: RegexOptions.Compiled | RegexOptions.IgnoreCase,
                ExcludedFilePathPatterns: commonPathExclusions,
                IncludedFilePathPatterns: [],
                ExcludedSnippetPatterns: []),
            new DeterministicSignalSpec(
                SignalId: "DET-SEC-010",
                RuleId: "GCI012",
                RuleName: "Security Risk",
                Severity: "medium",
                Confidence: "Medium",
                FindingText: "A [SecurityCritical] attribute was removed from changed code.",
                WhyItMatters: "Removing [SecurityCritical] demotes the method from the security-critical trust zone; transparent callers can now invoke it, undermining the security boundary.",
                SuggestedAction: "Restore [SecurityCritical] or provide a documented rationale for why the security boundary is no longer needed.",
                Kind: DeterministicSignalKind.RemovedLineRegex,
                Pattern: @"\[SecurityCritical\b",
                PatternOptions: RegexOptions.Compiled | RegexOptions.IgnoreCase,
                ExcludedFilePathPatterns: commonPathExclusions,
                IncludedFilePathPatterns: [],
                ExcludedSnippetPatterns: []),

            // --- Code Quality ---
            new DeterministicSignalSpec(
                SignalId: "DET-QUAL-003",
                RuleId: "GCI006",
                RuleName: "Edge Case Handling",
                Severity: "medium",
                Confidence: "Medium",
                FindingText: "A #nullable enable directive was removed from changed code.",
                WhyItMatters: "Disabling nullable context removes compile-time null-safety guarantees for the affected file; NullReferenceException risks that the compiler previously caught become runtime failures.",
                SuggestedAction: "Restore #nullable enable and resolve any resulting warnings rather than disabling the context.",
                Kind: DeterministicSignalKind.RemovedLineRegex,
                Pattern: @"#nullable\s+enable",
                PatternOptions: RegexOptions.Compiled | RegexOptions.IgnoreCase,
                ExcludedFilePathPatterns: commonPathExclusions,
                IncludedFilePathPatterns: [],
                ExcludedSnippetPatterns: [])
        ];
    }

    private static IReadOnlyList<LineMatchSignal> CreateSignalSpecsForKind(
        IReadOnlyList<DeterministicSignalSpec> specs,
        DeterministicSignalKind kind)
    {
        return specs
            .Where(spec => spec.Enabled && spec.Kind == kind)
            .Select(CreateLineMatchSignal)
            .ToArray();
    }

    private static LineMatchSignal CreateLineMatchSignal(DeterministicSignalSpec spec)
    {
        if (string.IsNullOrWhiteSpace(spec.Pattern))
        {
            throw new InvalidOperationException($"Signal {spec.SignalId} is missing a regex pattern.");
        }

        return new LineMatchSignal(
            Spec: spec,
            Pattern: new Regex(spec.Pattern, spec.PatternOptions),
            ExcludedFilePathPatterns: spec.ExcludedFilePathPatterns
                .Select(p => new Regex(p, RegexOptions.Compiled | RegexOptions.IgnoreCase))
                .ToArray(),
            IncludedFilePathPatterns: (spec.IncludedFilePathPatterns ?? [])
                .Select(p => new Regex(p, RegexOptions.Compiled | RegexOptions.IgnoreCase))
                .ToArray(),
            ExcludedSnippetPatterns: spec.ExcludedSnippetPatterns
                .Select(p => new Regex(p, RegexOptions.Compiled | RegexOptions.IgnoreCase))
                .ToArray());
    }

    private sealed record LineMatch(string FilePath, int LineNumber, string Snippet);

    private sealed record LineMatchSignal(
        DeterministicSignalSpec Spec,
        Regex Pattern,
        IReadOnlyList<Regex> ExcludedFilePathPatterns,
        IReadOnlyList<Regex> IncludedFilePathPatterns,
        IReadOnlyList<Regex> ExcludedSnippetPatterns);

    private sealed record DeterministicSignalSpec(
        string SignalId,
        string RuleId,
        string RuleName,
        string Severity,
        string Confidence,
        string FindingText,
        string WhyItMatters,
        string SuggestedAction,
        DeterministicSignalKind Kind,
        string? Pattern,
        RegexOptions PatternOptions,
        IReadOnlyList<string> ExcludedFilePathPatterns,
        IReadOnlyList<string> ExcludedSnippetPatterns,
        IReadOnlyList<string>? IncludedFilePathPatterns = null,  // null or empty = match all files
        bool Enabled = true);

    private enum DeterministicSignalKind
    {
        Metadata,
        AddedLineRegex,
        RemovedLineRegex
    }
}

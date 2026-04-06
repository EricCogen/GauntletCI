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

    private static readonly Regex HunkHeaderPattern = new(
        @"@@ -\d+(?:,\d+)? \+(?<line>\d+)(?:,\d+)? @@",
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

    private static readonly IReadOnlyList<AddedLineRegexSignal> AddedLineSignals = CreateAddedLineSignalSpecs()
        .Where(static spec => spec.Enabled && spec.Kind == DeterministicSignalKind.AddedLineRegex)
        .Select(CreateAddedLineRegexSignal)
        .ToArray();

    public IReadOnlyList<Finding> Analyze(string diff, DiffMetadata metadata)
    {
        List<Finding> findings = [];

        if (ShouldFlagWeakCoverage(metadata))
        {
            findings.Add(CreateFinding(
                WeakCoverageSignal,
                $"diff metadata: files_changed={metadata.FilesChanged}, test_files_changed={metadata.TestFilesChanged}, assertion_like_test_lines_added={metadata.TestAssertionLinesAdded}"));
        }

        foreach (AddedLineRegexSignal signal in AddedLineSignals)
        {
            AddedLineMatch? match = FirstAddedLineMatch(diff, signal);
            if (match is not null)
            {
                findings.Add(CreateFinding(
                    signal.Spec,
                    $"{match.FilePath}:{match.LineNumber} {match.Snippet}"));
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

    private static AddedLineMatch? FirstAddedLineMatch(string diff, AddedLineRegexSignal signal)
    {
        string currentFile = "unknown";
        int currentNewLine = 0;
        bool insideHunk = false;
        bool currentFileExcluded = false;

        foreach (string raw in diff.Split('\n'))
        {
            string line = raw.TrimEnd('\r');

            if (line.StartsWith("diff --git ", StringComparison.Ordinal))
            {
                currentFile = ParseFilePath(line);
                insideHunk = false;
                currentNewLine = 0;
                currentFileExcluded = signal.ExcludedFilePathPatterns.Any(exclusion => exclusion.IsMatch(currentFile));
                continue;
            }

            Match hunkHeader = HunkHeaderPattern.Match(line);
            if (hunkHeader.Success)
            {
                insideHunk = true;
                currentNewLine = int.Parse(hunkHeader.Groups["line"].Value);
                continue;
            }

            if (!insideHunk)
            {
                continue;
            }

            if (line.StartsWith('+') && !line.StartsWith("+++", StringComparison.Ordinal))
            {
                string snippet = line[1..].Trim();
                if (!currentFileExcluded &&
                    !IsCommentLike(snippet) &&
                    !signal.ExcludedSnippetPatterns.Any(exclusion => exclusion.IsMatch(snippet)) &&
                    signal.Pattern.IsMatch(snippet))
                {
                    return new AddedLineMatch(currentFile, currentNewLine, snippet.Length > 160 ? snippet[..160] : snippet);
                }

                currentNewLine++;
                continue;
            }

            if (line.StartsWith('-') && !line.StartsWith("---", StringComparison.Ordinal))
            {
                continue;
            }

            currentNewLine++;
        }

        return null;
    }

    private static string ParseFilePath(string diffHeader)
    {
        string[] parts = diffHeader.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4)
        {
            return "unknown";
        }

        string candidate = parts[3];
        if (candidate.StartsWith("b/", StringComparison.Ordinal))
        {
            return candidate[2..];
        }

        return candidate.TrimStart('/');
    }

    private static bool IsCommentLike(string snippet)
    {
        return snippet.StartsWith("//", StringComparison.Ordinal) ||
               snippet.StartsWith('#') ||
               snippet.StartsWith("/*", StringComparison.Ordinal) ||
               snippet.StartsWith('*');
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
                ExcludedSnippetPatterns: [])
        ];
    }

    private static AddedLineRegexSignal CreateAddedLineRegexSignal(DeterministicSignalSpec spec)
    {
        if (string.IsNullOrWhiteSpace(spec.Pattern))
        {
            throw new InvalidOperationException($"Signal {spec.SignalId} is missing a regex pattern.");
        }

        return new AddedLineRegexSignal(
            Spec: spec,
            Pattern: new Regex(spec.Pattern, spec.PatternOptions),
            ExcludedFilePathPatterns: spec.ExcludedFilePathPatterns
                .Select(pattern => new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase))
                .ToArray(),
            ExcludedSnippetPatterns: spec.ExcludedSnippetPatterns
                .Select(pattern => new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase))
                .ToArray());
    }

    private sealed record AddedLineMatch(string FilePath, int LineNumber, string Snippet);

    private sealed record AddedLineRegexSignal(
        DeterministicSignalSpec Spec,
        Regex Pattern,
        IReadOnlyList<Regex> ExcludedFilePathPatterns,
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
        bool Enabled = true);

    private enum DeterministicSignalKind
    {
        Metadata,
        AddedLineRegex
    }
}

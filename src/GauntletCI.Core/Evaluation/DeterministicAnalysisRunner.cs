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

    private static readonly Regex AddedLineAsyncBlockingPattern = new(
        @"(\.Result\b|GetAwaiter\(\)\.GetResult\(|\.Wait\(\)|Thread\.Sleep\(|time\.sleep\()",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex AddedLineDestructiveSchemaPattern = new(
        @"\b(DROP\s+COLUMN|DROP\s+TABLE|remove_column\b|drop_table\b)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex AddedLineSecretLiteralPattern = new(
        @"((api[_-]?key|secret|token|password|passwd)\b.{0,20}[:=]\s*[""'][^""']+[""']|Authorization\s*[:=]\s*[""']?Bearer\s+[A-Za-z0-9_\-\.]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex HunkHeaderPattern = new(
        @"@@ -\d+(?:,\d+)? \+(?<line>\d+)(?:,\d+)? @@",
        RegexOptions.Compiled);

    public IReadOnlyList<Finding> Analyze(string diff, DiffMetadata metadata)
    {
        List<Finding> findings = [];

        if (ShouldFlagWeakCoverage(metadata))
        {
            findings.Add(new Finding(
                RuleId: "GCI005",
                RuleName: "Test Coverage Relevance",
                Severity: "medium",
                FindingText: "Code behavior changed without meaningful new test assertions on the changed path.",
                Evidence: $"diff metadata: files_changed={metadata.FilesChanged}, test_files_changed={metadata.TestFilesChanged}, assertion_like_test_lines_added={metadata.TestAssertionLinesAdded}",
                WhyItMatters: "Behavior can regress while tests still pass when new execution paths are not asserted.",
                SuggestedAction: "Add assertions that exercise and verify the changed runtime path.",
                Confidence: "Medium"));
        }

        AddedLineMatch? asyncBlocking = FirstAddedLineMatch(diff, AddedLineAsyncBlockingPattern);
        if (asyncBlocking is not null)
        {
            findings.Add(new Finding(
                RuleId: "GCI016",
                RuleName: "Concurrency and State Risk",
                Severity: "high",
                FindingText: "Blocking async/synchronization pattern was introduced in changed code.",
                Evidence: $"{asyncBlocking.FilePath}:{asyncBlocking.LineNumber} {asyncBlocking.Snippet}",
                WhyItMatters: "Sync-over-async patterns can deadlock request flows or starve concurrency under load.",
                SuggestedAction: "Use async/await end-to-end and avoid blocking waits like .Result, .Wait(), and GetAwaiter().GetResult().",
                Confidence: "High"));
        }

        AddedLineMatch? destructiveSchema = FirstAddedLineMatch(diff, AddedLineDestructiveSchemaPattern);
        if (destructiveSchema is not null)
        {
            findings.Add(new Finding(
                RuleId: "GCI014",
                RuleName: "Rollback Safety",
                Severity: "high",
                FindingText: "Destructive schema change was introduced without proof of rollback-safe recovery.",
                Evidence: $"{destructiveSchema.FilePath}:{destructiveSchema.LineNumber} {destructiveSchema.Snippet}",
                WhyItMatters: "Drops can permanently remove production data and make rollback incomplete or unsafe.",
                SuggestedAction: "Use staged/backfill migration strategy and document an explicit rollback path before destructive schema removal.",
                Confidence: "High"));
        }

        AddedLineMatch? secretLiteral = FirstAddedLineMatch(diff, AddedLineSecretLiteralPattern);
        if (secretLiteral is not null)
        {
            findings.Add(new Finding(
                RuleId: "GCI012",
                RuleName: "Security Risk",
                Severity: "high",
                FindingText: "A credential-like literal was added directly in code/config changes.",
                Evidence: $"{secretLiteral.FilePath}:{secretLiteral.LineNumber} {secretLiteral.Snippet}",
                WhyItMatters: "Hardcoded credentials can leak through source control and are difficult to rotate safely.",
                SuggestedAction: "Move secrets to secure environment/config providers and inject at runtime.",
                Confidence: "High"));
        }

        return findings;
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

    private static AddedLineMatch? FirstAddedLineMatch(string diff, Regex pattern)
    {
        string currentFile = "unknown";
        int currentNewLine = 0;
        bool insideHunk = false;

        foreach (string raw in diff.Split('\n'))
        {
            string line = raw.TrimEnd('\r');

            if (line.StartsWith("diff --git ", StringComparison.Ordinal))
            {
                currentFile = ParseFilePath(line);
                insideHunk = false;
                currentNewLine = 0;
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
                if (!IsCommentLike(snippet) && pattern.IsMatch(snippet))
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

        return parts[3].TrimStart('b', '/');
    }

    private static bool IsCommentLike(string snippet)
    {
        return snippet.StartsWith("//", StringComparison.Ordinal) ||
               snippet.StartsWith('#') ||
               snippet.StartsWith("/*", StringComparison.Ordinal) ||
               snippet.StartsWith('*');
    }

    private sealed record AddedLineMatch(string FilePath, int LineNumber, string Snippet);
}

// SPDX-License-Identifier: Elastic-2.0
// Copyright (c) Eric Cogen. All rights reserved.

using System.Text;
using GauntletCI.Core.Models;

namespace GauntletCI.Core.Evaluation;

public sealed class ContextAssembler
{
    private readonly DiffContextTrimmer _diffTrimmer = new();

    public AssembledContext Assemble(
        GateResult branchGate,
        GateResult testGate,
        string diff,
        GauntletConfig config,
        IReadOnlyList<string> recentCommits,
        int maxDiffTokens = 8000)
    {
        TrimResult trimResult = _diffTrimmer.Trim(diff, maxDiffTokens);

        List<string> lines =
        [
            "Pre-flight gate results:",
            $"- Branch currency: {(branchGate.Passed ? "pass" : "fail")} ({branchGate.Summary})",
            $"- Test passage: {(testGate.Passed ? "pass" : "fail")} ({testGate.Summary})",
            string.Empty,
            "Test run summary:",
            $"- {ExtractTestSummary(testGate)}",
            string.Empty,
            "Repo config:",
            $"- model: {config.Model}",
            $"- telemetry: {config.Telemetry}",
            $"- test_command: {config.TestCommand}",
            $"- blocking_rules: {(config.BlockingRules.Count == 0 ? "[]" : string.Join(',', config.BlockingRules))}",
            $"- policy_refs: {(config.PolicyReferences.Count == 0 ? "[]" : string.Join(',', config.PolicyReferences))}",
            string.Empty,
            "Recent commits:",
        ];

        if (recentCommits.Count == 0)
        {
            lines.Add("- none");
        }
        else
        {
            foreach (string commit in recentCommits)
            {
                lines.Add($"- {commit}");
            }
        }

        lines.Add(string.Empty);
        lines.Add("Test change signals:");
        lines.Add($"- test_files_changed: {trimResult.Metadata.TestFilesChanged} (content_changes={trimResult.Metadata.TestFilesWithContentChanges}, rename_only={trimResult.Metadata.TestFilesRenameOnly})");
        lines.Add($"- test_lines_added: {trimResult.Metadata.TestLinesAdded}, test_lines_removed: {trimResult.Metadata.TestLinesRemoved}");
        lines.Add($"- assertion_like_test_lines_added: {trimResult.Metadata.TestAssertionLinesAdded}");
        lines.Add($"- setup_like_test_lines_added: {trimResult.Metadata.TestSetupLinesAdded}");
        lines.Add($"- tests_changed_without_new_assertions: {trimResult.Metadata.TestsChangedWithoutAssertions}");
        lines.Add($"- tests_look_like_rename_or_setup_churn: {trimResult.Metadata.TestChangesAreRenameOrSetupChurn}");
        lines.Add(string.Empty);
        if (trimResult.Trimmed)
        {
            lines.Add($"[Note: diff trimmed from {trimResult.OriginalTokens} to {trimResult.TrimmedTokens} tokens. Some context omitted. State uncertainty where relevant.]");
        }

        lines.Add("Diff:");
        lines.Add(trimResult.Diff);

        return new AssembledContext(string.Join(Environment.NewLine, lines), trimResult.Trimmed, trimResult.Metadata, trimResult.OriginalTokens, trimResult.TrimmedTokens);
    }

    private static string ExtractTestSummary(GateResult gate)
    {
        if (!gate.Passed)
        {
            return "failed";
        }

        if (string.IsNullOrWhiteSpace(gate.Output))
        {
            return "pass";
        }

        string output = gate.Output;
        string summaryLine = output.Split('\n').FirstOrDefault(static line => line.Contains("Test summary:", StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
        return string.IsNullOrWhiteSpace(summaryLine) ? "pass" : summaryLine.Trim();
    }
}

public sealed record AssembledContext(string Context, bool DiffTrimmed, DiffMetadata Metadata, int OriginalDiffTokens, int TrimmedDiffTokens);

public sealed class DiffContextTrimmer
{
    private static readonly string[] GeneratedMarkers = [".Designer.cs", ".g.cs", "Migrations/", "migrations/"];
    private static readonly string[] AssertionMarkers =
    [
        "assert",
        "expect(",
        ".should(",
        "verify(",
        "toequal(",
        "tobe(",
        "assertthat",
    ];
    private static readonly string[] SetupMarkers =
    [
        "setup",
        "fixture",
        "beforeeach",
        "aftereach",
        "arrange",
        "mock",
        "stub",
        "builder",
        "[fact]",
        "[theory]",
        "[test]",
        "describe(",
        "it(",
    ];

    public TrimResult Trim(string diff, int maxDiffTokens)
    {
        List<DiffFile> files = ParseFiles(diff);
        int originalTokens = EstimateTokens(diff);

        foreach (DiffFile file in files)
        {
            if (IsGenerated(file.FilePath) || file.IsBinary)
            {
                file.Excluded = true;
                continue;
            }

            foreach (DiffHunk hunk in file.Hunks)
            {
                hunk.Lines = TrimContextLines(hunk.Lines, contextLimit: 2);
            }
        }

        string candidate = Render(files);
        int candidateTokens = EstimateTokens(candidate);

        if (candidateTokens > maxDiffTokens)
        {
            IEnumerable<DiffHunk> hunks = files.Where(static f => !f.Excluded).SelectMany(static f => f.Hunks).OrderByDescending(static h => h.Lines.Count);
            foreach (DiffHunk hunk in hunks)
            {
                hunk.Excluded = true;
                candidate = Render(files);
                candidateTokens = EstimateTokens(candidate);
                if (candidateTokens <= maxDiffTokens)
                {
                    break;
                }
            }
        }

        DiffMetadata metadata = BuildMetadata(files, candidate, candidateTokens < originalTokens);
        return new TrimResult(candidate, candidateTokens < originalTokens, originalTokens, candidateTokens, metadata);
    }

    private static DiffMetadata BuildMetadata(IReadOnlyList<DiffFile> files, string rendered, bool trimmed)
    {
        int linesAdded = 0;
        int linesRemoved = 0;
        HashSet<string> languages = [];
        bool testTouched = false;
        int testFilesChanged = 0;
        int testFilesWithContentChanges = 0;
        int testFilesRenameOnly = 0;
        int testLinesAdded = 0;
        int testLinesRemoved = 0;
        int testAssertionLinesAdded = 0;
        int testSetupLinesAdded = 0;
        int testNonCommentLinesAdded = 0;

        foreach (DiffFile file in files.Where(static f => !f.Excluded))
        {
            bool isTestFile = IsTestPath(file.FilePath);
            if (isTestFile)
            {
                testTouched = true;
                testFilesChanged++;
                if (file.IsRenameOnly)
                {
                    testFilesRenameOnly++;
                }
            }

            string extension = Path.GetExtension(file.FilePath).TrimStart('.').ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(extension))
            {
                languages.Add(extension switch
                {
                    "cs" => "csharp",
                    "ts" => "typescript",
                    "js" => "javascript",
                    "py" => "python",
                    _ => extension,
                });
            }

            foreach (DiffHunk hunk in file.Hunks.Where(static h => !h.Excluded))
            {
                int fileAdded = hunk.Lines.Count(static line => line.StartsWith('+') && !line.StartsWith("+++", StringComparison.Ordinal));
                int fileRemoved = hunk.Lines.Count(static line => line.StartsWith('-') && !line.StartsWith("---", StringComparison.Ordinal));

                linesAdded += fileAdded;
                linesRemoved += fileRemoved;

                if (!isTestFile)
                {
                    continue;
                }

                testLinesAdded += fileAdded;
                testLinesRemoved += fileRemoved;

                foreach (string line in hunk.Lines.Where(static line => line.StartsWith('+') && !line.StartsWith("+++", StringComparison.Ordinal)))
                {
                    if (IsAssertionLine(line))
                    {
                        testAssertionLinesAdded++;
                    }

                    if (IsSetupLine(line))
                    {
                        testSetupLinesAdded++;
                    }

                    if (!IsCommentOrWhitespace(line))
                    {
                        testNonCommentLinesAdded++;
                    }
                }
            }

            if (isTestFile)
            {
                bool hasContentChanges = file.Hunks
                    .Where(static h => !h.Excluded)
                    .SelectMany(static h => h.Lines)
                    .Any(static line =>
                        (line.StartsWith('+') && !line.StartsWith("+++", StringComparison.Ordinal)) ||
                        (line.StartsWith('-') && !line.StartsWith("---", StringComparison.Ordinal)));
                if (hasContentChanges)
                {
                    testFilesWithContentChanges++;
                }
            }
        }

        bool testsChangedWithoutAssertions = testFilesChanged > 0 && testAssertionLinesAdded == 0;
        bool testChangesAreRenameOrSetupChurn =
            testsChangedWithoutAssertions &&
            (testFilesWithContentChanges == 0 ||
             (testNonCommentLinesAdded > 0 && testNonCommentLinesAdded == testSetupLinesAdded));

        return new DiffMetadata(
            LinesAdded: linesAdded,
            LinesRemoved: linesRemoved,
            FilesChanged: files.Count(static f => !f.Excluded),
            TestFilesTouched: testTouched,
            TestFilesChanged: testFilesChanged,
            TestFilesWithContentChanges: testFilesWithContentChanges,
            TestFilesRenameOnly: testFilesRenameOnly,
            TestLinesAdded: testLinesAdded,
            TestLinesRemoved: testLinesRemoved,
            TestAssertionLinesAdded: testAssertionLinesAdded,
            TestSetupLinesAdded: testSetupLinesAdded,
            TestsChangedWithoutAssertions: testsChangedWithoutAssertions,
            TestChangesAreRenameOrSetupChurn: testChangesAreRenameOrSetupChurn,
            Languages: [.. languages.OrderBy(static l => l)],
            DiffTrimmed: trimmed,
            EstimatedTokens: EstimateTokens(rendered));
    }

    private static bool IsGenerated(string filePath)
    {
        return GeneratedMarkers.Any(marker => filePath.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsTestPath(string filePath)
    {
        return filePath.Contains("test", StringComparison.OrdinalIgnoreCase) ||
               filePath.Contains("spec", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAssertionLine(string line)
    {
        string candidate = line[1..].Trim().ToLowerInvariant();
        return AssertionMarkers.Any(marker => candidate.Contains(marker, StringComparison.Ordinal));
    }

    private static bool IsSetupLine(string line)
    {
        string candidate = line[1..].Trim().ToLowerInvariant();
        return SetupMarkers.Any(marker => candidate.Contains(marker, StringComparison.Ordinal));
    }

    private static bool IsCommentOrWhitespace(string line)
    {
        string candidate = line[1..].Trim();
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return true;
        }

        return candidate.StartsWith("//", StringComparison.Ordinal) ||
               candidate.StartsWith("#", StringComparison.Ordinal) ||
               candidate.StartsWith("/*", StringComparison.Ordinal) ||
               candidate.StartsWith("*", StringComparison.Ordinal);
    }

    private static List<string> TrimContextLines(IReadOnlyList<string> lines, int contextLimit)
    {
        List<string> result = [];
        int contextCount = 0;
        foreach (string line in lines)
        {
            if (line.StartsWith(' '))
            {
                contextCount++;
                if (contextCount > contextLimit)
                {
                    continue;
                }
            }
            else
            {
                contextCount = 0;
            }

            result.Add(line);
        }

        return result;
    }

    private static List<DiffFile> ParseFiles(string diff)
    {
        List<DiffFile> files = [];
        DiffFile? currentFile = null;
        DiffHunk? currentHunk = null;

        foreach (string rawLine in diff.Split('\n'))
        {
            string line = rawLine.TrimEnd('\r');
            if (line.StartsWith("diff --git ", StringComparison.Ordinal))
            {
                string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                string path = parts.Length >= 4 ? parts[3].TrimStart('b', '/') : "unknown";
                currentFile = new DiffFile(path);
                files.Add(currentFile);
                currentHunk = null;
                continue;
            }

            if (currentFile is null)
            {
                continue;
            }

            if (line.StartsWith("Binary files ", StringComparison.Ordinal))
            {
                currentFile.IsBinary = true;
                continue;
            }

            if (line.StartsWith("@@", StringComparison.Ordinal))
            {
                currentHunk = new DiffHunk(line);
                currentFile.Hunks.Add(currentHunk);
                continue;
            }

            if (currentHunk is null)
            {
                currentFile.PreambleLines.Add(line);
                continue;
            }

            currentHunk.Lines.Add(line);
        }

        return files;
    }

    private static string Render(IReadOnlyList<DiffFile> files)
    {
        StringBuilder sb = new();
        foreach (DiffFile file in files)
        {
            if (file.Excluded)
            {
                continue;
            }

            sb.AppendLine($"diff --git a/{file.FilePath} b/{file.FilePath}");
            foreach (string line in file.PreambleLines)
            {
                sb.AppendLine(line);
            }

            foreach (DiffHunk hunk in file.Hunks)
            {
                if (hunk.Excluded)
                {
                    continue;
                }

                sb.AppendLine(hunk.Header);
                foreach (string line in hunk.Lines)
                {
                    sb.AppendLine(line);
                }
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static int EstimateTokens(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        return Math.Max(1, text.Length / 4);
    }

    private sealed class DiffFile(string filePath)
    {
        public string FilePath { get; } = filePath;

        public List<string> PreambleLines { get; } = [];

        public List<DiffHunk> Hunks { get; } = [];

        public bool IsBinary { get; set; }

        public bool Excluded { get; set; }

        public bool IsRenameOnly =>
            Hunks.Count == 0 &&
            PreambleLines.Any(static line => line.StartsWith("rename from ", StringComparison.Ordinal)) &&
            PreambleLines.Any(static line => line.StartsWith("rename to ", StringComparison.Ordinal));
    }

    private sealed class DiffHunk(string header)
    {
        public string Header { get; } = header;

        public List<string> Lines { get; set; } = [];

        public bool Excluded { get; set; }
    }
}

public sealed record TrimResult(string Diff, bool Trimmed, int OriginalTokens, int TrimmedTokens, DiffMetadata Metadata);

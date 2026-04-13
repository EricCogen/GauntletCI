// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.FileAnalysis;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0002 – Goal Alignment
/// Detects diffs that are unrelated to the commit message or span too many unrelated areas.
/// </summary>
public class GCI0002_GoalAlignment : RuleBase
{
    public override string Id => "GCI0002";
    public override string Name => "Goal Alignment";

    private static readonly string[] FrontendExtensions = [".ts", ".tsx", ".js", ".jsx", ".vue", ".html", ".css", ".scss"];
    private static readonly string[] BackendExtensions = [".cs", ".go", ".java", ".py", ".rb", ".rs", ".cpp", ".c"];
    private static readonly string[] ConfigExtensions = [".json", ".yml", ".yaml", ".xml", ".toml", ".env", ".config"];
    private static readonly string[] TestPatterns = ["test", "spec", "tests", "specs"];

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var diff = context.Diff;
        var findings = new List<Finding>();

        CheckCommitMessageAlignment(diff, context.SkippedFiles, findings);
        CheckUnclearScope(diff, context.SkippedFiles, findings);

        return Task.FromResult(findings);
    }

    private void CheckCommitMessageAlignment(DiffContext diff, IReadOnlyList<ChangedFileAnalysisRecord> skippedFiles, List<Finding> findings)
    {
        if (string.IsNullOrWhiteSpace(diff.CommitMessage)) return;

        var allFilePaths = diff.Files.Select(f => f.NewPath)
            .Concat(skippedFiles.Select(r => r.FilePath))
            .ToList();

        if (allFilePaths.Count == 0) return;

        var messageWords = diff.CommitMessage
            .ToLowerInvariant()
            .Split([' ', '-', '_', '.', '/', ':', '(', ')'], StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 3)
            .ToHashSet();

        if (messageWords.Count == 0) return;

        var pathSegments = allFilePaths
            .SelectMany(p => p
                .ToLowerInvariant()
                .Split(['/', '\\', '.'], StringSplitOptions.RemoveEmptyEntries))
            .Where(s => s.Length >= 3)
            .ToHashSet();

        int matchCount = pathSegments.Count(segment =>
            messageWords.Any(word => segment.Contains(word) || word.Contains(segment)));

        int totalFileCount = allFilePaths.Count;
        if (totalFileCount > 3 && matchCount == 0)
        {
            findings.Add(CreateFinding(
                summary: "Changed files appear unrelated to the commit message.",
                evidence: $"Commit message: \"{diff.CommitMessage}\" — no keyword overlap with changed files: {string.Join(", ", allFilePaths)}",
                whyItMatters: "Commits that don't match their description confuse reviewers and break git blame traceability.",
                suggestedAction: "Update the commit message to describe the actual changes, or split the commit.",
                confidence: Confidence.Low));
        }
    }

    private void CheckUnclearScope(DiffContext diff, IReadOnlyList<ChangedFileAnalysisRecord> skippedFiles, List<Finding> findings)
    {
        int totalFileCount = diff.Files.Count + skippedFiles.Count;
        if (totalFileCount <= 5) return;

        bool hasFrontend = skippedFiles.Any(r => FrontendExtensions.Any(e => r.Extension.Equals(e, StringComparison.OrdinalIgnoreCase)));
        bool hasBackend  = diff.Files.Count > 0; // eligible files are all backend (.cs)
        bool hasConfig   = skippedFiles.Any(r => ConfigExtensions.Any(e => r.Extension.Equals(e, StringComparison.OrdinalIgnoreCase)));
        bool hasTests    = diff.Files.Any(f => TestPatterns.Any(p => f.NewPath.Contains(p, StringComparison.OrdinalIgnoreCase)));

        int categoryCount = (hasFrontend ? 1 : 0) + (hasBackend ? 1 : 0) + (hasConfig ? 1 : 0) + (hasTests ? 1 : 0);

        if (categoryCount >= 3)
        {
            findings.Add(CreateFinding(
                summary: $"Diff spans {totalFileCount} files across {categoryCount} distinct categories (frontend, backend, config, tests).",
                evidence: $"Frontend: {hasFrontend}, Backend: {hasBackend}, Config: {hasConfig}, Tests: {hasTests}",
                whyItMatters: "Large cross-cutting diffs are harder to review, increase merge conflict risk, and make rollbacks difficult.",
                suggestedAction: "Consider splitting this into focused commits by concern.",
                confidence: Confidence.Low));
        }
    }
}

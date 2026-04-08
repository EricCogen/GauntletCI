// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.StaticAnalysis;

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
        DiffContext diff, AnalyzerResult? staticAnalysis, CancellationToken ct = default)
    {
        var findings = new List<Finding>();

        CheckCommitMessageAlignment(diff, findings);
        CheckUnclearScope(diff, findings);

        return Task.FromResult(findings);
    }

    private void CheckCommitMessageAlignment(DiffContext diff, List<Finding> findings)
    {
        if (string.IsNullOrWhiteSpace(diff.CommitMessage) || diff.Files.Count == 0) return;

        var messageWords = diff.CommitMessage
            .ToLowerInvariant()
            .Split([' ', '-', '_', '.', '/', ':', '(', ')'], StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3)
            .ToHashSet();

        if (messageWords.Count == 0) return;

        var fileNames = diff.Files
            .Select(f => Path.GetFileNameWithoutExtension(f.NewPath).ToLowerInvariant())
            .ToList();

        int matchCount = fileNames.Count(name =>
            messageWords.Any(word => name.Contains(word) || word.Contains(name)));

        if (diff.Files.Count > 3 && matchCount == 0)
        {
            findings.Add(CreateFinding(
                summary: "Changed files appear unrelated to the commit message.",
                evidence: $"Commit message: \"{diff.CommitMessage}\" — no keyword overlap with changed files: {string.Join(", ", diff.Files.Select(f => f.NewPath))}",
                whyItMatters: "Commits that don't match their description confuse reviewers and break git blame traceability.",
                suggestedAction: "Update the commit message to describe the actual changes, or split the commit.",
                confidence: Confidence.Low));
        }
    }

    private void CheckUnclearScope(DiffContext diff, List<Finding> findings)
    {
        if (diff.Files.Count <= 5) return;

        bool hasFrontend = diff.Files.Any(f => FrontendExtensions.Any(e => f.NewPath.EndsWith(e, StringComparison.OrdinalIgnoreCase)));
        bool hasBackend = diff.Files.Any(f => BackendExtensions.Any(e => f.NewPath.EndsWith(e, StringComparison.OrdinalIgnoreCase)));
        bool hasConfig = diff.Files.Any(f => ConfigExtensions.Any(e => f.NewPath.EndsWith(e, StringComparison.OrdinalIgnoreCase)));
        bool hasTests = diff.Files.Any(f => TestPatterns.Any(p => f.NewPath.Contains(p, StringComparison.OrdinalIgnoreCase)));

        int categoryCount = (hasFrontend ? 1 : 0) + (hasBackend ? 1 : 0) + (hasConfig ? 1 : 0) + (hasTests ? 1 : 0);

        if (categoryCount >= 3)
        {
            findings.Add(CreateFinding(
                summary: $"Diff spans {diff.Files.Count} files across {categoryCount} distinct categories (frontend, backend, config, tests).",
                evidence: $"Frontend: {hasFrontend}, Backend: {hasBackend}, Config: {hasConfig}, Tests: {hasTests}",
                whyItMatters: "Large cross-cutting diffs are harder to review, increase merge conflict risk, and make rollbacks difficult.",
                suggestedAction: "Consider splitting this into focused commits by concern.",
                confidence: Confidence.Low));
        }
    }
}

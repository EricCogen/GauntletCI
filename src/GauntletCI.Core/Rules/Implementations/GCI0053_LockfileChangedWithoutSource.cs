// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.FileAnalysis;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0053, Lockfile Changed Without Source Review
/// Fires when a diff contains only lockfile changes with no accompanying source-file edits.
/// This can hide malicious dependency upgrades or unexpected breaking changes.
/// </summary>
public class GCI0053_LockfileChangedWithoutSource : RuleBase
{
    public GCI0053_LockfileChangedWithoutSource(IPatternProvider patterns) : base(patterns)
    {
    }
    public override string Id   => "GCI0053";
    public override string Name => "Lockfile Changed Without Source Review";

    private static readonly HashSet<string> LockfileNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "packages.lock.json",
            "package-lock.json",
            "yarn.lock",
            "Pipfile.lock",
            "go.sum",
            "Cargo.lock",
            "Directory.Packages.props",
            "pnpm-lock.yaml",
            "poetry.lock",
        };

    private static readonly HashSet<string> LockfileExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".lock" };

    private static readonly HashSet<string> SourceExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".cs", ".ts", ".js", ".py", ".go", ".rs" };

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var findings = new List<Finding>();

        var allRecords = context.EligibleFiles.Concat(context.SkippedFiles).ToList();

        var lockfileChanges = allRecords
            .Where(r => IsLockfile(r.FilePath))
            .ToList();

        if (lockfileChanges.Count == 0)
            return Task.FromResult(findings);

        // Look for non-lockfile source changes in CS-eligible files and skipped files
        bool hasSourceChange =
            context.Diff.Files.Any(f => SourceExtensions.Contains(Path.GetExtension(f.NewPath)))
            || allRecords.Any(r => !IsLockfile(r.FilePath) && SourceExtensions.Contains(Path.GetExtension(r.FilePath)));

        if (hasSourceChange)
            return Task.FromResult(findings);

        foreach (var lockfileRecord in lockfileChanges)
        {
            findings.Add(CreateFinding(
                summary:         "Lockfile modified without accompanying source changes: verify dependency upgrade",
                evidence:        lockfileRecord.FilePath,
                whyItMatters:    "Lockfile-only changes introduce new dependency versions without visible source context. Malicious packages or unexpected breaking changes may go unnoticed.",
                suggestedAction: "Review the lockfile diff carefully. Verify each changed package version is intentional and from a trusted source. Consider adding a comment in the PR description describing the upgrade reason.",
                confidence:      Confidence.Low));
        }

        return Task.FromResult(findings);
    }

    private static bool IsLockfile(string path)
    {
        var fileName = Path.GetFileName(path);
        if (LockfileNames.Contains(fileName)) return true;
        return LockfileExtensions.Contains(Path.GetExtension(path));
    }
}


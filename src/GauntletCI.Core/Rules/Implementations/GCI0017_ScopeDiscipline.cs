// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0017 – Scope Discipline
/// Flags diffs that touch too many distinct modules or mix production and non-production files.
/// </summary>
public class GCI0017_ScopeDiscipline : RuleBase
{
    public override string Id => "GCI0017";
    public override string Name => "Scope Discipline";

    private static readonly string[] NonProductionPatterns =
        ["Migration", "Seed", "Fixture", "seed", "fixture", "TestData", "testdata"];

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var diff = context.Diff;
        var findings = new List<Finding>();

        CheckDistinctModules(diff, findings);
        CheckMixedProductionAndNonProduction(diff, findings);

        return Task.FromResult(findings);
    }

    private void CheckDistinctModules(DiffContext diff, List<Finding> findings)
    {
        var topLevelDirs = diff.Files
            .Select(f => f.NewPath.Replace('\\', '/'))
            .Select(p => p.Contains('/') ? p.Split('/')[0] : string.Empty)
            .Where(d => !string.IsNullOrEmpty(d))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (topLevelDirs.Count >= 3)
        {
            findings.Add(CreateFinding(
                summary: $"Diff spans {topLevelDirs.Count} distinct top-level directories.",
                evidence: $"Directories: {string.Join(", ", topLevelDirs)}",
                whyItMatters: "Changes spread across many modules are hard to review, increase risk, and make rollback harder.",
                suggestedAction: "Consider splitting the change into focused commits per module.",
                confidence: Confidence.Low));
        }
    }

    private void CheckMixedProductionAndNonProduction(DiffContext diff, List<Finding> findings)
    {
        var nonProdFiles = diff.Files
            .Where(f => NonProductionPatterns.Any(p =>
                f.NewPath.Contains(p, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var prodFiles = diff.Files
            .Where(f => !NonProductionPatterns.Any(p =>
                f.NewPath.Contains(p, StringComparison.OrdinalIgnoreCase)) &&
                !f.NewPath.Contains("Test", StringComparison.OrdinalIgnoreCase) &&
                !f.NewPath.Contains("Spec", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (nonProdFiles.Count > 0 && prodFiles.Count > 0)
        {
            findings.Add(CreateFinding(
                summary: "Non-production files (migrations, seeds, fixtures) changed alongside production code.",
                evidence: $"Non-prod: {string.Join(", ", nonProdFiles.Select(f => f.NewPath))}",
                whyItMatters: "Mixing data migration/seed changes with production logic makes the diff harder to review and roll back separately.",
                suggestedAction: "Separate data migrations and seed changes into their own commits.",
                confidence: Confidence.Low));
        }
    }
}

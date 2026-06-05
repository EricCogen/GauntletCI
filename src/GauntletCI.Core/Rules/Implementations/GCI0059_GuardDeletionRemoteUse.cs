// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0059, Guard Deletion Remote Use
/// Detects removed null/validation guards where the guarded symbol is still used in the same method.
/// </summary>
public class GCI0059_GuardDeletionRemoteUse : RuleBase
{
    public GCI0059_GuardDeletionRemoteUse(IPatternProvider patterns) : base(patterns)
    {
    }

    public override string Id => "GCI0059";
    public override string Name => "Guard Deletion Remote Use";

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var findings = new List<Finding>();

        foreach (var file in context.Diff.Files)
        {
            if (WellKnownPatterns.IsTestFile(file.NewPath))
                continue;

            foreach (var (guard, use) in GuardDeletionAnalyzer.FindRemoteUsesAfterGuardDeletion(file))
            {
                findings.Add(CreateFinding(
                    file,
                    summary: $"Removed guard on '{guard.GuardedSymbol}' but symbol still used in {guard.MethodName}()",
                    evidence: $"{file.NewPath}:{guard.LineNumber} removed `{guard.GuardText}`; {use.LineNumber} still uses `{use.UseText}`.",
                    whyItMatters: "Deleting a null or validation guard while retaining downstream use usually reintroduces NullReferenceException or invalid-state paths.",
                    suggestedAction: $"Restore the guard, add a replacement check before line {use.LineNumber}, or prove {guard.GuardedSymbol} is non-null by construction.",
                    confidence: Confidence.High,
                    line: new DiffLine
                    {
                        LineNumber = use.LineNumber,
                        Content = use.UseText,
                        Kind = use.IsAddedLine ? DiffLineKind.Added : DiffLineKind.Context,
                    }));
            }
        }

        return Task.FromResult(findings);
    }
}

// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0058, Paired Implementation Consistency
/// Compares sibling class implementations for opposite boolean polarity on the same predicate.
/// Closes the sibling-implementation drift gap (PG-RELATION / RC-3).
/// </summary>
public class GCI0058_PairedImplementationConsistency : RuleBase
{
    public GCI0058_PairedImplementationConsistency(IPatternProvider patterns) : base(patterns)
    {
    }

    public override string Id => "GCI0058";
    public override string Name => "Paired Implementation Consistency";

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var findings = new List<Finding>();

        foreach (var file in context.Diff.Files)
        {
            if (WellKnownPatterns.IsTestFile(file.NewPath))
                continue;

            var observations = PairedImplementationAnalyzer.AnalyzeFile(file);
            foreach (var (suspect, reference) in PairedImplementationAnalyzer.FindPolarityMismatches(observations))
            {
                findings.Add(CreateFinding(
                    file,
                    summary: $"Sibling implementations disagree on '{suspect.Callee}' polarity in {suspect.MethodName}()",
                    evidence: $"{file.NewPath}:{suspect.LineNumber} — {suspect.ClassName}.{suspect.MethodName} uses `{suspect.Condition}` while {reference.ClassName}.{reference.MethodName} uses `{reference.Condition}`.",
                    whyItMatters: "Parallel implementations of the same behavior with inverted predicates usually indicate a copy/paste logic bug rather than an intentional difference.",
                    suggestedAction: $"Align {suspect.ClassName}.{suspect.MethodName} with {reference.ClassName} or document why the polarity must differ.",
                    confidence: Confidence.High,
                    line: new DiffLine { LineNumber = suspect.LineNumber, Content = suspect.Condition, Kind = DiffLineKind.Added }));
            }
        }

        return Task.FromResult(findings);
    }
}

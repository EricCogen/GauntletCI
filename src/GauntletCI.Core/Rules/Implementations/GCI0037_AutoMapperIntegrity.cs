// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.StaticAnalysis;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0037 – AutoMapper Integrity
/// Fires when AutoMapper mappings are added or changed without AssertConfigurationIsValid test evidence.
/// </summary>
public class GCI0037_AutoMapperIntegrity : RuleBase
{
    public override string Id => "GCI0037";
    public override string Name => "AutoMapper Integrity";

    private static readonly string[] MappingSignals =
    [
        "CreateMap<", ": Profile", ".Map<"
    ];

    public override Task<List<Finding>> EvaluateAsync(
        DiffContext diff, AnalyzerResult? staticAnalysis, CancellationToken ct = default)
    {
        var findings = new List<Finding>();

        bool hasMappingSignal = diff.Files
            .Where(f => f.NewPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .SelectMany(f => f.AddedLines)
            .Any(l => MappingSignals.Any(signal => l.Content.Contains(signal, StringComparison.Ordinal)));

        if (!hasMappingSignal) return Task.FromResult(findings);

        bool hasValidation = diff.Files
            .SelectMany(f => f.Hunks.SelectMany(h => h.Lines))
            .Any(l => l.Content.Contains("AssertConfigurationIsValid", StringComparison.Ordinal));

        if (!hasValidation)
        {
            findings.Add(CreateFinding(
                summary: "AutoMapper mapping changed without AssertConfigurationIsValid test evidence in this diff.",
                evidence: "AutoMapper mapping signal (CreateMap<, : Profile, or .Map<) found in added lines.",
                whyItMatters: "Unmapped or misconfigured AutoMapper profiles fail at runtime, not compile time. Without AssertConfigurationIsValid in tests, broken mappings ship to production.",
                suggestedAction: "Call cfg.AssertConfigurationIsValid() in a unit test after each AutoMapper profile change to catch mapping gaps at build time.",
                confidence: Confidence.Medium));
        }

        return Task.FromResult(findings);
    }
}

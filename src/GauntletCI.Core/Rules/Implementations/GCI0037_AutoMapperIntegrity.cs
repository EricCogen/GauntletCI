// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0037 – AutoMapper Integrity
/// Fires when AutoMapper mappings are added or changed without AssertConfigurationIsValid test evidence.
/// </summary>
public class GCI0037_AutoMapperIntegrity : RuleBase
{
    public override string Id => "GCI0037";
    public override string Name => "AutoMapper Integrity";

    private static readonly string[] StrongSignals = ["CreateMap<", ": Profile"];
    private const string MapSignal = ".Map<";
    private const string UsingAutoMapper = "using AutoMapper";

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var diff = context.Diff;
        var findings = new List<Finding>();

        var addedLines = diff.Files.SelectMany(f => f.AddedLines).ToList();
        var allLines = diff.Files.SelectMany(f => f.Hunks.SelectMany(h => h.Lines)).ToList();

        bool hasStrongSignal = addedLines.Any(l =>
            StrongSignals.Any(s => l.Content.Contains(s, StringComparison.Ordinal)))
            || allLines.Any(l => l.Content.Contains(UsingAutoMapper, StringComparison.Ordinal));

        if (!hasStrongSignal) return Task.FromResult(findings);

        bool hasMapCall = addedLines.Any(l => l.Content.Contains(MapSignal, StringComparison.Ordinal));

        bool hasValidation = allLines.Any(l =>
            l.Content.Contains("AssertConfigurationIsValid", StringComparison.Ordinal));

        if (!hasValidation)
        {
            var evidenceSignals = new List<string>();
            if (addedLines.Any(l => l.Content.Contains("CreateMap<", StringComparison.Ordinal)))
                evidenceSignals.Add("CreateMap<");
            if (addedLines.Any(l => l.Content.Contains(": Profile", StringComparison.Ordinal)))
                evidenceSignals.Add(": Profile");
            if (allLines.Any(l => l.Content.Contains(UsingAutoMapper, StringComparison.Ordinal)))
                evidenceSignals.Add("using AutoMapper");
            if (hasMapCall)
                evidenceSignals.Add(".Map<");

            findings.Add(CreateFinding(
                summary: "AutoMapper mapping changed without AssertConfigurationIsValid test evidence in this diff.",
                evidence: $"AutoMapper signal(s) found in added lines: {string.Join(", ", evidenceSignals)}.",
                whyItMatters: "Unmapped or misconfigured AutoMapper profiles fail at runtime, not compile time. Without AssertConfigurationIsValid in tests, broken mappings ship to production.",
                suggestedAction: "Call cfg.AssertConfigurationIsValid() in a unit test after each AutoMapper profile change to catch mapping gaps at build time.",
                confidence: Confidence.Medium));
        }

        return Task.FromResult(findings);
    }
}

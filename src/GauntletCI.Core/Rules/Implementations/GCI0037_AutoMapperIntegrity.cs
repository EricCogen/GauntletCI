// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0037 – Mapping Profile Integrity
/// Fires when AutoMapper, Mapster, AgileMapper, or TinyMapper mappings are added or changed
/// without corresponding compile-time validation evidence in the diff.
/// </summary>
[ArchivedRule("Too library-specific to be broadly useful across the corpus")]
public class GCI0037_AutoMapperIntegrity : RuleBase
{
    public override string Id => "GCI0037";
    public override string Name => "Mapping Profile Integrity";

    // AutoMapper
    private static readonly string[] AutoMapperStrongSignals = ["CreateMap<", ": Profile"];
    private const string AutoMapperMapSignal = ".Map<";
    private const string UsingAutoMapper = "using AutoMapper";

    // Mapster
    private static readonly string[] MapsterSignals = ["TypeAdapterConfig", "IRegister", "config.NewConfig<", "using Mapster"];

    // AgileMapper
    private static readonly string[] AgileMapperSignals = ["Mapper.WhenMapping", "MapperFactory", "using AgileObjects.AgileMapper"];

    // TinyMapper
    private static readonly string[] TinyMapperSignals = ["TinyMapper.Bind<", "using Nelibur.Mapper"];

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var diff = context.Diff;
        var findings = new List<Finding>();

        var addedLines = diff.Files.SelectMany(f => f.AddedLines).ToList();
        var allLines = diff.Files.SelectMany(f => f.Hunks.SelectMany(h => h.Lines)).ToList();

        CheckAutoMapper(addedLines, allLines, findings);
        CheckMapster(addedLines, allLines, findings);
        CheckAgileMapper(addedLines, allLines, findings);
        CheckTinyMapper(addedLines, findings);

        return Task.FromResult(findings);
    }

    private void CheckAutoMapper(List<DiffLine> addedLines, List<DiffLine> allLines, List<Finding> findings)
    {
        bool hasStrongSignal = addedLines.Any(l =>
            AutoMapperStrongSignals.Any(s => l.Content.Contains(s, StringComparison.Ordinal)))
            || allLines.Any(l => l.Content.Contains(UsingAutoMapper, StringComparison.Ordinal));

        if (!hasStrongSignal) return;

        bool hasMapCall = addedLines.Any(l => l.Content.Contains(AutoMapperMapSignal, StringComparison.Ordinal));

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
    }

    private void CheckMapster(List<DiffLine> addedLines, List<DiffLine> allLines, List<Finding> findings)
    {
        bool hasSignal = addedLines.Any(l =>
            MapsterSignals.Any(s => l.Content.Contains(s, StringComparison.Ordinal)));

        if (!hasSignal) return;

        bool hasValidation = allLines.Any(l =>
            l.Content.Contains("TypeAdapterConfig.GlobalSettings.Compile()", StringComparison.Ordinal)
            || l.Content.Contains(".Compile()", StringComparison.Ordinal));

        if (!hasValidation)
        {
            var evidenceSignals = MapsterSignals
                .Where(s => addedLines.Any(l => l.Content.Contains(s, StringComparison.Ordinal)))
                .ToList();

            findings.Add(CreateFinding(
                summary: "Mapster mapping changed without TypeAdapterConfig.Compile() validation in this diff.",
                evidence: $"Mapster signal(s) found in added lines: {string.Join(", ", evidenceSignals)}.",
                whyItMatters: "Unmapped or misconfigured Mapster configurations fail at runtime. Without TypeAdapterConfig.GlobalSettings.Compile() in tests, broken mappings ship to production.",
                suggestedAction: "Call TypeAdapterConfig.GlobalSettings.Compile() in a unit test to validate all Mapster mappings at build time.",
                confidence: Confidence.Medium));
        }
    }

    private void CheckAgileMapper(List<DiffLine> addedLines, List<DiffLine> allLines, List<Finding> findings)
    {
        bool hasSignal = addedLines.Any(l =>
            AgileMapperSignals.Any(s => l.Content.Contains(s, StringComparison.Ordinal)));

        if (!hasSignal) return;

        bool hasValidation = allLines.Any(l =>
            l.Content.Contains("Mapper.GetPlanFor<", StringComparison.Ordinal));

        if (!hasValidation)
        {
            var evidenceSignals = AgileMapperSignals
                .Where(s => addedLines.Any(l => l.Content.Contains(s, StringComparison.Ordinal)))
                .ToList();

            findings.Add(CreateFinding(
                summary: "AgileMapper mapping changed without Mapper.GetPlanFor< test evidence in this diff.",
                evidence: $"AgileMapper signal(s) found in added lines: {string.Join(", ", evidenceSignals)}.",
                whyItMatters: "Misconfigured AgileMapper plans fail at runtime. Without plan validation in tests, broken mappings ship to production.",
                suggestedAction: "Use Mapper.GetPlanFor<Source>().To<Destination>() in a test to validate AgileMapper configurations.",
                confidence: Confidence.Medium));
        }
    }

    private void CheckTinyMapper(List<DiffLine> addedLines, List<Finding> findings)
    {
        bool hasSignal = addedLines.Any(l =>
            TinyMapperSignals.Any(s => l.Content.Contains(s, StringComparison.Ordinal)));

        if (!hasSignal) return;

        var evidenceSignals = TinyMapperSignals
            .Where(s => addedLines.Any(l => l.Content.Contains(s, StringComparison.Ordinal)))
            .ToList();

        findings.Add(CreateFinding(
            summary: "TinyMapper binding changed — no built-in compile-time validation available.",
            evidence: $"TinyMapper signal(s) found in added lines: {string.Join(", ", evidenceSignals)}.",
            whyItMatters: "TinyMapper has no built-in compile-time validation. Broken bindings fail silently at runtime and are not caught by tests unless explicitly exercised.",
            suggestedAction: "Add a test that calls TinyMapper.Map<Source, Destination>() with representative data to verify the binding works correctly.",
            confidence: Confidence.Medium));
    }
}

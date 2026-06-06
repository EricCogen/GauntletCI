// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Configuration;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.Semantics;

namespace GauntletCI.Core.Rules.Delivery;

/// <summary>
/// Enriches findings with semantic counterfactual witnesses from patch operations (PG-SEMANTICS).
/// </summary>
public static class SemanticFindingProcessor
{
    /// <summary>Result of semantic enrichment.</summary>
    public sealed class Result
    {
        public required IReadOnlyList<Finding> Findings { get; init; }
        public int BoostsApplied { get; init; }
    }

    /// <summary>
    /// Boosts confidence and attaches counterfactual notes when patch semantics align with a finding line.
    /// </summary>
    public static Result Apply(
        IReadOnlyList<Finding> findings,
        DiffContext diff,
        SemanticsConfig config)
    {
        ArgumentNullException.ThrowIfNull(findings);
        ArgumentNullException.ThrowIfNull(diff);
        ArgumentNullException.ThrowIfNull(config);

        if (!config.Enabled || findings.Count == 0)
        {
            return new Result { Findings = findings.ToList(), BoostsApplied = 0 };
        }

        var operations = PatchOperationAnalyzer.Analyze(diff);
        var patchModel = DiffToPatchAdapter.FromDiffContext(diff);
        var counterfactuals = PatchCounterfactualGenerator.GenerateCounterfactuals(
            operations,
            new PatchTransformationCollection(),
            patchModel);

        var boostRules = config.BoostRules.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var highRiskByLine = operations.All
            .Where(o => o.Kind == PatchOperationKind.ConditionalModified && o.RiskLevel >= 0.85)
            .GroupBy(o => Key(o.FilePath, o.NewLineNumber))
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var enriched = new List<Finding>(findings.Count);
        var boosts = 0;

        foreach (var finding in findings)
        {
            if (!finding.Line.HasValue || string.IsNullOrEmpty(finding.FilePath))
            {
                enriched.Add(finding);
                continue;
            }

            if (!boostRules.Contains(finding.RuleId) ||
                !highRiskByLine.TryGetValue(Key(finding.FilePath, finding.Line.Value), out var operation))
            {
                enriched.Add(finding);
                continue;
            }

            var line = finding.Line!.Value;
            var witness = FindWitness(counterfactuals, finding.FilePath, line);
            var note = witness is null
                ? $"Semantic witness: {operation.Description}"
                : $"Counterfactual: {witness.Description} — {operation.Description}";

            enriched.Add(CloneWithEnrichment(finding, note));
            boosts++;
        }

        return new Result { Findings = enriched, BoostsApplied = boosts };
    }

    private static Finding CloneWithEnrichment(Finding source, string counterfactualNote) => new()
    {
        RuleId = source.RuleId,
        RuleName = source.RuleName,
        Summary = source.Summary,
        Evidence = source.Evidence,
        WhyItMatters = source.WhyItMatters,
        SuggestedAction = source.SuggestedAction,
        Confidence = Confidence.High,
        Severity = source.Severity,
        SeverityOverride = source.SeverityOverride,
        FilePath = source.FilePath,
        Line = source.Line,
        LlmExplanation = source.LlmExplanation,
        ExpertContext = source.ExpertContext,
        CodeSnippet = source.CodeSnippet,
        CoverageNote = string.IsNullOrEmpty(source.CoverageNote)
            ? counterfactualNote
            : source.CoverageNote + " | " + counterfactualNote,
        TicketContext = source.TicketContext,
    };

    private static PatchCounterfactual? FindWitness(
        PatchCounterfactualCollection counterfactuals,
        string? filePath,
        int line)
    {
        var normalized = NormalizePath(filePath);
        return counterfactuals.All.FirstOrDefault(c =>
                   c.PrimaryLineNumber == line &&
                   string.Equals(NormalizePath(c.FilePath), normalized, StringComparison.OrdinalIgnoreCase))
               ?? counterfactuals.All.FirstOrDefault(c =>
                   string.Equals(NormalizePath(c.FilePath), normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static string Key(string? filePath, int? line) =>
        $"{NormalizePath(filePath)}|{line ?? 0}";

    private static string NormalizePath(string? path) =>
        (path ?? string.Empty).Replace('\\', '/').TrimStart('/');
}

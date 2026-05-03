// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis.Enrichment;
using GauntletCI.Core.Model;

namespace GauntletCI.Cli.Enrichment.Stages;

/// <summary>
/// Enriches findings with expert knowledge facts that match the rule or pattern.
/// Enables configuration of domain-specific knowledge (e.g., framework-specific guidance, company policies).
/// </summary>
public class ExpertKnowledgeEnricher : IFindingEnricher
{
    private readonly IReadOnlyDictionary<string, ExpertFact> _knowledgeBase;

    public string StageName => "ExpertKnowledge";
    public bool IsAvailable => _knowledgeBase.Count > 0;
    public IReadOnlySet<string> DependsOn => new HashSet<string>();  // No dependencies

    /// <summary>
    /// Creates an enricher with the provided knowledge base.
    /// Knowledge base maps rule IDs or pattern keywords to expert facts.
    /// </summary>
    /// <param name="knowledgeBase">Dictionary mapping keys (rule IDs, patterns) to expert facts.</param>
    public ExpertKnowledgeEnricher(IReadOnlyDictionary<string, ExpertFact> knowledgeBase)
    {
        _knowledgeBase = knowledgeBase ?? new Dictionary<string, ExpertFact>();
    }

    /// <summary>
    /// Enriches the finding with expert knowledge if a matching fact exists.
    /// Looks up by RuleId first, then by rule name patterns.
    /// </summary>
    public Task<bool> EnrichAsync(Finding finding, CancellationToken ct = default)
    {
        if (finding is null || !IsAvailable)
            return Task.FromResult(false);

        // Skip if already enriched
        if (finding.ExpertContext != null)
            return Task.FromResult(false);

        // Try exact match on RuleId first
        if (_knowledgeBase.TryGetValue(finding.RuleId, out var fact))
        {
            finding.ExpertContext = fact;
            return Task.FromResult(true);
        }

        // Try case-insensitive match on RuleName
        var nameKey = _knowledgeBase.Keys.FirstOrDefault(k =>
            k.Equals(finding.RuleName, StringComparison.OrdinalIgnoreCase));

        if (nameKey != null && _knowledgeBase.TryGetValue(nameKey, out var nameFact))
        {
            finding.ExpertContext = nameFact;
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }
}

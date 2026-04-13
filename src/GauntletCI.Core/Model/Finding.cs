// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Core.Model;

public class Finding
{
    public required string RuleId { get; set; }
    public required string RuleName { get; set; }
    public required string Summary { get; set; }
    public required string Evidence { get; set; }       // file + line + snippet
    public required string WhyItMatters { get; set; }
    public required string SuggestedAction { get; set; }
    public Confidence Confidence { get; set; }
    public string? FilePath { get; set; }
    public int? Line { get; set; }
    public string? LlmExplanation { get; set; }         // optional LLM enrichment
}

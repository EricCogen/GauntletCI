// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Corpus.Models;

public sealed class ExpectedFinding
{
    public string RuleId { get; init; } = string.Empty;
    public bool ShouldTrigger { get; init; }
    public double ExpectedConfidence { get; init; }
    public string Reason { get; init; } = string.Empty;
    public LabelSource LabelSource { get; init; }
    public bool IsInconclusive { get; init; }
}

// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Corpus.Models;

public sealed class ActualFinding
{
    public string RuleId { get; init; } = string.Empty;
    public bool DidTrigger { get; init; }
    public double ActualConfidence { get; init; }
    public string Message { get; init; } = string.Empty;
    public string ChangeImplication { get; init; } = string.Empty;
    public string Evidence { get; init; } = string.Empty;
    public long ExecutionTimeMs { get; init; }
    public string? FilePath { get; init; }
}

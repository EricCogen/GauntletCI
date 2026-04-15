// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Corpus.Labeling;

public sealed record LlmLabelResult(
    bool ShouldTrigger,
    double Confidence,
    string Reason,
    bool IsInconclusive = false);

public interface ILlmLabeler
{
    /// <summary>Returns null if the LLM could not produce a label (quota, no key, etc.).</summary>
    Task<LlmLabelResult?> ClassifyAsync(
        string ruleId,
        string findingMessage,
        string evidence,
        string? filePath,
        IEnumerable<string> reviewCommentBodies,
        string diffSnippet,
        CancellationToken ct = default);
}

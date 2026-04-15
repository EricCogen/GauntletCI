// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Corpus.Labeling;

/// <summary>No-op labeler used when no API key is configured.</summary>
public sealed class NullLlmLabeler : ILlmLabeler
{
    public Task<LlmLabelResult?> ClassifyAsync(
        string ruleId,
        string findingMessage,
        string evidence,
        string? filePath,
        IEnumerable<string> reviewCommentBodies,
        string diffSnippet,
        CancellationToken ct = default)
        => Task.FromResult<LlmLabelResult?>(null);
}

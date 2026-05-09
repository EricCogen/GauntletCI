// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Corpus.Models;

public sealed class PullRequestCandidate
{
    public string Source { get; init; } = string.Empty;
    public string RepoOwner { get; init; } = string.Empty;
    public string RepoName { get; init; } = string.Empty;
    public int PullRequestNumber { get; init; }
    public string Url { get; init; } = string.Empty;
    public string Language { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
    public string CandidateReason { get; init; } = string.Empty;
    public int ReviewCommentCount { get; init; }
    public bool IsDraft { get; init; }
    public MergeState MergeState { get; init; }
    public string? RawMetadataJson { get; init; }
}

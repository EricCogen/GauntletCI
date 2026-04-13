// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Corpus.Models;

public sealed class HydratedPullRequest
{
    public string RepoOwner { get; init; } = string.Empty;
    public string RepoName { get; init; } = string.Empty;
    public int PullRequestNumber { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public string BaseSha { get; init; } = string.Empty;
    public string HeadSha { get; init; } = string.Empty;
    public string MergeCommitSha { get; init; } = string.Empty;
    public int FilesChangedCount { get; init; }
    public int Additions { get; init; }
    public int Deletions { get; init; }
    public IReadOnlyList<ChangedFile> ChangedFiles { get; init; } = [];
    public IReadOnlyList<ReviewComment> ReviewComments { get; init; } = [];
    public IReadOnlyList<string> Commits { get; init; } = [];
    public string DiffText { get; init; } = string.Empty;
    public string PatchText { get; init; } = string.Empty;
    public string? RawApiPayloadJson { get; init; }
    public DateTime HydratedAtUtc { get; init; }
}

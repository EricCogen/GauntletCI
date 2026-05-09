// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Corpus.Models;

public sealed class DiscoveryQuery
{
    public IReadOnlyList<string> Languages { get; init; } = [];
    public IReadOnlyList<string> RepoAllowList { get; init; } = [];
    public IReadOnlyList<string> RepoBlockList { get; init; } = [];
    public DateTime? StartDateUtc { get; init; }
    public DateTime? EndDateUtc { get; init; }
    public int MinReviewComments { get; init; }
    public int MinStars { get; init; }
    public int MaxStars { get; init; } = int.MaxValue;
    public int MaxCandidates { get; init; } = 100;
    /// <summary>
    /// Maximum candidates accepted from any single repo when using the allowlist.
    /// 0 = unlimited (all candidates from each repo count toward MaxCandidates).
    /// </summary>
    public int PerRepoLimit { get; init; }
    public bool IncludeDrafts { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
}

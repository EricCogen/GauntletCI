// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Corpus.Models;

public sealed class FixtureMetadata
{
    public string FixtureId { get; init; } = string.Empty;
    public FixtureTier Tier { get; init; }
    public string Repo { get; init; } = string.Empty;
    public int PullRequestNumber { get; init; }
    public string Language { get; init; } = string.Empty;
    public IReadOnlyList<string> RuleIds { get; init; } = [];
    public IReadOnlyList<string> Tags { get; init; } = [];
    public PrSizeBucket PrSizeBucket { get; init; }
    public int FilesChanged { get; init; }
    public bool HasTestsChanged { get; init; }
    public bool HasReviewComments { get; init; }
    public string BaseSha { get; init; } = string.Empty;
    public string HeadSha { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
}

// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Corpus.Interfaces;
using GauntletCI.Corpus.Models;

namespace GauntletCI.Corpus.Discovery;

/// <summary>
/// Trivial discovery provider that converts a static list of PR URLs into candidates.
/// This is the first provider — no network calls beyond what the caller already has.
/// </summary>
public sealed class ManualSeedProvider : IDiscoveryProvider
{
    private readonly IReadOnlyList<string> _urls;

    public ManualSeedProvider(IReadOnlyList<string> prUrls) => _urls = prUrls;

    public string GetProviderName() => "manual";

    public bool SupportsIncrementalSync => false;

    public Task<IReadOnlyList<PullRequestCandidate>> SearchCandidatesAsync(
        DiscoveryQuery query, CancellationToken ct = default)
    {
        var candidates = _urls
            .Select(url =>
            {
                var (owner, repo, prNumber) = Hydration.GitHubRestHydrator.ParsePrUrl(url);
                return new PullRequestCandidate
                {
                    Source            = GetProviderName(),
                    RepoOwner         = owner,
                    RepoName          = repo,
                    PullRequestNumber = prNumber,
                    Url               = url,
                    CandidateReason   = "manual-seed",
                };
            })
            .Take(query.MaxCandidates)
            .ToList();

        return Task.FromResult<IReadOnlyList<PullRequestCandidate>>(candidates);
    }
}

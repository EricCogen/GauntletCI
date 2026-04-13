// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Corpus.Models;

namespace GauntletCI.Corpus.Interfaces;

public interface IDiscoveryProvider
{
    string GetProviderName();
    bool SupportsIncrementalSync { get; }
    Task<IReadOnlyList<PullRequestCandidate>> SearchCandidatesAsync(
        DiscoveryQuery query, CancellationToken cancellationToken = default);
}

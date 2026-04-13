// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Corpus.Models;

namespace GauntletCI.Corpus.Interfaces;

public interface IPullRequestHydrator
{
    Task<HydratedPullRequest> HydrateAsync(
        PullRequestCandidate candidate, CancellationToken cancellationToken = default);

    Task<HydratedPullRequest> HydrateFromUrlAsync(
        string url, CancellationToken cancellationToken = default);
}

// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Corpus.Models;

namespace GauntletCI.Corpus.Interfaces;

public interface IRuleCorpusRunner
{
    Task<IReadOnlyList<ActualFinding>> RunAsync(
        string fixtureId, string diffText, CancellationToken cancellationToken = default);
}

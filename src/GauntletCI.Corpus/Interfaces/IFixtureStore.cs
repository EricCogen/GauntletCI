// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Corpus.Models;

namespace GauntletCI.Corpus.Interfaces;

public interface IFixtureStore
{
    Task SaveMetadataAsync(FixtureMetadata metadata, CancellationToken cancellationToken = default);
    Task<FixtureMetadata?> GetMetadataAsync(string fixtureId, CancellationToken cancellationToken = default);
    Task SaveExpectedFindingsAsync(string fixtureId, IReadOnlyList<ExpectedFinding> findings, CancellationToken cancellationToken = default);
    Task SaveActualFindingsAsync(string fixtureId, string runId, IReadOnlyList<ActualFinding> findings, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FixtureMetadata>> ListFixturesAsync(FixtureTier? tier = null, CancellationToken cancellationToken = default);
}

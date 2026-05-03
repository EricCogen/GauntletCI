// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Analysis.Enrichment;

/// <summary>
/// A no-op enricher that does nothing, used when a particular enrichment service is unavailable.
/// Allows the pipeline to continue even if optional enrichment services are offline.
/// </summary>
public class NullFindingEnricher : IFindingEnricher
{
    private readonly string _stageName;

    public string StageName => _stageName;
    public bool IsAvailable => false;  // Always unavailable, so pipeline skips it
    public IReadOnlySet<string> DependsOn => new HashSet<string>();

    public NullFindingEnricher(string stageName)
    {
        _stageName = stageName ?? throw new ArgumentNullException(nameof(stageName));
    }

    public Task<bool> EnrichAsync(Finding finding, CancellationToken ct = default)
    {
        // No-op: do nothing
        return Task.FromResult(false);
    }
}

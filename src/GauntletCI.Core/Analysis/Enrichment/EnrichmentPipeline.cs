// SPDX-License-Identifier: Elastic-2.0
using System.Diagnostics;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Analysis.Enrichment;

/// <summary>
/// Coordinates execution of multiple finding enrichers in dependency order.
/// Validates that all enrichers are registered and dependencies are satisfied before execution.
/// Provides diagnostics on enrichment performance and availability.
/// </summary>
public class EnrichmentPipeline
{
    private readonly IReadOnlyList<IFindingEnricher> _enrichers;
    private readonly IReadOnlyDictionary<string, IFindingEnricher> _enrichersByName;
    private readonly IReadOnlyList<string> _executionOrder;

    /// <summary>
    /// All enrichers registered in this pipeline, in discovery order.
    /// </summary>
    public IReadOnlyList<IFindingEnricher> Enrichers => _enrichers;

    /// <summary>
    /// The order in which enrichers will be executed, respecting dependencies.
    /// </summary>
    public IReadOnlyList<string> ExecutionOrder => _executionOrder;

    /// <summary>
    /// Creates a pipeline with the given enrichers. Validates all dependencies are satisfied.
    /// </summary>
    /// <param name="enrichers">All enrichers to register. Order does not matter; execution order is computed from dependencies.</param>
    /// <exception cref="InvalidOperationException">Thrown if dependencies are unsatisfied or circular.</exception>
    public EnrichmentPipeline(IEnumerable<IFindingEnricher> enrichers)
    {
        _enrichers = enrichers.ToList();
        _enrichersByName = _enrichers.ToDictionary(e => e.StageName, StringComparer.OrdinalIgnoreCase);

        // Validate all dependencies exist
        foreach (var enricher in _enrichers)
        {
            foreach (var dep in enricher.DependsOn)
            {
                if (!_enrichersByName.ContainsKey(dep))
                    throw new InvalidOperationException(
                        $"Enricher '{enricher.StageName}' depends on '{dep}' which is not registered. " +
                        $"Available: {string.Join(", ", _enrichersByName.Keys.OrderBy(k => k))}");
            }
        }

        // Compute execution order via topological sort
        _executionOrder = TopologicalSort();
    }

    /// <summary>
    /// Enriches all findings in a collection by running them through the pipeline.
    /// Each enricher runs on all findings sequentially, respecting dependency order.
    /// </summary>
    /// <param name="findings">All findings to enrich. May be modified in-place.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Diagnostics on enrichment execution (stages, counts, timing).</returns>
    public async Task<EnrichmentResult> EnrichAsync(IEnumerable<Finding> findings, CancellationToken ct = default)
    {
        var findingList = findings.ToList();
        var result = new EnrichmentResult();

        foreach (var stageName in _executionOrder)
        {
            ct.ThrowIfCancellationRequested();

            var enricher = _enrichersByName[stageName];
            if (!enricher.IsAvailable)
            {
                result.SkippedStages.Add(stageName);
                continue;
            }

            var sw = Stopwatch.StartNew();
            int successCount = 0;
            int skipCount = 0;

            foreach (var finding in findingList)
            {
                try
                {
                    var enriched = await enricher.EnrichAsync(finding, ct);
                    if (enriched) successCount++;
                    else skipCount++;
                }
                catch (Exception ex)
                {
                    result.StageErrors[stageName] = ex.Message;
                    // Continue with other findings even if one fails
                }
            }

            sw.Stop();
            result.ExecutedStages.Add(new StageMetric(stageName, sw.ElapsedMilliseconds, successCount, skipCount, findingList.Count));
        }

        return result;
    }

    /// <summary>
    /// Topologically sorts enrichers by their dependencies.
    /// Uses Kahn's algorithm to detect cycles and produce valid ordering.
    /// </summary>
    private List<string> TopologicalSort()
    {
        // Build dependency graph
        var inDegree = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var graph = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var enricher in _enrichers)
        {
            inDegree[enricher.StageName] = enricher.DependsOn.Count;
            graph[enricher.StageName] = new();
        }

        // Build reverse edges (depender → dependency becomes dependency → depender)
        foreach (var enricher in _enrichers)
        {
            foreach (var dep in enricher.DependsOn)
            {
                graph[dep].Add(enricher.StageName);
            }
        }

        // Kahn's algorithm
        var queue = new Queue<string>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var result = new List<string>();

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            result.Add(current);

            foreach (var neighbor in graph[current])
            {
                inDegree[neighbor]--;
                if (inDegree[neighbor] == 0)
                    queue.Enqueue(neighbor);
            }
        }

        if (result.Count != _enrichers.Count)
            throw new InvalidOperationException(
                $"Circular dependency detected in enrichment pipeline. " +
                $"Enrichers with cycles: {string.Join(", ", inDegree.Where(kv => kv.Value > 0).Select(kv => kv.Key).OrderBy(k => k))}");

        return result;
    }
}

/// <summary>
/// Metrics and diagnostics from a single enrichment pipeline run.
/// </summary>
public class EnrichmentResult
{
    /// <summary>All enricher stages that executed successfully.</summary>
    public List<StageMetric> ExecutedStages { get; } = new();

    /// <summary>Enricher stages that were skipped (IsAvailable == false).</summary>
    public List<string> SkippedStages { get; } = new();

    /// <summary>Any errors encountered during enrichment, keyed by stage name.</summary>
    public Dictionary<string, string> StageErrors { get; } = new();

    /// <summary>Total findings processed by the pipeline.</summary>
    public int TotalFindingsEnriched => ExecutedStages.Sum(s => s.FindingCount);

    /// <summary>Total enrichments completed (sum of all success counts across stages).</summary>
    public int TotalEnrichmentsCounts => ExecutedStages.Sum(s => s.SuccessCount);

    /// <summary>Total time spent in enrichment pipeline.</summary>
    public long TotalElapsedMilliseconds => ExecutedStages.Sum(s => s.ElapsedMilliseconds);
}

/// <summary>
/// Metrics for a single enricher stage within an enrichment run.
/// </summary>
public class StageMetric
{
    public StageMetric(string stageName, long elapsedMs, int successCount, int skipCount, int findingCount)
    {
        StageName = stageName;
        ElapsedMilliseconds = elapsedMs;
        SuccessCount = successCount;
        SkipCount = skipCount;
        FindingCount = findingCount;
    }

    /// <summary>Name of the enricher stage.</summary>
    public string StageName { get; }

    /// <summary>Time spent in this stage (milliseconds).</summary>
    public long ElapsedMilliseconds { get; }

    /// <summary>Number of findings successfully enriched by this stage.</summary>
    public int SuccessCount { get; }

    /// <summary>Number of findings skipped by this stage (e.g., already enriched, not applicable).</summary>
    public int SkipCount { get; }

    /// <summary>Total findings processed by this stage.</summary>
    public int FindingCount { get; }

    /// <summary>Success rate as a percentage (0-100).</summary>
    public decimal SuccessRate => FindingCount == 0 ? 0 : (SuccessCount * 100m) / FindingCount;
}

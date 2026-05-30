// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Core.Rules.Delivery;

/// <summary>Metrics from <see cref="FindingDeliveryProcessor"/> for diagnostics and telemetry.</summary>
public sealed class FindingDeliverySummary
{
    /// <summary>Findings count before delivery processing.</summary>
    public int InputCount { get; init; }

    /// <summary>Findings count after delivery processing.</summary>
    public int OutputCount { get; init; }

    /// <summary>Findings removed because file-level rules were demoted when line-anchored findings exist.</summary>
    public int DroppedByFileLevelDemotion { get; init; }

    /// <summary>Findings removed by per-rule per-file caps.</summary>
    public int DroppedByPerRuleCap { get; init; }

    /// <summary>Findings removed by the global cap after ranking.</summary>
    public int DroppedByGlobalCap { get; init; }

    /// <summary>Number of findings whose confidence was boosted by rule coordination.</summary>
    public int CoordinationBoostsApplied { get; init; }

    /// <summary>Findings removed because the anchored added line matches a removed line (relocated code).</summary>
    public int DroppedByProvenanceFilter { get; init; }

    /// <summary>Findings removed because the repo was classified as a class library.</summary>
    public int DroppedByDomainFilter { get; init; }
}

// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Model;

namespace GauntletCI.Llm;

public interface ILlmEngine
{
    bool IsAvailable { get; }
    Task<string> EnrichFindingAsync(Finding finding, CancellationToken ct = default);
    Task<string> SummarizeReportAsync(IEnumerable<Finding> findings, CancellationToken ct = default);
}

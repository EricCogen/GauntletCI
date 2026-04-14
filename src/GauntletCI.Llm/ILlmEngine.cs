// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Model;

namespace GauntletCI.Llm;

public interface ILlmEngine : IDisposable
{
    bool IsAvailable { get; }
    Task<string> EnrichFindingAsync(Finding finding, CancellationToken ct = default);
    Task<string> SummarizeReportAsync(IEnumerable<Finding> findings, CancellationToken ct = default);
    /// <summary>Sends a raw prompt and returns the model's completion text.</summary>
    Task<string> CompleteAsync(string prompt, CancellationToken ct = default);
}

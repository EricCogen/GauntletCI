// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Model;

namespace GauntletCI.Llm;

/// <summary>
/// Abstraction over local and remote LLM backends used to enrich findings and summarize reports.
/// Implementations must be safe to call from multiple threads as long as they are not disposed.
/// </summary>
public interface ILlmEngine : IDisposable
{
    /// <summary>Returns <see langword="true"/> when the underlying model is loaded and ready to accept prompts.</summary>
    bool IsAvailable { get; }

    /// <summary>Generates a one-sentence plain-English explanation of why a finding is risky.</summary>
    /// <param name="finding">The finding to enrich with LLM-generated context.</param>
    /// <param name="ct">Token used to cancel the request.</param>
    Task<string> EnrichFindingAsync(Finding finding, CancellationToken ct = default);

    /// <summary>Produces a short paragraph summarizing the overall risk across all findings in a report.</summary>
    /// <param name="findings">The full set of findings from a single analysis run.</param>
    /// <param name="ct">Token used to cancel the request.</param>
    Task<string> SummarizeReportAsync(IEnumerable<Finding> findings, CancellationToken ct = default);

    /// <summary>Sends a raw prompt and returns the model's completion text.</summary>
    Task<string> CompleteAsync(string prompt, CancellationToken ct = default);
}

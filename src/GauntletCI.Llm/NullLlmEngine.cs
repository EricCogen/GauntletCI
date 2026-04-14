// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Model;

namespace GauntletCI.Llm;

/// <summary>
/// No-op LLM engine used when --with-llm is not passed or no model is available.
/// </summary>
public sealed class NullLlmEngine : ILlmEngine
{
    /// <summary>Always <see langword="false"/>; this engine performs no inference.</summary>
    public bool IsAvailable => false;

    /// <summary>Returns an empty string without calling any model.</summary>
    public Task<string> EnrichFindingAsync(Finding finding, CancellationToken ct = default)
        => Task.FromResult(string.Empty);

    /// <summary>Returns an empty string without calling any model.</summary>
    public Task<string> SummarizeReportAsync(IEnumerable<Finding> findings, CancellationToken ct = default)
        => Task.FromResult(string.Empty);

    /// <summary>Returns an empty string without calling any model.</summary>
    public Task<string> CompleteAsync(string prompt, CancellationToken ct = default)
        => Task.FromResult(string.Empty);

    /// <summary>No-op; this engine holds no resources.</summary>
    public void Dispose() { }
}

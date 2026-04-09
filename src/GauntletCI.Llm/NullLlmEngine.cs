// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Model;

namespace GauntletCI.Llm;

/// <summary>
/// No-op LLM engine used when --with-llm is not passed or no model is available.
/// </summary>
public sealed class NullLlmEngine : ILlmEngine
{
    public bool IsAvailable => false;

    public Task<string> EnrichFindingAsync(Finding finding, CancellationToken ct = default)
        => Task.FromResult(string.Empty);

    public Task<string> SummarizeReportAsync(IEnumerable<Finding> findings, CancellationToken ct = default)
        => Task.FromResult(string.Empty);

    public void Dispose() { }
}

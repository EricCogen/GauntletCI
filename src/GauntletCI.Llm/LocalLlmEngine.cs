// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Model;

namespace GauntletCI.Llm;

/// <summary>
/// Local ONNX-based LLM engine stub.
/// Real integration with Microsoft.ML.OnnxRuntimeGenAI.Managed is deferred.
/// Model would be loaded from: ~/.gauntletci/models/phi3-mini
/// </summary>
public sealed class LocalLlmEngine : ILlmEngine
{
    // TODO: Integrate Microsoft.ML.OnnxRuntimeGenAI.Managed here.
    // 1. Load model from modelPath in constructor.
    // 2. EnrichFindingAsync: build a prompt from the Finding and run inference.
    // 3. SummarizeReportAsync: build a summary prompt from all findings.
    private readonly string _modelPath;

    public LocalLlmEngine(string? modelPath = null)
    {
        _modelPath = modelPath
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".gauntletci", "models", "phi3-mini");
    }

    public bool IsAvailable => false; // Set to true once ONNX model is loaded

    public Task<string> EnrichFindingAsync(Finding finding, CancellationToken ct = default)
        => Task.FromResult(string.Empty);

    public Task<string> SummarizeReportAsync(IEnumerable<Finding> findings, CancellationToken ct = default)
        => Task.FromResult(string.Empty);
}

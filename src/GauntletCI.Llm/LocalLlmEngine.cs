// SPDX-License-Identifier: Elastic-2.0
using System.Diagnostics;
using System.Text;
using GauntletCI.Core.Model;
using Microsoft.ML.OnnxRuntimeGenAI;

namespace GauntletCI.Llm;

/// <summary>
/// Local ONNX LLM engine using Microsoft.ML.OnnxRuntimeGenAI with Phi-3 Mini.
/// Runs on GPU via DirectML when available; falls back to CPU automatically.
/// Model is loaded lazily on first use and cached for the lifetime of this instance.
/// Dispose when done to release native resources.
/// </summary>
public sealed class LocalLlmEngine : ILlmEngine, IDisposable
{
    private const int MaxPromptsPerRun = 10;
    private const int MaxOutputTokens = 256;

    private readonly string _modelPath;
    private readonly object _lock = new();

    private OgaHandle? _ogaHandle;
    private Model? _model;
    private Tokenizer? _tokenizer;
    private TokenizerStream? _tokenizerStream;
    private bool _loadFailed;
    private int _promptsUsed;
    private bool _disposed;

    public LocalLlmEngine(string? modelPath = null)
    {
        _modelPath = modelPath
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".gauntletci", "models", "phi3-mini");
    }

    public bool IsAvailable
    {
        get
        {
            if (_loadFailed || _disposed) return false;
            if (_model != null) return true;
            return new ModelDownloader(_modelPath).IsModelCached();
        }
    }

    public async Task<string> EnrichFindingAsync(Finding finding, CancellationToken ct = default)
    {
        var prompt = PromptTemplates.EnrichFinding(
            finding.RuleId, finding.RuleName, finding.Summary, finding.Evidence);
        return await RunInferenceAsync(prompt, ct);
    }

    public async Task<string> SummarizeReportAsync(IEnumerable<Finding> findings, CancellationToken ct = default)
    {
        var summaries = findings.Select(f => f.Summary);
        var prompt = PromptTemplates.SummarizeReport(summaries);
        return await RunInferenceAsync(prompt, ct);
    }

    private async Task<string> RunInferenceAsync(string prompt, CancellationToken ct)
    {
        if (_disposed) return string.Empty;

        if (_promptsUsed >= MaxPromptsPerRun)
        {
            Console.Error.WriteLine("[GauntletCI] LLM prompt cap reached (10 per run). Skipping enrichment.");
            return string.Empty;
        }

        if (!TryEnsureLoaded())
            return string.Empty;

        _promptsUsed++;

        return await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            lock (_lock)
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    var sequences = _tokenizer!.Encode(prompt);
                    using var generatorParams = new GeneratorParams(_model!);
                    generatorParams.SetSearchOption("max_length", MaxOutputTokens);
                    generatorParams.SetSearchOption("do_sample", false);

                    using var generator = new Generator(_model!, generatorParams);
                    generator.AppendTokenSequences(sequences);
                    var sb = new StringBuilder();

                    while (!generator.IsDone())
                    {
                        ct.ThrowIfCancellationRequested();
                        generator.GenerateNextToken();
                        if (generator.IsDone()) break;

                        var token = generator.GetNextTokens()[0];
                        sb.Append(_tokenizerStream!.Decode(token));

                        if (sw.ElapsedMilliseconds > 500)
                        {
                            Console.Error.WriteLine("[GauntletCI] LLM inference exceeded 500ms limit. Truncating.");
                            break;
                        }
                    }

                    return sb.ToString().Trim();
                }
                catch (OperationCanceledException)
                {
                    return string.Empty;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[GauntletCI] LLM inference error: {ex.Message}");
                    return string.Empty;
                }
            }
        }, ct);
    }

    private bool TryEnsureLoaded()
    {
        if (_model != null) return true;
        if (_loadFailed) return false;

        lock (_lock)
        {
            if (_model != null) return true;
            if (_loadFailed) return false;

            try
            {
                if (!new ModelDownloader(_modelPath).IsModelCached())
                {
                    Console.Error.WriteLine(
                        $"[GauntletCI] Model not found at {_modelPath}. " +
                        "Run 'gauntletci init --download-model' to download it.");
                    _loadFailed = true;
                    return false;
                }

                var sw = Stopwatch.StartNew();
                _ogaHandle = new OgaHandle();
                _model = new Model(_modelPath);
                _tokenizer = new Tokenizer(_model);
                _tokenizerStream = _tokenizer.CreateStream();
                sw.Stop();

                if (sw.ElapsedMilliseconds > 3000)
                    Console.Error.WriteLine($"[GauntletCI] Model load took {sw.ElapsedMilliseconds}ms (limit 3000ms).");

                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[GauntletCI] Failed to load LLM model: {ex.Message}");
                _loadFailed = true;
                return false;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        lock (_lock)
        {
            _tokenizerStream?.Dispose();
            _tokenizer?.Dispose();
            _model?.Dispose();
            _ogaHandle?.Dispose();
        }
    }
}

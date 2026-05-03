// SPDX-License-Identifier: Elastic-2.0
using System.Diagnostics;
using System.Text;
using GauntletCI.Core.Model;
using Microsoft.ML.OnnxRuntimeGenAI;

namespace GauntletCI.Llm;

/// <summary>
/// Local ONNX LLM engine using Microsoft.ML.OnnxRuntimeGenAI with Phi-4 Mini.
/// Runs on GPU via DirectML when available; falls back to CPU automatically.
/// Model is loaded lazily on first use and cached for the lifetime of this instance.
/// Dispose when done to release native resources.
/// </summary>
public sealed class LocalLlmEngine : ILlmEngine, IDisposable
{
    private const int DefaultMaxPromptsPerRun = 10;
    private const int DefaultMaxInferenceMs = 60_000;
    private const int MaxOutputTokens = 256;

    private readonly string _modelPath;
    private readonly int _maxPromptsPerRun;
    private readonly int _maxInferenceMs;
    private readonly object _lock = new();

    private OgaHandle? _ogaHandle;
    private Model? _model;
    private Tokenizer? _tokenizer;
    private TokenizerStream? _tokenizerStream;
    private bool _loadFailed;
    private int _promptsUsed;
    private bool _disposed;

    /// <summary>Initializes the engine with the path to the local ONNX model directory.</summary>
    /// <param name="modelPath">Directory containing the ONNX model files; defaults to <c>~/.gauntletci/models/phi4-mini</c> when <see langword="null"/>.</param>
    /// <param name="maxInferenceMs">Per-completion timeout in milliseconds; defaults to 60 000 ms.</param>
    public LocalLlmEngine(string? modelPath = null, int maxInferenceMs = DefaultMaxInferenceMs)
        : this(modelPath, DefaultMaxPromptsPerRun, maxInferenceMs) { }

    /// <summary>Internal constructor used by tests to override the per-run prompt cap and inference timeout.</summary>
    internal LocalLlmEngine(string? modelPath, int maxPromptsPerRun, int maxInferenceMs = DefaultMaxInferenceMs)
    {
        _modelPath = modelPath
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".gauntletci", "models", "phi4-mini");
        _maxPromptsPerRun = maxPromptsPerRun;
        _maxInferenceMs = maxInferenceMs;
    }

    /// <summary>Returns <see langword="true"/> when the model files are cached on disk and this instance has not failed to load.</summary>
    public bool IsAvailable
    {
        get
        {
            if (_loadFailed || _disposed) return false;
            if (_model != null) return true;
            return new ModelDownloader(_modelPath).IsModelCached();
        }
    }

    /// <summary>Builds an enrichment prompt and runs local ONNX inference to explain the finding.</summary>
    /// <remarks>Uses the constrained prompt template with strict anti-hallucination system instructions.</remarks>
    public async Task<string> EnrichFindingAsync(Finding finding, CancellationToken ct = default)
    {
        var prompt = PromptTemplates.EnrichFindingConstrained(
            finding.RuleId, finding.RuleName, finding.Summary, finding.Evidence);
        return await RunInferenceAsync(prompt, ct).ConfigureAwait(false);
    }

    /// <summary>Builds a summarization prompt from all finding summaries and runs local ONNX inference.</summary>
    public async Task<string> SummarizeReportAsync(IEnumerable<Finding> findings, CancellationToken ct = default)
    {
        var summaries = findings.Select(f => f.Summary);
        var prompt = PromptTemplates.SummarizeReport(summaries);
        return await RunInferenceAsync(prompt, ct).ConfigureAwait(false);
    }

    /// <summary>Forwards a pre-built prompt directly to the local ONNX model and returns its completion.</summary>
    public Task<string> CompleteAsync(string prompt, CancellationToken ct = default)
        => RunInferenceAsync(prompt, ct);

    private async Task<string> RunInferenceAsync(string prompt, CancellationToken ct)
    {
        if (_disposed) return string.Empty;

        if (_promptsUsed >= _maxPromptsPerRun)
        {
            Console.Error.WriteLine($"[GauntletCI] LLM prompt cap reached ({_maxPromptsPerRun} per run). Skipping enrichment.");
            return string.Empty;
        }

        if (!TryEnsureLoaded())
            return string.Empty;

        Interlocked.Increment(ref _promptsUsed);

        return await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            lock (_lock)
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    var tokenizer = _tokenizer ?? throw new InvalidOperationException("Tokenizer must not be null after TryEnsureLoaded.");
                    var model = _model ?? throw new InvalidOperationException("Model must not be null after TryEnsureLoaded.");
                    
                    var sequences = tokenizer.Encode(prompt);
                    using var generatorParams = new GeneratorParams(model);
                    generatorParams.SetSearchOption("max_length", MaxOutputTokens);
                    generatorParams.SetSearchOption("do_sample", false);

                    using var generator = new Generator(model, generatorParams);
                    generator.AppendTokenSequences(sequences);
                    var sb = new StringBuilder();

                    while (!generator.IsDone())
                    {
                        ct.ThrowIfCancellationRequested();
                        generator.GenerateNextToken();
                        if (generator.IsDone()) break;

                        var token = generator.GetNextTokens()[0];
                        var tokenizerStream = _tokenizerStream ?? throw new InvalidOperationException("TokenizerStream must not be null after TryEnsureLoaded.");
                        sb.Append(tokenizerStream.Decode(token));

                        if (sw.ElapsedMilliseconds > _maxInferenceMs)
                        {
                            Console.Error.WriteLine($"[GauntletCI] LLM inference exceeded {_maxInferenceMs}ms limit. Truncating.");
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

    /// <summary>Releases the ONNX model, tokenizer, and all associated native handles.</summary>
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

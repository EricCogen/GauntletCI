// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core;

namespace GauntletCI.Llm;

/// <summary>
/// Downloads the Phi-4 Mini INT4 ONNX model from HuggingFace on first run.
/// Files are cached in ~/.gauntletci/models/phi4-mini/.
/// </summary>
public class ModelDownloader
{
    private const string BaseUrl =
        "https://huggingface.co/microsoft/Phi-4-mini-instruct-onnx/resolve/main/" +
        "cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4/";

    private static readonly string[] RequiredFiles =
    [
        "genai_config.json",
        "config.json",
        "added_tokens.json",
        "special_tokens_map.json",
        "tokenizer.json",
        "tokenizer.model",
        "tokenizer_config.json",
        "phi-4-mini-instruct-cpu-int4-rtn-block-32-acc-level-4.onnx",
        "phi-4-mini-instruct-cpu-int4-rtn-block-32-acc-level-4.onnx.data",
    ];

    private readonly string _modelDir;

    public ModelDownloader(string modelDir)
    {
        _modelDir = modelDir;
    }

    public bool IsModelCached() =>
        RequiredFiles.All(f => File.Exists(Path.Combine(_modelDir, f)));

    /// <summary>
    /// Ensures the model is downloaded. Reports progress via <paramref name="progress"/>.
    /// Returns the model directory path when complete.
    /// </summary>
    public async Task<string> EnsureModelAsync(
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        if (IsModelCached())
        {
            progress?.Report("Model already cached.");
            return _modelDir;
        }

        Directory.CreateDirectory(_modelDir);
        progress?.Report($"Downloading Phi-4 Mini (INT4 ONNX) to {_modelDir} ...");
        progress?.Report("Note: model.onnx.data is ~2 GB: this may take several minutes.");

        using var http = HttpClientFactory.GetGenericClient();

        foreach (var file in RequiredFiles)
        {
            ct.ThrowIfCancellationRequested();
            var dest = Path.Combine(_modelDir, file);
            if (File.Exists(dest))
            {
                progress?.Report($"  ✓ {file} (already present)");
                continue;
            }

            var url = BaseUrl + file;
            progress?.Report($"  ↓ {file} ...");

            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var fs = File.Create(dest);
            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);

            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            var buffer = new byte[81920];
            long downloaded = 0;
            int read;

            while ((read = await stream.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
            {
                await fs.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                downloaded += read;
                if (totalBytes > 0 && downloaded % (10 * 1024 * 1024) == 0)
                {
                    var pct = (int)(downloaded * 100 / totalBytes);
                    progress?.Report($"    {file}: {pct}% ({downloaded / 1024 / 1024} MB)");
                }
            }

            progress?.Report($"  ✓ {file}");
        }

        progress?.Report("Model download complete.");
        return _modelDir;
    }
}

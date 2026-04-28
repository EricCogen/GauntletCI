// SPDX-License-Identifier: Elastic-2.0
using System.Diagnostics;
using System.Runtime.InteropServices;
using GauntletCI.Core.Configuration;

namespace GauntletCI.Corpus.Labeling;

/// <summary>
/// Snapshot of the host machine's relevant hardware capabilities for LLM model selection.
/// </summary>
public sealed record HardwareProfile
{
    public long TotalRamBytes  { get; init; }
    public int  CpuCores       { get; init; }
    public long GpuVramBytes   { get; init; }   // 0 = no dedicated GPU detected
    public bool IsAppleSilicon { get; init; }   // unified memory: Metal acceleration

    // Convenience in GB
    public double TotalRamGb => TotalRamBytes / 1_073_741_824.0;
    public double GpuVramGb  => GpuVramBytes  / 1_073_741_824.0;

    /// <summary>
    /// Whether any GPU acceleration is available (dedicated VRAM or Apple unified memory).
    /// </summary>
    public bool HasGpuAcceleration => GpuVramBytes > 0 || IsAppleSilicon;

    /// <summary>
    /// The Ollama model name best suited for this machine's capabilities.
    /// Prefers models that fit comfortably in available memory to avoid swapping.
    /// </summary>
    public string RecommendedModel
    {
        get
        {
            // Apple Silicon: unified RAM works like VRAM: full memory available for inference
            if (IsAppleSilicon)
            {
                if (TotalRamGb >= 32) return "llama3";       // 8B, ~5GB quantized
                if (TotalRamGb >= 16) return "mistral";      // 7B, ~4.5GB quantized
                if (TotalRamGb >= 8)  return LlmDefaults.OllamaModel; // 3.8B, ~2.5GB quantized
                return "tinyllama";
            }

            // Dedicated GPU: VRAM is the binding constraint
            if (GpuVramBytes > 0)
            {
                if (GpuVramGb >= 10) return "llama3";        // 8B fits in 10GB VRAM
                if (GpuVramGb >= 6)  return "mistral";       // 7B Q4 fits in 6GB
                if (GpuVramGb >= 4)  return LlmDefaults.OllamaModel; // 3.8B fits in 4GB (~2.5GB)
                return "tinyllama";
            }

            // CPU-only: system RAM must hold the model + OS overhead (assume 3GB OS headroom)
            var usableGb = TotalRamGb - 3.0;
            if (usableGb >= 8)  return "mistral";            // 7B Q4 needs ~4.5GB
            if (usableGb >= 4)  return LlmDefaults.OllamaModel;      // 3.8B Q4 needs ~2.5GB
            if (usableGb >= 2)  return "tinyllama";          // 1.1B needs ~0.7GB
            return "tinyllama";
        }
    }

    // -----------------------------------------------------------------------
    // Detection
    // -----------------------------------------------------------------------

    /// <summary>Detects the current machine's hardware profile.</summary>
    public static HardwareProfile Detect()
    {
        var ram           = DetectTotalRam();
        var cores         = Environment.ProcessorCount;
        var isApple       = DetectAppleSilicon();
        var vram          = isApple ? 0L : DetectGpuVram();

        return new HardwareProfile
        {
            TotalRamBytes  = ram,
            CpuCores       = cores,
            GpuVramBytes   = vram,
            IsAppleSilicon = isApple,
        };
    }

    // -----------------------------------------------------------------------
    // Detection helpers
    // -----------------------------------------------------------------------

    private static long DetectTotalRam()
    {
        // GCMemoryInfo.TotalAvailableMemoryBytes reflects physical installed RAM on .NET 8
        var info = GC.GetGCMemoryInfo();
        return info.TotalAvailableMemoryBytes > 0
            ? info.TotalAvailableMemoryBytes
            : 0L;
    }

    private static bool DetectAppleSilicon()
    {
        if (!OperatingSystem.IsMacOS()) return false;
        // ARM64 on macOS → Apple Silicon (M1/M2/M3/M4)
        return RuntimeInformation.OSArchitecture == Architecture.Arm64;
    }

    private static long DetectGpuVram()
    {
        // Try nvidia-smi first (works on Linux, Windows, WSL)
        var nvVram = TryNvidiaSmi();
        if (nvVram > 0) return nvVram;

        // Windows fallback: DXGI/WMI via PowerShell
        if (OperatingSystem.IsWindows())
        {
            var wmiVram = TryWmiVram();
            if (wmiVram > 0) return wmiVram;
        }

        return 0L;
    }

    private static long TryNvidiaSmi()
    {
        try
        {
            // Returns total VRAM in MiB for the first GPU
            var psi = new ProcessStartInfo("nvidia-smi",
                "--query-gpu=memory.total --format=csv,noheader,nounits")
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };
            using var proc = Process.Start(psi);
            var output = proc?.StandardOutput.ReadToEnd().Trim();
            proc?.WaitForExit(3000);

            if (long.TryParse(output?.Split('\n')[0].Trim(), out var mib))
                return mib * 1024L * 1024L;
        }
        catch { /* nvidia-smi not available */ }
        return 0L;
    }

    private static long TryWmiVram()
    {
        try
        {
            // PowerShell one-liner: first GPU's AdapterRAM in bytes
            var script = "(Get-CimInstance Win32_VideoController | " +
                         "Select-Object -First 1 -ExpandProperty AdapterRAM)";
            var psi = new ProcessStartInfo("powershell", $"-NoProfile -Command \"{script}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };
            using var proc = Process.Start(psi);
            var output = proc?.StandardOutput.ReadToEnd().Trim();
            proc?.WaitForExit(5000);

            if (long.TryParse(output, out var bytes) && bytes > 0)
                return bytes;
        }
        catch { /* PowerShell not available */ }
        return 0L;
    }

    /// <summary>Returns a human-readable summary for display in CLI output.</summary>
    public string ToSummaryString()
    {
        var ram  = $"{TotalRamGb:F1} GB RAM";
        var cpu  = $"{CpuCores} cores";
        var gpu  = IsAppleSilicon
                       ? "Apple Silicon (unified memory)"
                       : GpuVramBytes > 0
                           ? $"GPU {GpuVramGb:F1} GB VRAM"
                           : "no GPU detected";
        return $"{ram}, {cpu}, {gpu}";
    }
}

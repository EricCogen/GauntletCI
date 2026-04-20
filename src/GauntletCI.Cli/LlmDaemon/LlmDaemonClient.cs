// SPDX-License-Identifier: Elastic-2.0
using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using GauntletCI.Core.Model;
using GauntletCI.Llm;

namespace GauntletCI.Cli.LlmDaemon;

/// <summary>
/// ILlmEngine implementation that communicates with a background LlmDaemonServer over a named pipe.
/// On first use, automatically spawns the daemon and waits up to 30 seconds for it to load the model.
/// Falls back to null (caller should substitute LocalLlmEngine) if the daemon cannot be started.
/// </summary>
internal sealed class LlmDaemonClient : ILlmEngine
{
    private static readonly TimeSpan ConnectProbeTimeout = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan DaemonStartupTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DaemonStartupPollInterval = TimeSpan.FromMilliseconds(500);

    private readonly NamedPipeClientStream _pipe;
    private readonly StreamReader          _reader;
    private readonly StreamWriter          _writer;
    private bool _disposed;

    private LlmDaemonClient(NamedPipeClientStream pipe, StreamReader reader, StreamWriter writer)
    {
        _pipe   = pipe;
        _reader = reader;
        _writer = writer;
    }

    public bool IsAvailable => _pipe.IsConnected && !_disposed;

    /// <summary>
    /// Tries to connect to a running daemon. If none is found and the model is cached,
    /// spawns the daemon, prints a startup notification, then waits up to 30 seconds.
    /// Returns null if the daemon cannot be started or the model is not cached.
    /// </summary>
    public static async Task<LlmDaemonClient?> ConnectOrStartAsync(CancellationToken ct = default)
    {
        // Fast path: daemon already running
        var client = await TryConnectAsync(ct);
        if (client is not null) return client;

        // Only auto-start if the model is already downloaded
        var modelDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".gauntletci", "models", "phi4-mini");
        if (!new ModelDownloader(modelDir).IsModelCached())
            return null;

        if (!TrySpawnDaemon())
            return null;

        // Notify the user immediately — they'll see this before the wait begins
        Console.Error.WriteLine();
        Console.Error.WriteLine(
            "  ℹ  LLM enrichment requested. Starting local model daemon — " +
            "this may take up to 30s on first run.");
        Console.Error.WriteLine(
            "     Subsequent runs will connect instantly once the daemon is warm.");
        Console.Error.WriteLine();

        // Poll until daemon responds or timeout elapses
        var deadline = DateTime.UtcNow + DaemonStartupTimeout;
        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            await Task.Delay(DaemonStartupPollInterval, ct);
            client = await TryConnectAsync(ct);
            if (client is not null) return client;
        }

        Console.Error.WriteLine(
            "  ⚠  LLM daemon did not respond in time. " +
            "Analysis will proceed without LLM enrichment.");
        Console.Error.WriteLine();
        return null;
    }

    private static async Task<LlmDaemonClient?> TryConnectAsync(CancellationToken ct)
    {
        NamedPipeClientStream? pipe = null;
        try
        {
            pipe = new NamedPipeClientStream(".", LlmDaemonServer.PipeName,
                PipeDirection.InOut, PipeOptions.Asynchronous);

            using var probeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            probeCts.CancelAfter(ConnectProbeTimeout);
            await pipe.ConnectAsync(probeCts.Token);

            var reader = new StreamReader(pipe, leaveOpen: true);
            var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };

            // Ping to confirm the server is ready
            await writer.WriteLineAsync(JsonSerializer.Serialize(new DaemonRequest("ping")));
            var raw = await reader.ReadLineAsync(ct);
            if (raw is null) { pipe.Dispose(); return null; }

            var pong = JsonSerializer.Deserialize<DaemonResponse>(raw);
            if (pong?.Ok != true) { pipe.Dispose(); return null; }

            pipe = null; // ownership transferred to client
            return new LlmDaemonClient(
                (NamedPipeClientStream)reader.BaseStream, reader, writer);
        }
        catch
        {
            pipe?.Dispose();
            return null;
        }
    }

    private static bool TrySpawnDaemon()
    {
        try
        {
            // For an installed global tool, Environment.ProcessPath points to the shim.
            // Fallback: let the OS find "gauntletci" on PATH.
            var processPath = Environment.ProcessPath;
            var exe = (processPath is not null &&
                       Path.GetFileNameWithoutExtension(processPath)
                           .Equals("gauntletci", StringComparison.OrdinalIgnoreCase))
                ? processPath
                : "gauntletci";

            var psi = new ProcessStartInfo(exe, "__llm-daemon")
            {
                UseShellExecute = false,
                CreateNoWindow  = true,
            };

            // Start the daemon and immediately release our handle — we don't own its lifetime.
            using var proc = Process.Start(psi);
            return proc is not null;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> EnrichFindingAsync(Finding finding, CancellationToken ct = default)
    {
        var req = new DaemonRequest("enrich",
            RuleId:   finding.RuleId,
            RuleName: finding.RuleName,
            Summary:  finding.Summary,
            Evidence: finding.Evidence);

        var resp = await SendAsync(req, ct);
        return resp?.Result ?? string.Empty;
    }

    public Task<string> SummarizeReportAsync(IEnumerable<Finding> findings, CancellationToken ct = default)
        => Task.FromResult(string.Empty);

    public Task<string> CompleteAsync(string prompt, CancellationToken ct = default)
        => Task.FromResult(string.Empty);

    private async Task<DaemonResponse?> SendAsync(DaemonRequest req, CancellationToken ct)
    {
        if (_disposed || !_pipe.IsConnected) return null;
        try
        {
            await _writer.WriteLineAsync(JsonSerializer.Serialize(req));
            var line = await _reader.ReadLineAsync(ct);
            return line is null ? null : JsonSerializer.Deserialize<DaemonResponse>(line);
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _writer.Dispose(); } catch { }
        try { _reader.Dispose(); } catch { }
        try { _pipe.Dispose();  } catch { }
    }
}

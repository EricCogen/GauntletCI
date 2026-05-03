// SPDX-License-Identifier: Elastic-2.0
using System.IO.Pipes;
using System.Text.Json;
using GauntletCI.Core.Model;
using GauntletCI.Llm;

namespace GauntletCI.Cli.LlmDaemon;

/// <summary>
/// Named-pipe server that keeps a LocalLlmEngine loaded between CLI invocations.
/// Auto-terminates after 30 minutes of idle.  One client served at a time.
/// </summary>
internal static class LlmDaemonServer
{
    private static readonly TimeSpan IdleTimeout          = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan ConnectionPollWindow = TimeSpan.FromSeconds(30);

    internal static string PipeName =>
        $"gauntletci-llm-{Sanitize(Environment.UserName)}";

    internal static string PidFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".gauntletci", "llm-daemon.pid");

    private static string Sanitize(string s) =>
        new(s.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());

    public static async Task RunAsync(CancellationToken ct = default)
    {
        var pidPath = PidFilePath;
        Directory.CreateDirectory(Path.GetDirectoryName(pidPath)!);
        await File.WriteAllTextAsync(pidPath, Environment.ProcessId.ToString(), ct);

        using ILlmEngine engine = new LocalLlmEngine();
        var lastActivity = DateTime.UtcNow;

        try
        {
            while (!ct.IsCancellationRequested && DateTime.UtcNow - lastActivity < IdleTimeout)
            {
                using var pipe = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.InOut,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                // Wait for a client with a rolling poll window so we can re-check idle time
                using var pollCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                pollCts.CancelAfter(ConnectionPollWindow);

                try
                {
                    await pipe.WaitForConnectionAsync(pollCts.Token);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    continue; // poll window elapsed: loop back to check idle timeout
                }

                lastActivity = DateTime.UtcNow;
                await HandleClientAsync(engine, pipe, ct);
                lastActivity = DateTime.UtcNow;
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            try { File.Delete(pidPath); } catch (Exception) { }
        }
    }

    private static async Task HandleClientAsync(ILlmEngine engine, NamedPipeServerStream pipe, CancellationToken ct)
    {
        using var reader = new StreamReader(pipe, leaveOpen: true);
        using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };

        while (pipe.IsConnected && !ct.IsCancellationRequested)
        {
            string? line;
            try { line = await reader.ReadLineAsync(ct); }
            catch (Exception) { break; }
            if (line is null) break;

            DaemonResponse resp;
            try
            {
                DaemonRequest? req;
                try
                {
                    req = JsonSerializer.Deserialize<DaemonRequest>(line);
                }
                catch (JsonException ex)
                {
                    resp = new DaemonResponse(false, $"Invalid JSON: {ex.Message}");
                    goto SendResponse;
                }

                if (req is null)
                {
                    resp = new DaemonResponse(false, "Deserialization resulted in null");
                }
                else
                {
                    resp = req.Op switch
                    {
                        "ping"   => new DaemonResponse(true, "ready"),
                        "enrich" => new DaemonResponse(true, await EnrichAsync(engine, req, ct)),
                        _        => new DaemonResponse(false, $"Unknown op: {req.Op}")
                    };
                }
            }
            catch (Exception ex)
            {
                resp = new DaemonResponse(false, ex.Message);
            }

            SendResponse:
            try { await writer.WriteLineAsync(JsonSerializer.Serialize(resp)); }
            catch (Exception) { break; }
        }
    }

    private static async Task<string> EnrichAsync(ILlmEngine engine, DaemonRequest req, CancellationToken ct)
    {
        var finding = new Finding
        {
            RuleId          = req.RuleId   ?? string.Empty,
            RuleName        = req.RuleName ?? string.Empty,
            Summary         = req.Summary  ?? string.Empty,
            Evidence        = req.Evidence ?? string.Empty,
            WhyItMatters    = string.Empty,
            SuggestedAction = string.Empty,
        };
        return await engine.EnrichFindingAsync(finding, ct);
    }
}

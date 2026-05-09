// SPDX-License-Identifier: Elastic-2.0
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GauntletCI.Core;

namespace GauntletCI.Cli.Licensing;

/// <summary>
/// Validates an active GauntletCI license against the remote status endpoint.
/// Caches the result for 24 hours to avoid latency on every run.
/// Fails open (returns valid) if the network is unreachable, so air-gapped
/// or locked-down CI environments are unaffected.
/// Set GAUNTLETCI_OFFLINE=1 to skip the network check entirely (Enterprise/air-gap).
/// </summary>
public static class NetworkLicenseValidator
{
    private const string StatusEndpoint = "https://gauntletci-license-worker.patient-water-71dd.workers.dev/license/status";
    private static readonly TimeSpan CacheTtl       = TimeSpan.FromHours(24);

    private static readonly string CachePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".gauntletci", "license-status-cache.json");

    /// <summary>
    /// Validates the token against the remote endpoint.
    /// Returns (Valid: true, Reason: null) when the subscription is active.
    /// Returns (Valid: false, Reason: string) when cancelled or revoked.
    /// Returns (Valid: true, Reason: null) when the network is unreachable (fail-open).
    /// Returns (Valid: true, Reason: null) when GAUNTLETCI_OFFLINE=1 is set.
    /// </summary>
    public static async Task<(bool Valid, string? Reason)> ValidateAsync(
        string token,
        CancellationToken ct = default)
    {
        if (IsOfflineMode())
            return (true, null);

        // Check cache first -- skip network if cache is fresh and token matches.
        var cached = TryReadCache(token);
        if (cached.HasValue)
            return cached.Value;

        try
        {
            using var http = HttpClientFactory.GetGenericClient();
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            using var response = await http.GetAsync(StatusEndpoint, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            using var doc  = JsonDocument.Parse(body);
            var root       = doc.RootElement;
            var valid      = root.GetProperty("valid").GetBoolean();
            var reason     = root.TryGetProperty("reason", out var r) ? r.GetString() : null;

            WriteCache(token, valid, reason);
            return (valid, reason);
        }
        catch (OperationCanceledException) { /* caller cancelled */ throw; }
        catch
        {
            // Network unavailable -- fail open and do not update the cache.
            return (true, null);
        }
    }

    // -------------------------------------------------------------------------

    private static bool IsOfflineMode() =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GAUNTLETCI_OFFLINE"));

    private static (bool Valid, string? Reason)? TryReadCache(string token)
    {
        try
        {
            if (!File.Exists(CachePath))
                return null;

            using var stream = File.OpenRead(CachePath);
            using var doc    = JsonDocument.Parse(stream);
            var root         = doc.RootElement;

            // Invalidate if the token has changed since the cache was written.
            var cachedHash = root.TryGetProperty("tokenHash", out var h) ? h.GetString() : null;
            if (cachedHash != TokenHash(token))
                return null;

            var cachedAt = DateTimeOffset.FromUnixTimeSeconds(
                root.GetProperty("cachedAt").GetInt64());
            if (DateTimeOffset.UtcNow - cachedAt > CacheTtl)
                return null;

            var valid  = root.GetProperty("valid").GetBoolean();
            var reason = root.TryGetProperty("reason", out var rv) ? rv.GetString() : null;
            return (valid, reason);
        }
        catch
        {
            return null;
        }
    }

    private static void WriteCache(string token, bool valid, string? reason)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);
            var obj = new
            {
                tokenHash = TokenHash(token),
                valid,
                reason,
                cachedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            };
            File.WriteAllText(CachePath, JsonSerializer.Serialize(obj));
        }
        catch { /* best effort */ }
    }

    private static string TokenHash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes)[..16];
    }
}

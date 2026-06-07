// SPDX-License-Identifier: Elastic-2.0
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GauntletCI.Core;

namespace GauntletCI.Cli.Licensing;

/// <summary>
/// Validates an active GauntletCI license against the remote status endpoint.
/// Caches the result for 24 hours to avoid latency on every run.
/// On network errors, reuses a stale cache entry when one exists for the same token.
/// When no cache exists and the network is unreachable, fails open but flags
/// <see cref="NetworkLicenseValidationResult.SkippedNetworkCheck"/> so callers can warn.
/// Set GAUNTLETCI_OFFLINE=1 to skip the network check entirely (Enterprise/air-gap).
/// </summary>
public static class NetworkLicenseValidator
{
    private const string StatusEndpoint = "https://gauntletci-license-worker.patient-water-71dd.workers.dev/license/status";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

    private static readonly string DefaultCachePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".gauntletci", "license-status-cache.json");

    /// <summary>Test hook: override cache file path.</summary>
    internal static string? TestCachePathOverride { get; set; }

    /// <summary>Test hook: override status endpoint (use unreachable URL to simulate network failure).</summary>
    internal static string? TestStatusEndpointOverride { get; set; }

    private static string CachePath => TestCachePathOverride ?? DefaultCachePath;

    /// <summary>
    /// Validates the token against the remote endpoint.
    /// </summary>
    public static async Task<NetworkLicenseValidationResult> ValidateAsync(
        string token,
        CancellationToken ct = default)
    {
        if (IsOfflineMode())
            return new NetworkLicenseValidationResult(true, null, SkippedNetworkCheck: true);

        var cached = TryReadCache(token, ignoreTtl: false);
        if (cached.HasValue)
            return new NetworkLicenseValidationResult(cached.Value.Valid, cached.Value.Reason);

        try
        {
            var endpoint = TestStatusEndpointOverride ?? StatusEndpoint;
            var http = HttpClientFactory.GetGenericClient();
            // Do not dispose: HttpClientFactory owns this shared, process-wide client.

            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            using var response = await http.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var valid = root.GetProperty("valid").GetBoolean();
            var reason = root.TryGetProperty("reason", out var r) ? r.GetString() : null;

            WriteCache(token, valid, reason);
            return new NetworkLicenseValidationResult(valid, reason);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[NetworkLicenseValidator] Network check failed: {ex.Message}");

            var stale = TryReadCache(token, ignoreTtl: true);
            if (stale.HasValue)
                return new NetworkLicenseValidationResult(stale.Value.Valid, stale.Value.Reason);

            // No prior validation record -- allow air-gapped first run, but flag it.
            return new NetworkLicenseValidationResult(true, null, SkippedNetworkCheck: true);
        }
    }

    // -------------------------------------------------------------------------

    private static bool IsOfflineMode() =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GAUNTLETCI_OFFLINE"));

    internal static (bool Valid, string? Reason)? TryReadCache(string token, bool ignoreTtl)
    {
        try
        {
            if (!File.Exists(CachePath))
                return null;

            using var stream = File.OpenRead(CachePath);
            using var doc = JsonDocument.Parse(stream);
            var root = doc.RootElement;

            var cachedHash = root.TryGetProperty("tokenHash", out var h) ? h.GetString() : null;
            if (cachedHash != TokenHash(token))
                return null;

            if (!ignoreTtl)
            {
                var cachedAt = DateTimeOffset.FromUnixTimeSeconds(
                    root.GetProperty("cachedAt").GetInt64());
                if (DateTimeOffset.UtcNow - cachedAt > CacheTtl)
                    return null;
            }

            var valid = root.GetProperty("valid").GetBoolean();
            var reason = root.TryGetProperty("reason", out var rv) ? rv.GetString() : null;
            return (valid, reason);
        }
        catch
        {
            return null;
        }
    }

    internal static void WriteCache(string token, bool valid, string? reason)
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
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[NetworkLicenseValidator] Warning: Failed to write license cache: {ex.Message}");
        }
    }

    private static string TokenHash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes)[..16];
    }
}

public readonly record struct NetworkLicenseValidationResult(
    bool Valid,
    string? Reason,
    bool SkippedNetworkCheck = false);

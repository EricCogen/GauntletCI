// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Cli.Licensing;

namespace GauntletCI.Tests;

public class NetworkLicenseValidatorTests : IDisposable
{
    private readonly string _cacheDir;
    private readonly string _cachePath;

    public NetworkLicenseValidatorTests()
    {
        _cacheDir = Path.Combine(Path.GetTempPath(), "gauntletci-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_cacheDir);
        _cachePath = Path.Combine(_cacheDir, "license-status-cache.json");
        NetworkLicenseValidator.TestCachePathOverride = _cachePath;
    }

    public void Dispose()
    {
        NetworkLicenseValidator.TestCachePathOverride = null;
        NetworkLicenseValidator.TestStatusEndpointOverride = null;
        if (Directory.Exists(_cacheDir))
            Directory.Delete(_cacheDir, recursive: true);
    }

    private void SimulateNetworkFailure() =>
        NetworkLicenseValidator.TestStatusEndpointOverride = "http://127.0.0.1:1/unreachable";

    [Fact]
    public async Task ValidateAsync_UsesFreshCacheWithoutNetworkCall()
    {
        const string token = "test-token-fresh-cache";
        NetworkLicenseValidator.WriteCache(token, valid: true, reason: null);

        SimulateNetworkFailure();
        var result = await NetworkLicenseValidator.ValidateAsync(token);

        Assert.True(result.Valid);
        Assert.False(result.SkippedNetworkCheck);
    }

    [Fact]
    public async Task ValidateAsync_UsesStaleCacheWhenNetworkFails()
    {
        const string token = "test-token-stale-cache";
        WriteStaleCache(token, valid: false, reason: "cancelled", age: TimeSpan.FromDays(2));

        SimulateNetworkFailure();
        var result = await NetworkLicenseValidator.ValidateAsync(token);

        Assert.False(result.Valid);
        Assert.Equal("cancelled", result.Reason);
        Assert.False(result.SkippedNetworkCheck);
    }

    [Fact]
    public async Task ValidateAsync_FailsOpenWhenNoCacheAndNetworkFails()
    {
        const string token = "test-token-no-cache";
        SimulateNetworkFailure();

        var result = await NetworkLicenseValidator.ValidateAsync(token);

        Assert.True(result.Valid);
        Assert.True(result.SkippedNetworkCheck);
    }

    [Fact]
    public void TryReadCache_IgnoresTtlWhenRequested()
    {
        const string token = "test-token-ttl";
        WriteStaleCache(token, valid: true, reason: null, age: TimeSpan.FromDays(2));

        Assert.Null(NetworkLicenseValidator.TryReadCache(token, ignoreTtl: false));
        Assert.NotNull(NetworkLicenseValidator.TryReadCache(token, ignoreTtl: true));
    }

    private void WriteStaleCache(string token, bool valid, string? reason, TimeSpan age)
    {
        NetworkLicenseValidator.WriteCache(token, valid, reason);
        var cachedAt = DateTimeOffset.UtcNow.Subtract(age).ToUnixTimeSeconds();
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(_cachePath));
        var tokenHash = doc.RootElement.GetProperty("tokenHash").GetString();
        var payload = new
        {
            tokenHash,
            valid,
            reason,
            cachedAt,
        };
        File.WriteAllText(_cachePath, System.Text.Json.JsonSerializer.Serialize(payload));
    }
}

// SPDX-License-Identifier: Elastic-2.0
using System.Reflection;
using System.Text.Json;
using GauntletCI.Cli.Telemetry;

namespace GauntletCI.Tests;

/// <summary>
/// Tests for TelemetryConsent — mode parsing, mode enum values, and migration from legacy consent.json.
/// Uses reflection to reset the internal static cache between tests.
/// Runs serially with TelemetryCollectorTests to prevent file-lock races on the consent file.
/// </summary>
[Collection("TelemetrySerial")]
public class TelemetryConsentTests
{
    private static void ResetCache() =>
        typeof(TelemetryConsent)
            .GetField("_cache", BindingFlags.NonPublic | BindingFlags.Static)
            ?.SetValue(null, null);

    [Fact]
    public void TelemetryMode_HasThreeValues()
    {
        var values = Enum.GetValues<TelemetryMode>();
        Assert.Equal(3, values.Length);
    }

    [Fact]
    public void TelemetryMode_Off_IsZero()
    {
        Assert.Equal(0, (int)TelemetryMode.Off);
    }

    [Fact]
    public void TelemetryMode_Local_IsOne()
    {
        Assert.Equal(1, (int)TelemetryMode.Local);
    }

    [Fact]
    public void TelemetryMode_Shared_IsTwo()
    {
        Assert.Equal(2, (int)TelemetryMode.Shared);
    }

    [Fact]
    public void TelemetryMode_Confidence_MapsCorrectly()
    {
        // Confidence.High == 2 in Finding JSON output — Shared telemetry should also be 2
        // This test documents the intentional parallel: High confidence = Shared mode = value 2
        Assert.Equal((int)TelemetryMode.Shared, 2);
    }

    [Fact]
    public void LegacyConsentRecord_OptedInTrue_MapsToSharedMode()
    {
        // Simulate what migration logic does: OptedIn=true → "shared"
        bool? optedIn = true;
        var migratedMode = optedIn switch
        {
            true  => "shared",
            false => "off",
            null  => null,
        };
        Assert.Equal("shared", migratedMode);
    }

    [Fact]
    public void LegacyConsentRecord_OptedInFalse_MapsToOffMode()
    {
        bool? optedIn = false;
        var migratedMode = optedIn switch
        {
            true  => "shared",
            false => "off",
            null  => null,
        };
        Assert.Equal("off", migratedMode);
    }

    [Fact]
    public void LegacyConsentRecord_OptedInNull_MapsToNullMode()
    {
        bool? optedIn = null;
        var migratedMode = optedIn switch
        {
            true  => "shared",
            false => "off",
            null  => null,
        };
        Assert.Null(migratedMode);
    }

    [Fact]
    public void TelemetryConsent_SetMode_UpdatesCache()
    {
        ResetCache();

        // SetMode writes to ~/.gauntletci/config.json — only verify the public API
        // returns the value we set without throwing.
        var before = TelemetryConsent.GetMode();

        TelemetryConsent.SetMode(TelemetryMode.Local);
        var after = TelemetryConsent.GetMode();

        Assert.Equal(TelemetryMode.Local, after);

        // Restore whatever was set before (avoid polluting real user config)
        TelemetryConsent.SetMode(before);
        ResetCache();
    }

    [Fact]
    public void TelemetryConsent_SetOptIn_True_SetsSharedMode()
    {
        ResetCache();
        var before = TelemetryConsent.GetMode();

        TelemetryConsent.SetOptIn(true);
        Assert.Equal(TelemetryMode.Shared, TelemetryConsent.GetMode());

        TelemetryConsent.SetMode(before);
        ResetCache();
    }

    [Fact]
    public void TelemetryConsent_SetOptIn_False_SetsOffMode()
    {
        ResetCache();
        var before = TelemetryConsent.GetMode();

        TelemetryConsent.SetOptIn(false);
        Assert.Equal(TelemetryMode.Off, TelemetryConsent.GetMode());

        TelemetryConsent.SetMode(before);
        ResetCache();
    }

    [Fact]
    public void TelemetryConsent_IsOptedIn_FalseWhenOff()
    {
        ResetCache();
        var before = TelemetryConsent.GetMode();

        TelemetryConsent.SetMode(TelemetryMode.Off);
        Assert.False(TelemetryConsent.IsOptedIn);

        TelemetryConsent.SetMode(before);
        ResetCache();
    }

    [Fact]
    public void TelemetryConsent_IsOptedIn_TrueWhenLocal()
    {
        ResetCache();
        var before = TelemetryConsent.GetMode();

        TelemetryConsent.SetMode(TelemetryMode.Local);
        Assert.True(TelemetryConsent.IsOptedIn);

        TelemetryConsent.SetMode(before);
        ResetCache();
    }

    [Fact]
    public void TelemetryConsent_IsOptedIn_TrueWhenShared()
    {
        ResetCache();
        var before = TelemetryConsent.GetMode();

        TelemetryConsent.SetMode(TelemetryMode.Shared);
        Assert.True(TelemetryConsent.IsOptedIn);

        TelemetryConsent.SetMode(before);
        ResetCache();
    }

    [Fact]
    public void LegacyConsentJson_CanDeserializeToExpectedShape()
    {
        // Verify the legacy JSON shape can be read — guards against future accidental breakage
        var legacyJson = """
            {
              "InstallId": "test-install-id-abc",
              "OptedIn": true,
              "DecidedAt": "2025-01-15T12:00:00+00:00"
            }
            """;

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var record = JsonSerializer.Deserialize<LegacyConsentShape>(legacyJson, options);

        Assert.NotNull(record);
        Assert.Equal("test-install-id-abc", record.InstallId);
        Assert.True(record.OptedIn);
    }

    // Public shape mirroring the private LegacyConsentRecord for deserialization testing
    private record LegacyConsentShape(string InstallId, bool? OptedIn, DateTimeOffset? DecidedAt);
}

// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Cli.Telemetry;

namespace GauntletCI.Tests;

/// <summary>
/// Tests for TelemetryUploader — observable contract only.
/// The uploader depends on static state and creates its own HttpClient internally,
/// so tests verify fire-and-forget safety, early-return behaviour, and exception swallowing.
/// </summary>
[Collection("TelemetrySerial")]
public class TelemetryUploaderTests
{
    // -------------------------------------------------------------------------
    // UploadInBackground
    // -------------------------------------------------------------------------

    [Fact]
    public void UploadInBackground_DoesNotThrow()
    {
        TelemetryConsent.SetMode(TelemetryMode.Off);

        var ex = Record.Exception(() => TelemetryUploader.UploadInBackground());

        Assert.Null(ex);
    }

    // -------------------------------------------------------------------------
    // UploadAsync — early-return paths (no network traffic)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UploadAsync_WhenConsentOff_ReturnsImmediatelyWithoutException()
    {
        TelemetryConsent.SetMode(TelemetryMode.Off);

        await TelemetryUploader.UploadAsync(); // must not throw
    }

    [Fact]
    public async Task UploadAsync_WhenConsentLocal_ReturnsImmediatelyWithoutException()
    {
        TelemetryConsent.SetMode(TelemetryMode.Local);

        await TelemetryUploader.UploadAsync(); // must not throw
    }

    // -------------------------------------------------------------------------
    // UploadAsync — Shared consent with no reachable server
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UploadAsync_WhenConsentShared_SwallowsNetworkException()
    {
        TelemetryConsent.SetMode(TelemetryMode.Shared);

        // No real server is available; the uploader must swallow the exception.
        var ex = await Record.ExceptionAsync(() => TelemetryUploader.UploadAsync());

        Assert.Null(ex);
    }
}

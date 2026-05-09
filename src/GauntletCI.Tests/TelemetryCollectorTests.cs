// SPDX-License-Identifier: Elastic-2.0
using System.Reflection;
using GauntletCI.Cli.Telemetry;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules;

namespace GauntletCI.Tests;

/// <summary>
/// Tests for TelemetryCollector. Because CollectAsync swallows all exceptions and relies
/// on static dependencies (TelemetryConsent, TelemetryStore, TelemetryDb), most tests
/// verify the observable contract: no exceptions are thrown.
/// ExtractExt is tested via reflection since it is a private static helper.
/// Runs serially with TelemetryConsentTests to prevent file-lock races on the consent file.
/// NOTE: Full integration testing requires mockable static deps.
/// </summary>
[Collection("TelemetrySerial")]
public class TelemetryCollectorTests
{
    private static readonly MethodInfo? ExtractExtMethod =
        typeof(TelemetryCollector).GetMethod("ExtractExt",
            BindingFlags.NonPublic | BindingFlags.Static);

    private static string? InvokeExtractExt(string? evidence) =>
        (string?)ExtractExtMethod?.Invoke(null, new object?[] { evidence });

    private static EvaluationResult EmptyResult() => new()
    {
        Findings       = [],
        RulesEvaluated = 0,
        RuleMetrics    = [],
    };

    private static DiffContext EmptyDiff() => new()
    {
        Files   = [],
        RawDiff = "",
    };

    [Fact]
    public async Task CollectAsync_WhenTelemetryOff_DoesNotThrow()
    {
        // CollectAsync swallows all exceptions: verify it completes cleanly.
        // Avoid SetMode to prevent file-lock contention with TelemetryConsentTests.
        await TelemetryCollector.CollectAsync(EmptyResult(), EmptyDiff(), Path.GetTempPath());
        // Reaching here without exception = pass
    }

    [Fact]
    public async Task CollectAsync_WithEmptyDiff_DoesNotThrow()
    {
        // CollectAsync swallows all exceptions: verify it completes cleanly
        await TelemetryCollector.CollectAsync(EmptyResult(), EmptyDiff(), Path.GetTempPath(), quiet: true);
    }

    [Fact]
    public async Task CollectAsync_WithFindings_DoesNotThrow()
    {
        var result = new EvaluationResult
        {
            Findings =
            [
                new Finding
                {
                    RuleId          = "GCI0001",
                    RuleName        = "Test Rule",
                    Summary         = "Test summary",
                    Evidence        = "src/Foo.cs: line 1",
                    WhyItMatters    = "test",
                    SuggestedAction = "test",
                    Confidence      = Confidence.High,
                }
            ],
            RulesEvaluated = 1,
            RuleMetrics    = [],
        };

        await TelemetryCollector.CollectAsync(result, EmptyDiff(), Path.GetTempPath());
        // Reaching here without exception = pass
    }

    [Fact]
    public void ExtractExt_CsExtension_ReturnsDotCs()
    {
        var result = InvokeExtractExt("src/Foo.cs: line 42");
        Assert.Equal(".cs", result);
    }

    [Fact]
    public void ExtractExt_TsExtension_ReturnsDotTs()
    {
        var result = InvokeExtractExt("src/components/Button.ts: line 10");
        Assert.Equal(".ts", result);
    }

    [Fact]
    public void ExtractExt_EmptyString_ReturnsNull()
    {
        var result = InvokeExtractExt("");
        Assert.Null(result);
    }

    [Fact]
    public void ExtractExt_NullInput_ReturnsNull()
    {
        var result = InvokeExtractExt(null);
        Assert.Null(result);
    }

    [Fact]
    public void ExtractExt_NoFilePathInEvidence_ReturnsNull()
    {
        var result = InvokeExtractExt("Guard clause was removed from the authentication flow");
        Assert.Null(result);
    }

    [Fact]
    public void ExtractExt_ExtensionLongerThan6Chars_ReturnsNull()
    {
        // Extensions > 6 chars are filtered out
        var result = InvokeExtractExt("src/archive.toolong: line 1");
        Assert.Null(result);
    }

    [Fact]
    public void ExtractExt_ReturnsLowercase()
    {
        var result = InvokeExtractExt("src/Foo.CS: line 1");
        Assert.Equal(".cs", result);
    }
}

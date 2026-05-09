// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Model;

namespace GauntletCI.Tests;

public class SensitivityFilterTests
{
    // Advisory always passes, Info/None always pass (gated by minSeverity elsewhere)

    [Theory]
    [InlineData(RuleSeverity.Advisory, Confidence.Low)]
    [InlineData(RuleSeverity.Advisory, Confidence.High)]
    [InlineData(RuleSeverity.Info,     Confidence.Low)]
    [InlineData(RuleSeverity.None,     Confidence.Low)]
    public void Passes_AdvisoryInfoNone_AlwaysTrue(RuleSeverity severity, Confidence confidence)
    {
        Assert.True(SensitivityFilter.Passes(severity, confidence, SensitivityThreshold.Strict));
        Assert.True(SensitivityFilter.Passes(severity, confidence, SensitivityThreshold.Balanced));
        Assert.True(SensitivityFilter.Passes(severity, confidence, SensitivityThreshold.Permissive));
    }

    // ── Strict ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(RuleSeverity.Block, Confidence.High,   true)]
    [InlineData(RuleSeverity.Block, Confidence.Medium, true)]
    [InlineData(RuleSeverity.Block, Confidence.Low,    false)]
    [InlineData(RuleSeverity.Warn,  Confidence.High,   false)]
    [InlineData(RuleSeverity.Warn,  Confidence.Medium, false)]
    [InlineData(RuleSeverity.Warn,  Confidence.Low,    false)]
    public void Passes_Strict_Matrix(RuleSeverity severity, Confidence confidence, bool expected)
    {
        Assert.Equal(expected, SensitivityFilter.Passes(severity, confidence, SensitivityThreshold.Strict));
    }

    // ── Balanced (default) ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(RuleSeverity.Block, Confidence.High,   true)]
    [InlineData(RuleSeverity.Block, Confidence.Medium, true)]
    [InlineData(RuleSeverity.Block, Confidence.Low,    true)]
    [InlineData(RuleSeverity.Warn,  Confidence.High,   true)]
    [InlineData(RuleSeverity.Warn,  Confidence.Medium, true)]
    [InlineData(RuleSeverity.Warn,  Confidence.Low,    false)]
    public void Passes_Balanced_Matrix(RuleSeverity severity, Confidence confidence, bool expected)
    {
        Assert.Equal(expected, SensitivityFilter.Passes(severity, confidence, SensitivityThreshold.Balanced));
    }

    // ── Permissive ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(RuleSeverity.Block, Confidence.High)]
    [InlineData(RuleSeverity.Block, Confidence.Low)]
    [InlineData(RuleSeverity.Warn,  Confidence.High)]
    [InlineData(RuleSeverity.Warn,  Confidence.Low)]
    public void Passes_Permissive_AllBlockAndWarn(RuleSeverity severity, Confidence confidence)
    {
        Assert.True(SensitivityFilter.Passes(severity, confidence, SensitivityThreshold.Permissive));
    }
}

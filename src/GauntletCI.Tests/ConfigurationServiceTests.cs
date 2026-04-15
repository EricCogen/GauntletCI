// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Configuration;
using GauntletCI.Core.Model;

namespace GauntletCI.Tests;

public class ConfigurationServiceTests
{
    // ── Default severity resolution ────────────────────────────────────────────

    [Theory]
    [InlineData("GCI0001", RuleSeverity.Block)]
    [InlineData("GCI0003", RuleSeverity.Block)]
    [InlineData("GCI0012", RuleSeverity.Block)]
    [InlineData("GCI0039", RuleSeverity.Block)]
    [InlineData("GCI0006", RuleSeverity.Warn)]
    [InlineData("GCI0035", RuleSeverity.Warn)]
    [InlineData("GCI0041", RuleSeverity.Warn)]
    [InlineData("GCI0099", RuleSeverity.Info)]   // unknown → Info
    [InlineData("GCI_SYN_AGG", RuleSeverity.Info)] // synthetic → Info
    public void GetEffectiveSeverity_NoConfig_ReturnsDefault(string ruleId, RuleSeverity expected)
    {
        var svc = new ConfigurationService(new GauntletConfig());
        Assert.Equal(expected, svc.GetEffectiveSeverity(ruleId));
    }

    // ── .gauntletci.json overrides ─────────────────────────────────────────────

    [Theory]
    [InlineData("Block", RuleSeverity.Block)]
    [InlineData("Warn",  RuleSeverity.Warn)]
    [InlineData("Info",  RuleSeverity.Info)]
    [InlineData("None",  RuleSeverity.None)]
    [InlineData("block", RuleSeverity.Block)]   // case-insensitive
    [InlineData("WARN",  RuleSeverity.Warn)]
    public void GetEffectiveSeverity_JsonOverride_WinsOverDefault(string severityValue, RuleSeverity expected)
    {
        var config = new GauntletConfig
        {
            Rules = new() { ["GCI0001"] = new RuleConfig { Severity = severityValue } }
        };
        var svc = new ConfigurationService(config);
        Assert.Equal(expected, svc.GetEffectiveSeverity("GCI0001"));
    }

    [Theory]
    [InlineData("High",   RuleSeverity.Block)]  // legacy Confidence values accepted
    [InlineData("Medium", RuleSeverity.Warn)]
    [InlineData("Low",    RuleSeverity.Info)]
    public void GetEffectiveSeverity_LegacySeverityStrings_MapCorrectly(string legacyValue, RuleSeverity expected)
    {
        var config = new GauntletConfig
        {
            Rules = new() { ["GCI0001"] = new RuleConfig { Severity = legacyValue } }
        };
        var svc = new ConfigurationService(config);
        Assert.Equal(expected, svc.GetEffectiveSeverity("GCI0001"));
    }

    // ── .editorconfig parsing ──────────────────────────────────────────────────

    [Fact]
    public void GetEffectiveSeverity_EditorConfig_ParsesAllLevels()
    {
        var dir = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, ".editorconfig"), """
                [*.cs]
                dotnet_diagnostic.GCI0006.severity = error
                dotnet_diagnostic.GCI0022.severity = warning
                dotnet_diagnostic.GCI0029.severity = suggestion
                dotnet_diagnostic.GCI0035.severity = none
                """);

            var svc = new ConfigurationService(new GauntletConfig(), dir);
            Assert.Equal(RuleSeverity.Block, svc.GetEffectiveSeverity("GCI0006"));
            Assert.Equal(RuleSeverity.Warn,  svc.GetEffectiveSeverity("GCI0022"));
            Assert.Equal(RuleSeverity.Info,  svc.GetEffectiveSeverity("GCI0029"));
            Assert.Equal(RuleSeverity.None,  svc.GetEffectiveSeverity("GCI0035"));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void GetEffectiveSeverity_MissingEditorConfig_FallsBackToDefault()
    {
        var dir = CreateTempDir();
        try
        {
            // No .editorconfig created
            var svc = new ConfigurationService(new GauntletConfig(), dir);
            Assert.Equal(RuleSeverity.Block, svc.GetEffectiveSeverity("GCI0001")); // built-in default
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ── Priority chain ─────────────────────────────────────────────────────────

    [Fact]
    public void GetEffectiveSeverity_JsonWinsOverEditorConfig()
    {
        var dir = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, ".editorconfig"), """
                [*.cs]
                dotnet_diagnostic.GCI0001.severity = none
                """);

            var config = new GauntletConfig
            {
                Rules = new() { ["GCI0001"] = new RuleConfig { Severity = "Warn" } }
            };
            var svc = new ConfigurationService(config, dir);
            // JSON override (Warn) wins over .editorconfig (none)
            Assert.Equal(RuleSeverity.Warn, svc.GetEffectiveSeverity("GCI0001"));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void GetEffectiveSeverity_EditorConfigWinsOverDefault()
    {
        var dir = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, ".editorconfig"), """
                [*.cs]
                dotnet_diagnostic.GCI0001.severity = suggestion
                """);

            var svc = new ConfigurationService(new GauntletConfig(), dir);
            // .editorconfig (Info) wins over built-in default (Block)
            Assert.Equal(RuleSeverity.Info, svc.GetEffectiveSeverity("GCI0001"));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ── Caching ────────────────────────────────────────────────────────────────

    [Fact]
    public void GetEffectiveSeverity_CalledTwice_ReturnsSameResult()
    {
        var svc = new ConfigurationService(new GauntletConfig());
        var first  = svc.GetEffectiveSeverity("GCI0001");
        var second = svc.GetEffectiveSeverity("GCI0001");
        Assert.Equal(first, second);
    }

    // ── EvaluationResult.ShouldBlock ──────────────────────────────────────────

    [Fact]
    public void ShouldBlock_ExitOnBlock_TrueWhenBlockFindingExists()
    {
        var result = MakeResult(RuleSeverity.Block);
        Assert.True(result.ShouldBlock("Block"));
    }

    [Fact]
    public void ShouldBlock_ExitOnBlock_FalseWhenOnlyWarnFindings()
    {
        var result = MakeResult(RuleSeverity.Warn);
        Assert.False(result.ShouldBlock("Block"));
    }

    [Fact]
    public void ShouldBlock_ExitOnWarn_TrueWhenWarnFindingExists()
    {
        var result = MakeResult(RuleSeverity.Warn);
        Assert.True(result.ShouldBlock("Warn"));
    }

    [Fact]
    public void ShouldBlock_ExitOnWarn_TrueWhenBlockFindingExists()
    {
        var result = MakeResult(RuleSeverity.Block);
        Assert.True(result.ShouldBlock("Warn"));
    }

    [Fact]
    public void ShouldBlock_ExitOnWarn_FalseWhenOnlyInfoFindings()
    {
        var result = MakeResult(RuleSeverity.Info);
        Assert.False(result.ShouldBlock("Warn"));
    }

    [Fact]
    public void ShouldBlock_NoFindings_AlwaysFalse()
    {
        var result = new GauntletCI.Core.Rules.EvaluationResult { Findings = [] };
        Assert.False(result.ShouldBlock("Block"));
        Assert.False(result.ShouldBlock("Warn"));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"gci_svc_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static GauntletCI.Core.Rules.EvaluationResult MakeResult(RuleSeverity severity) =>
        new()
        {
            Findings =
            [
                new Finding
                {
                    RuleId = "GCI0001", RuleName = "Test", Summary = "s",
                    Evidence = "e", WhyItMatters = "w", SuggestedAction = "a",
                    Severity = severity,
                }
            ]
        };
}

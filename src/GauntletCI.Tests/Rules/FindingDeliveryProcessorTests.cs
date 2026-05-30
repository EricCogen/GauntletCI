// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Configuration;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules.Delivery;

namespace GauntletCI.Tests.Rules;

public sealed class FindingDeliveryProcessorTests
{
    private static Finding MakeFinding(
        string ruleId,
        string? filePath = "src/Foo.cs",
        int? line = 1,
        RuleSeverity severity = RuleSeverity.Warn,
        Confidence confidence = Confidence.Medium) => new()
        {
            RuleId = ruleId,
            RuleName = ruleId,
            Summary = $"Finding for {ruleId}",
            Evidence = $"{filePath}:{line}",
            WhyItMatters = "test",
            SuggestedAction = "fix",
            FilePath = filePath,
            Line = line,
            Severity = severity,
            Confidence = confidence,
        };

    [Fact]
    public void Apply_WhenDisabled_PassesThroughUnchanged()
    {
        var findings = new[] { MakeFinding("GCI0038", line: 10) };
        var config = new FindingDeliveryConfig { Enabled = false };

        var result = FindingDeliveryProcessor.Apply(findings, config);

        Assert.Single(result.Findings);
        Assert.Equal(1, result.Summary.OutputCount);
        Assert.Equal(0, result.Summary.DroppedByPerRuleCap);
    }

    [Fact]
    public void Apply_PerRulePerFileCap_KeepsHighestScoredFindings()
    {
        var findings = Enumerable.Range(1, 6)
            .Select(i => MakeFinding("GCI0038", line: i, confidence: i == 6 ? Confidence.High : Confidence.Low))
            .ToList();

        var config = new FindingDeliveryConfig
        {
            PerRulePerFileCap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["GCI0038"] = 3 },
        };

        var result = FindingDeliveryProcessor.Apply(findings, config);

        Assert.Equal(3, result.Findings.Count);
        Assert.Equal(3, result.Summary.DroppedByPerRuleCap);
        Assert.Contains(result.Findings, f => f.Line == 6 && f.Confidence == Confidence.High);
    }

    [Fact]
    public void Apply_GlobalCap_DropsLowestRankedAfterPerRuleCap()
    {
        var findings = new List<Finding>();
        for (var i = 0; i < 30; i++)
            findings.Add(MakeFinding("GCI0006", filePath: $"src/File{i}.cs", line: 1, severity: RuleSeverity.Info));

        var config = new FindingDeliveryConfig
        {
            GlobalMaxFindings = 10,
            DefaultPerRulePerFileCap = 10,
        };

        var result = FindingDeliveryProcessor.Apply(findings, config);

        Assert.Equal(10, result.Findings.Count);
        Assert.True(result.Summary.DroppedByGlobalCap > 0);
    }

    [Fact]
    public void Apply_DemotesFileLevelGci0001WhenLineAnchoredFindingsExist()
    {
        var findings = new[]
        {
            MakeFinding("GCI0001", line: null, severity: RuleSeverity.Block),
            MakeFinding("GCI0038", line: 42),
        };

        var result = FindingDeliveryProcessor.Apply(findings, new FindingDeliveryConfig());

        Assert.Single(result.Findings);
        Assert.Equal("GCI0038", result.Findings[0].RuleId);
        Assert.Equal(1, result.Summary.DroppedByFileLevelDemotion);
    }

    [Fact]
    public void Apply_KeepsFileLevelWhenNoLineAnchoredFindingsExist()
    {
        var findings = new[]
        {
            MakeFinding("GCI0001", line: null, severity: RuleSeverity.Block),
        };

        var result = FindingDeliveryProcessor.Apply(findings, new FindingDeliveryConfig());

        Assert.Single(result.Findings);
        Assert.Equal(0, result.Summary.DroppedByFileLevelDemotion);
    }

    [Fact]
    public void Apply_RanksBlockLineAnchoredHighConfidenceFirst()
    {
        var findings = new[]
        {
            MakeFinding("GCI0043", line: 1, severity: RuleSeverity.Info, confidence: Confidence.Low),
            MakeFinding("GCI0007", line: 10, severity: RuleSeverity.Block, confidence: Confidence.High),
            MakeFinding("GCI0038", line: 5, severity: RuleSeverity.Warn, confidence: Confidence.Medium),
        };

        var config = new FindingDeliveryConfig { GlobalMaxFindings = 25 };
        var result = FindingDeliveryProcessor.Apply(findings, config);

        Assert.Equal("GCI0007", result.Findings[0].RuleId);
    }
}

public sealed class FindingCoordinationEngineTests
{
    private static Finding MakeFinding(string ruleId, Confidence confidence = Confidence.Low) => new()
    {
        RuleId = ruleId,
        RuleName = ruleId,
        Summary = ruleId,
        Evidence = "evidence",
        WhyItMatters = "why",
        SuggestedAction = "fix",
        Confidence = confidence,
    };

    [Fact]
    public void Apply_Gci0016Present_BoostsGci0039AndGci0044()
    {
        var findings = new List<Finding>
        {
            MakeFinding("GCI0016", Confidence.High),
            MakeFinding("GCI0039", Confidence.Low),
            MakeFinding("GCI0044", Confidence.Low),
        };

        var boosts = FindingCoordinationEngine.Apply(findings);

        Assert.Equal(2, boosts);
        Assert.All(findings.Where(f => f.RuleId is "GCI0039" or "GCI0044"), f => Assert.Equal(Confidence.Medium, f.Confidence));
    }

    [Fact]
    public void Apply_Gci0032AndGci0003_BoostsBoth()
    {
        var findings = new List<Finding>
        {
            MakeFinding("GCI0032", Confidence.Low),
            MakeFinding("GCI0003", Confidence.Low),
        };

        var boosts = FindingCoordinationEngine.Apply(findings);

        Assert.Equal(2, boosts);
        Assert.Equal(Confidence.High, findings.Single(f => f.RuleId == "GCI0003").Confidence);
        Assert.Equal(Confidence.Medium, findings.Single(f => f.RuleId == "GCI0032").Confidence);
    }

    [Fact]
    public void Apply_Gci0024AndGci0015_BoostsBoth()
    {
        var findings = new List<Finding>
        {
            MakeFinding("GCI0024", Confidence.Low),
            MakeFinding("GCI0015", Confidence.Low),
        };

        var boosts = FindingCoordinationEngine.Apply(findings);

        Assert.Equal(2, boosts);
        Assert.All(findings, f => Assert.Equal(Confidence.Medium, f.Confidence));
    }

    [Fact]
    public void Apply_NoCoordinationPattern_LeavesConfidenceUnchanged()
    {
        var findings = new List<Finding> { MakeFinding("GCI0038", Confidence.Low) };

        var boosts = FindingCoordinationEngine.Apply(findings);

        Assert.Equal(0, boosts);
        Assert.Equal(Confidence.Low, findings[0].Confidence);
    }
}

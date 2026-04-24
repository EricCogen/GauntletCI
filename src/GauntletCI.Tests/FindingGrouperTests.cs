// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules;

namespace GauntletCI.Tests;

public class FindingGrouperTests
{
    private static Finding MakeFinding(
        string ruleId    = "GCI0001",
        string? filePath = "src/Foo.cs",
        int? line        = 10,
        string evidence  = "Line 10: var x = 1;",
        string summary   = "Sample summary",
        Confidence conf  = Confidence.Medium,
        RuleSeverity sev = RuleSeverity.Warn) => new()
    {
        RuleId          = ruleId,
        RuleName        = "Sample",
        Summary         = summary,
        Evidence        = evidence,
        WhyItMatters    = "why",
        SuggestedAction = "action",
        FilePath        = filePath,
        Line            = line,
        Confidence      = conf,
        Severity        = sev,
    };

    [Fact]
    public void Group_DistinctRules_KeepsFindingsSeparate()
    {
        var groups = FindingGrouper.Group([
            MakeFinding(ruleId: "GCI0001"),
            MakeFinding(ruleId: "GCI0002"),
        ]);

        Assert.Equal(2, groups.Count);
    }

    [Fact]
    public void Group_SameRuleSameFile_CollapsesIntoOneGroup()
    {
        var groups = FindingGrouper.Group([
            MakeFinding(line: 12, evidence: "Line 12: a"),
            MakeFinding(line: 34, evidence: "Line 34: b"),
        ]);

        var g = Assert.Single(groups);
        Assert.Equal(2, g.Count);
        Assert.Equal(new[] { 12, 34 }, g.Lines);
        Assert.Equal(12, g.PrimaryLine);
        Assert.Equal(2, g.Evidence.Count);
    }

    [Fact]
    public void Group_SameRuleDifferentFiles_KeepsSeparate()
    {
        var groups = FindingGrouper.Group([
            MakeFinding(filePath: "src/A.cs"),
            MakeFinding(filePath: "src/B.cs"),
        ]);

        Assert.Equal(2, groups.Count);
    }

    [Fact]
    public void Group_DuplicateEvidence_Deduplicated()
    {
        var groups = FindingGrouper.Group([
            MakeFinding(line: 12, evidence: "same"),
            MakeFinding(line: 12, evidence: "same"),
        ]);

        var g = Assert.Single(groups);
        Assert.Equal(2, g.Count);
        Assert.Single(g.Evidence);
        Assert.Single(g.Lines);
    }

    [Fact]
    public void Group_PreservesFirstSeenOrder()
    {
        var groups = FindingGrouper.Group([
            MakeFinding(ruleId: "GCI0010"),
            MakeFinding(ruleId: "GCI0001"),
            MakeFinding(ruleId: "GCI0010"),
        ]);

        Assert.Equal("GCI0010", groups[0].RuleId);
        Assert.Equal("GCI0001", groups[1].RuleId);
    }

    [Fact]
    public void Group_NullFilePath_GroupsTogether()
    {
        var groups = FindingGrouper.Group([
            MakeFinding(filePath: null, line: null, evidence: "a"),
            MakeFinding(filePath: null, line: null, evidence: "b"),
        ]);

        var g = Assert.Single(groups);
        Assert.Null(g.FilePath);
        Assert.Equal(2, g.Evidence.Count);
    }

    [Fact]
    public void Group_EmptyInput_ReturnsEmpty()
    {
        Assert.Empty(FindingGrouper.Group([]));
    }
}

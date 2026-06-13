// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public sealed class GCI0019Tests
{
    [Fact]
    public async Task EvaluateAsync_BinaryFileDiff_ProducesFinding()
    {
        var diff = DiffParser.Parse("""
            diff --git a/assets/logo.png b/assets/logo.png
            index abc1234..def5678 100644
            Binary files a/assets/logo.png and b/assets/logo.png differ
            """);

        var rule = new GCI0019_ConfidenceAndEvidence(new DefaultPatternProvider());
        var context = new GauntletCI.Core.Analysis.AnalysisContext
        {
            AllFiles = diff.Files,
            Diff = diff,
        };

        var findings = await rule.EvaluateAsync(context);

        var finding = Assert.Single(findings);
        Assert.Equal("GCI0019", finding.RuleId);
        Assert.Equal("assets/logo.png", finding.FilePath);
    }

    [Fact]
    public void PostProcess_LargeDiffWithNoOtherFindings_ProducesWarning()
    {
        var addedLines = string.Join("\n", Enumerable.Range(1, 210).Select(i => $"+    var x{i} = {i};"));
        var raw = $"""
            diff --git a/src/CleanService.cs b/src/CleanService.cs
            index abc..def 100644
            --- a/src/CleanService.cs
            +++ b/src/CleanService.cs
            @@ -1,1 +1,211 @@
             // service
            {addedLines}
            """;
        var diff = DiffParser.Parse(raw);
        var rule = new GCI0019_ConfidenceAndEvidence(new DefaultPatternProvider());

        var finding = rule.PostProcess(diff, Array.Empty<Finding>());

        Assert.NotNull(finding);
        Assert.Equal("GCI0019", finding!.RuleId);
        Assert.Contains("Large diff", finding.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void PostProcess_LargeDiffWithMultipleOtherFindings_ReturnsNull()
    {
        var addedLines = string.Join("\n", Enumerable.Range(1, 210).Select(i => $"+    var x{i} = {i};"));
        var raw = $"""
            diff --git a/src/CleanService.cs b/src/CleanService.cs
            index abc..def 100644
            --- a/src/CleanService.cs
            +++ b/src/CleanService.cs
            @@ -1,1 +1,211 @@
             // service
            {addedLines}
            """;
        var diff = DiffParser.Parse(raw);
        var rule = new GCI0019_ConfidenceAndEvidence(new DefaultPatternProvider());
        var prior = new List<Finding>
        {
            new() { RuleId = "GCI0003", RuleName = "A", Summary = "a", Evidence = "e", WhyItMatters = "w", SuggestedAction = "s" },
            new() { RuleId = "GCI0004", RuleName = "B", Summary = "b", Evidence = "e", WhyItMatters = "w", SuggestedAction = "s" },
        };

        Assert.Null(rule.PostProcess(diff, prior));
    }
}

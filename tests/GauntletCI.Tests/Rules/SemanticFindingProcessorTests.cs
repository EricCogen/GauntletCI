// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Configuration;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules.Delivery;
using GauntletCI.Core.Semantics;

namespace GauntletCI.Tests.Rules;

public sealed class PatchOperationAnalyzerTests
{
    [Fact]
    public void Analyze_PolarityFlipInHunk_EmitsHighRiskConditionalModified()
    {
        var diff = DiffParser.Parse("""
            diff --git a/src/Subscription.cs b/src/Subscription.cs
            index abc..def 100644
            --- a/src/Subscription.cs
            +++ b/src/Subscription.cs
            @@ -10,3 +10,3 @@
                 foreach (var endpoint in endpoints)
            -        if (!IsSubscriberConnected(endpoint))
            +        if (IsSubscriberConnected(endpoint))
                     endpoints.Remove(endpoint);
            """);

        var ops = PatchOperationAnalyzer.Analyze(diff);
        var conditional = ops.ByKind(PatchOperationKind.ConditionalModified).Single();

        Assert.True(conditional.RiskLevel >= 0.85);
        Assert.Contains("Polarity flip", conditional.Description, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("!IsSubscriberConnected(x)", "IsSubscriberConnected(x)", true)]
    [InlineData("server is { IsSubscriberConnected: false }", "server is { IsSubscriberConnected: true }", true)]
    [InlineData("count > 0", "count > 1", false)]
    public void IsPolarityFlip_DetectsExpectedPairs(string removed, string added, bool expected) =>
        Assert.Equal(expected, PatchOperationAnalyzer.IsPolarityFlip(removed, added));
}

public sealed class SemanticFindingProcessorTests
{
    [Fact]
    public void Apply_Gci0058OnPolarityLine_AddsCounterfactualNote()
    {
        var diff = DiffParser.Parse("""
            diff --git a/src/Subscription.cs b/src/Subscription.cs
            index abc..def 100644
            --- a/src/Subscription.cs
            +++ b/src/Subscription.cs
            @@ -10,3 +10,3 @@
                 foreach (var endpoint in endpoints)
            -        if (!IsSubscriberConnected(endpoint))
            +        if (IsSubscriberConnected(endpoint))
                     endpoints.Remove(endpoint);
            """);

        var addedLine = diff.Files.Single().AddedLines.Single(l => l.Content.Contains("if (", StringComparison.Ordinal));
        var findings = new List<Finding>
        {
            new()
            {
                RuleId = "GCI0058",
                RuleName = "Paired Implementation Consistency",
                Summary = "polarity mismatch",
                Evidence = "evidence",
                WhyItMatters = "why",
                SuggestedAction = "fix",
                Confidence = Confidence.Medium,
                FilePath = "src/Subscription.cs",
                Line = addedLine.LineNumber,
            },
        };

        var result = SemanticFindingProcessor.Apply(findings, diff, new SemanticsConfig());

        var enriched = Assert.Single(result.Findings);
        Assert.Equal(Confidence.High, enriched.Confidence);
        Assert.Contains("Counterfactual", enriched.CoverageNote ?? string.Empty, StringComparison.Ordinal);
        Assert.Equal(1, result.BoostsApplied);
    }
}

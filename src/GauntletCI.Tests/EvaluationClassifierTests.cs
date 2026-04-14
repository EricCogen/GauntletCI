// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Corpus.Models;
using GauntletCI.Corpus.Scoring;

namespace GauntletCI.Tests;

public sealed class EvaluationClassifierTests
{
    private readonly EvaluationClassifier _classifier = new();

    private static FixtureMetadata Fixture(string id = "fix-001", FixtureTier tier = FixtureTier.Silver) =>
        new() { FixtureId = id, Tier = tier };

    private static ExpectedFinding Label(
        string ruleId,
        bool shouldTrigger,
        LabelSource source = LabelSource.Heuristic,
        bool inconclusive = false) =>
        new()
        {
            RuleId         = ruleId,
            ShouldTrigger  = shouldTrigger,
            LabelSource    = source,
            IsInconclusive = inconclusive,
            Reason         = "test reason",
        };

    private static ActualFinding Actual(string ruleId, bool didTrigger) =>
        new() { RuleId = ruleId, DidTrigger = didTrigger };

    [Fact]
    public void Classify_FiredRuleWithShouldTriggerTrue_ReturnsTruePositive()
    {
        // Arrange
        var fixture  = Fixture();
        var expected = new[] { Label("GCI0001", shouldTrigger: true) };
        var actual   = new[] { Actual("GCI0001", didTrigger: true) };

        // Act
        var results = _classifier.Classify(fixture, expected, actual);

        // Assert
        var eval = Assert.Single(results);
        Assert.Equal(EvaluationStatus.TruePositive, eval.Status);
        Assert.Equal("GCI0001", eval.RuleId);
        Assert.Equal(fixture.FixtureId, eval.FixtureId);
        Assert.Equal(fixture.Tier, eval.Tier);
    }

    [Fact]
    public void Classify_FiredRuleWithShouldTriggerFalse_ReturnsFalsePositive()
    {
        // Arrange
        var fixture  = Fixture();
        var expected = new[] { Label("GCI0001", shouldTrigger: false) };
        var actual   = new[] { Actual("GCI0001", didTrigger: true) };

        // Act
        var results = _classifier.Classify(fixture, expected, actual);

        // Assert
        Assert.Equal(EvaluationStatus.FalsePositive, Assert.Single(results).Status);
    }

    [Fact]
    public void Classify_UnfiredRuleWithShouldTriggerTrue_ReturnsFalseNegative()
    {
        // Arrange
        var fixture  = Fixture();
        var expected = new[] { Label("GCI0001", shouldTrigger: true) };
        var actual   = new[] { Actual("GCI0001", didTrigger: false) };

        // Act
        var results = _classifier.Classify(fixture, expected, actual);

        // Assert
        Assert.Equal(EvaluationStatus.FalseNegative, Assert.Single(results).Status);
    }

    [Fact]
    public void Classify_UnfiredRuleWithShouldTriggerFalse_ReturnsTrueNegative()
    {
        // Arrange
        var fixture  = Fixture();
        var expected = new[] { Label("GCI0001", shouldTrigger: false) };
        var actual   = new[] { Actual("GCI0001", didTrigger: false) };

        // Act
        var results = _classifier.Classify(fixture, expected, actual);

        // Assert
        Assert.Equal(EvaluationStatus.TrueNegative, Assert.Single(results).Status);
    }

    [Fact]
    public void Classify_FiredRuleWithNoLabel_ReturnsUnknown()
    {
        // Arrange
        var fixture = Fixture();
        var actual  = new[] { Actual("GCI0001", didTrigger: true) };

        // Act
        var results = _classifier.Classify(fixture, [], actual);

        // Assert
        var eval = Assert.Single(results);
        Assert.Equal(EvaluationStatus.Unknown, eval.Status);
        Assert.Equal(LabelConfidence.Unknown, eval.LabelConfidence);
    }

    [Fact]
    public void Classify_InconclusiveLabelIgnored_FiresAsUnknown()
    {
        // Arrange — label is inconclusive, so it is excluded from consideration
        var fixture  = Fixture();
        var expected = new[] { Label("GCI0001", shouldTrigger: true, inconclusive: true) };
        var actual   = new[] { Actual("GCI0001", didTrigger: true) };

        // Act
        var results = _classifier.Classify(fixture, expected, actual);

        // Assert — rule fired but no valid label exists → Unknown
        var eval = Assert.Single(results);
        Assert.Equal(EvaluationStatus.Unknown, eval.Status);
        Assert.Equal(LabelConfidence.Unknown, eval.LabelConfidence);
    }

    [Fact]
    public void Classify_HumanReviewLabel_LabelConfidenceIsTrusted()
    {
        // Arrange
        var fixture  = Fixture();
        var expected = new[] { Label("GCI0001", shouldTrigger: true, source: LabelSource.HumanReview) };
        var actual   = new[] { Actual("GCI0001", didTrigger: true) };

        // Act
        var results = _classifier.Classify(fixture, expected, actual);

        // Assert
        Assert.Equal(LabelConfidence.Trusted, Assert.Single(results).LabelConfidence);
    }

    [Fact]
    public void Classify_HeuristicLabel_LabelConfidenceIsHeuristic()
    {
        // Arrange
        var fixture  = Fixture();
        var expected = new[] { Label("GCI0001", shouldTrigger: true, source: LabelSource.Heuristic) };
        var actual   = new[] { Actual("GCI0001", didTrigger: true) };

        // Act
        var results = _classifier.Classify(fixture, expected, actual);

        // Assert
        Assert.Equal(LabelConfidence.Heuristic, Assert.Single(results).LabelConfidence);
    }

    [Fact]
    public void Classify_MultipleRules_ClassifiesAllCorrectly()
    {
        // Arrange — GCI0001 fired + should → TP; GCI0002 fired + shouldn't → FP; GCI0003 not fired + should → FN
        var fixture  = Fixture();
        var expected = new[]
        {
            Label("GCI0001", shouldTrigger: true),
            Label("GCI0002", shouldTrigger: false),
            Label("GCI0003", shouldTrigger: true),
        };
        var actual = new[]
        {
            Actual("GCI0001", didTrigger: true),
            Actual("GCI0002", didTrigger: true),
            Actual("GCI0003", didTrigger: false),
        };

        // Act
        var results = _classifier.Classify(fixture, expected, actual);

        // Assert
        Assert.Equal(3, results.Count);
        Assert.Contains(results, r => r.RuleId == "GCI0001" && r.Status == EvaluationStatus.TruePositive);
        Assert.Contains(results, r => r.RuleId == "GCI0002" && r.Status == EvaluationStatus.FalsePositive);
        Assert.Contains(results, r => r.RuleId == "GCI0003" && r.Status == EvaluationStatus.FalseNegative);
    }

    [Fact]
    public void Classify_EmptyActualAndExpected_ReturnsEmptyList()
    {
        // Arrange
        var fixture = Fixture();

        // Act
        var results = _classifier.Classify(fixture, [], []);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void Classify_NothingFired_NothingExpected_ReturnsEmpty()
    {
        // Arrange — rule exists in actual but didn't fire, and there are no labels
        var fixture = Fixture();
        var actual  = new[] { Actual("GCI0001", didTrigger: false) };

        // Act
        var results = _classifier.Classify(fixture, [], actual);

        // Assert — unfired rules with no label are silently omitted
        Assert.Empty(results);
    }
}

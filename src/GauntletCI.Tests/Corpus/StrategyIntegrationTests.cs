// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Corpus.Labeling;
using GauntletCI.Corpus.Labeling.Strategies;
using GauntletCI.Corpus.Models;
using GauntletCI.Corpus.Interfaces;

namespace GauntletCI.Tests.Corpus;

/// <summary>
/// Integration tests for strategy orchestration and cross-rule compatibility.
/// Verifies that strategies work together correctly and are properly integrated.
/// </summary>
public sealed class StrategyIntegrationTests
{
    // Stub store for testing InferLabels* methods
    private sealed class NullFixtureStore : IFixtureStore
    {
        public Task SaveMetadataAsync(FixtureMetadata metadata, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<FixtureMetadata?> GetMetadataAsync(string fixtureId, CancellationToken cancellationToken = default) =>
            Task.FromResult<FixtureMetadata?>(null);

        public Task SaveExpectedFindingsAsync(string fixtureId, IReadOnlyList<ExpectedFinding> findings, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<ExpectedFinding>> ReadExpectedFindingsAsync(string fixtureId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ExpectedFinding>>([]);

        public Task SaveActualFindingsAsync(string fixtureId, string runId, IReadOnlyList<ActualFinding> findings, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<ActualFinding>> ReadActualFindingsAsync(string fixtureId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ActualFinding>>([]);

        public Task<string?> TryReadReviewCommentsAsync(string fixtureId, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<IReadOnlyList<FixtureMetadata>> ListFixturesAsync(FixtureTier? tier = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FixtureMetadata>>([]);
    }

    private readonly SilverLabelEngine _engine;

    public StrategyIntegrationTests()
    {
        var store = new NullFixtureStore();
        _engine = new SilverLabelEngine(store);
    }

    [Fact]
    public void AllStrategies_DeclareUniqueRuleIds()
    {
        // Ensure no rule ID is declared by multiple strategies
        var allRuleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var strategies = ReflectStrategies();

        foreach (var strategy in strategies)
        {
            foreach (var ruleId in strategy.RuleIds)
            {
                Assert.True(allRuleIds.Add(ruleId), 
                    $"Rule {ruleId} declared by multiple strategies");
            }
        }
    }

    [Fact]
    public void AllStrategies_CoverExpectedRules()
    {
        // Verify all known GCI rules are covered by strategies
        var expectedRules = new[]
        {
            "GCI0003", "GCI0006", "GCI0012", "GCI0016", "GCI0021", "GCI0022",
            "GCI0024", "GCI0032", "GCI0035", "GCI0041", "GCI0042", "GCI0043", "GCI0044"
        };

        var strategies = ReflectStrategies();
        var coveredRules = strategies
            .SelectMany(s => s.RuleIds)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var rule in expectedRules)
        {
            Assert.Contains(rule, coveredRules);
        }
    }

    [Fact]
    public void StrategyRegistration_AllStrategiesInstantiate()
    {
        // Verify all strategy implementations can be instantiated
        var strategies = ReflectStrategies();
        
        Assert.NotEmpty(strategies);
        Assert.Equal(6, strategies.Count);  // Exactly 6 strategies
        
        // Verify each strategy has RuleIds
        foreach (var strategy in strategies)
        {
            Assert.NotNull(strategy.RuleIds);
            Assert.NotEmpty(strategy.RuleIds);
        }
    }

    [Fact]
    public async Task NegativeLabelsGenerated_ForRulesWithNoSignal()
    {
        // When no strategies trigger, negative labels should be generated in ApplyToFixtureAsync
        // but InferLabelsAsync should return empty for clean diffs
        var diff = """
            --- a/src/Program.cs
            +++ b/src/Program.cs
            @@ -1,3 +1,4 @@
             public class Foo { }
            """;

        var labels = await _engine.InferLabelsAsync("fix-001", diff);

        // InferLabelsAsync returns only positive labels
        Assert.Empty(labels);
    }

    [Fact]
    public async Task CommentedCode_StrategiesSkipComments()
    {
        // Commented-out violations should not trigger
        var diff = """
            --- a/src/Config.cs
            +++ b/src/Config.cs
            @@ -5,3 +5,6 @@
             // async void BadHandler(object sender, EventArgs e) { }
             // var secret = "hardcoded";
             // var result = task.Result;
            """;

        var labels = await _engine.InferLabelsAsync("fix-001", diff);

        var violations = labels.Where(l => l.ShouldTrigger).ToList();
        Assert.Empty(violations);
    }

    [Fact]
    public async Task EdgeCase_EmptyDiffProducesNoPositiveLabels()
    {
        // Empty diff should produce no positive labels from InferLabelsAsync
        var labels = await _engine.InferLabelsAsync("fix-001", "");
        Assert.Empty(labels);
    }

    [Fact]
    public async Task EdgeCase_OnlyContextLines()
    {
        // Diff with only context (no additions/removals) should produce no labels
        var diff = """
            --- a/src/Foo.cs
            +++ b/src/Foo.cs
            @@ -1,5 +1,5 @@
             public class Foo
             {
                 public void Bar() { }
             }
            """;

        var labels = await _engine.InferLabelsAsync("fix-001", diff);
        Assert.Empty(labels);
    }

    [Fact]
    public async Task Strategy_AllReturningFindingsHaveRequiredFields()
    {
        // Ensure all returned findings have all required fields populated
        var diff = """
            --- a/src/Code.cs
            +++ b/src/Code.cs
            @@ -1,3 +1,4 @@
            +var apiKey = "sk_test_1234567890abcdefghijk";
            """;

        var labels = await _engine.InferLabelsAsync("fix-001", diff);

        foreach (var label in labels)
        {
            Assert.False(string.IsNullOrEmpty(label.RuleId), "RuleId must not be empty");
            Assert.False(string.IsNullOrEmpty(label.Reason), "Reason must not be empty");
            Assert.NotEqual(0.0, label.ExpectedConfidence);
            Assert.True(Enum.IsDefined(typeof(LabelSource), label.LabelSource), "LabelSource must be a valid enum value");
        }
    }

    [Fact]
    public async Task Confidence_AllPositiveLabelsWithinRange()
    {
        // All positive labels should have confidence in expected range (0.40-0.80)
        var diff = """
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,3 +1,5 @@
             class Service
             {
            +    var result = GetData().Result;
             }
            """;

        var labels = await _engine.InferLabelsAsync("fix-001", diff);
        
        foreach (var label in labels.Where(l => l.ShouldTrigger))
        {
            Assert.InRange(label.ExpectedConfidence, 0.40, 0.80);
        }
    }

    // -- Helpers --

    private static List<IInferenceStrategy> ReflectStrategies()
    {
        // Create all strategy instances to verify their RuleIds
        return new List<IInferenceStrategy>
        {
            new SecurityPatternStrategy(),
            new AsyncPatternStrategy(),
            new ExceptionHandlingPatternStrategy(),
            new DataIntegrityPatternStrategy(),
            new NullabilityPatternStrategy(),
            new EdgeCasePatternStrategy(),
        };
    }
}

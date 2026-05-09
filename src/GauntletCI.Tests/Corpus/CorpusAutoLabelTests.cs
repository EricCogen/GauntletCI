// SPDX-License-Identifier: Elastic-2.0
using System.IO;
using GauntletCI.Corpus.Interfaces;
using GauntletCI.Corpus.Labeling;
using GauntletCI.Corpus.Models;

namespace GauntletCI.Tests.Corpus;

[Collection("ConsoleOut")]
public sealed class CorpusAutoLabelTests
{
    // ── Controllable fake fixture store ───────────────────────────────────────

    private sealed class FakeFixtureStore : IFixtureStore
    {
        private readonly IReadOnlyList<ActualFinding> _actualFindings;
        private readonly string? _reviewCommentsJson;
        private readonly IReadOnlyList<ExpectedFinding> _existingLabels;

        public List<ExpectedFinding> SavedFindings { get; } = [];

        public FakeFixtureStore(
            IReadOnlyList<ActualFinding>? actualFindings = null,
            string? reviewCommentsJson = null,
            IReadOnlyList<ExpectedFinding>? existingLabels = null)
        {
            _actualFindings    = actualFindings    ?? [];
            _reviewCommentsJson = reviewCommentsJson;
            _existingLabels    = existingLabels    ?? [];
        }

        public Task<IReadOnlyList<ActualFinding>> ReadActualFindingsAsync(
            string fixtureId, CancellationToken ct = default)
            => Task.FromResult(_actualFindings);

        public Task<string?> TryReadReviewCommentsAsync(
            string fixtureId, CancellationToken ct = default)
            => Task.FromResult(_reviewCommentsJson);

        public Task<IReadOnlyList<ExpectedFinding>> ReadExpectedFindingsAsync(
            string fixtureId, CancellationToken ct = default)
            => Task.FromResult(_existingLabels);

        public Task SaveExpectedFindingsAsync(
            string fixtureId, IReadOnlyList<ExpectedFinding> findings, CancellationToken ct = default)
        {
            SavedFindings.Clear();
            SavedFindings.AddRange(findings);
            return Task.CompletedTask;
        }

        // Unused stubs
        public Task SaveMetadataAsync(FixtureMetadata metadata, CancellationToken ct = default) => Task.CompletedTask;
        public Task<FixtureMetadata?> GetMetadataAsync(string fixtureId, CancellationToken ct = default) => Task.FromResult<FixtureMetadata?>(null);
        public Task SaveActualFindingsAsync(string fixtureId, string runId, IReadOnlyList<ActualFinding> findings, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<FixtureMetadata>> ListFixturesAsync(FixtureTier? tier = null, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<FixtureMetadata>>([]);
    }

    // ── Controllable fake LLM labeler ─────────────────────────────────────────

    private sealed class FakeLlmLabeler(LlmLabelResult? result) : ILlmLabeler
    {
        public Task<LlmLabelResult?> ClassifyAsync(
            string ruleId, string findingMessage, string evidence, string? filePath,
            IEnumerable<string> reviewCommentBodies, string diffSnippet, CancellationToken ct = default)
            => Task.FromResult(result);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tier 2: File-path correlation
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApplyToFixture_FilePathCorrelation_EmitsPositiveLabelForMatchingRule()
    {
        // Arrange: reviewer commented on Foo/Bar.cs, GCI0006 fired on the same file
        var actualFindings = new List<ActualFinding>
        {
            new() { RuleId = "GCI0006", DidTrigger = true, FilePath = "Foo/Bar.cs", Message = "Null dereference risk" },
        };
        // Review comment references the same path
        const string reviewJson = """[{"body": "Please review this code", "path": "Foo/Bar.cs"}]""";

        var store  = new FakeFixtureStore(actualFindings, reviewJson);
        var engine = new SilverLabelEngine(store, new NullLlmLabeler());

        // Act: benign diff that doesn't trigger any Tier 1 heuristic for GCI0006
        await engine.ApplyToFixtureAsync("test-fixture", "--- a/Foo/Bar.cs\n+++ b/Foo/Bar.cs\n@@ -1 +1 @@\n+public class Foo {}");

        // Assert: at least one positive label for GCI0006 via file-path correlation
        Assert.Contains(store.SavedFindings, f => f.RuleId == "GCI0006" && f.ShouldTrigger);
    }

    [Fact]
    public async Task ApplyToFixture_FilePathCorrelation_NormalizesPathCasing()
    {
        // Arrange: reviewer path uses different casing from finding's FilePath
        var actualFindings = new List<ActualFinding>
        {
            new() { RuleId = "GCI0006", DidTrigger = true, FilePath = "Src/My.cs", Message = "Null risk" },
        };
        const string reviewJson = """[{"body": "Check this", "path": "src/my.cs"}]""";

        var store  = new FakeFixtureStore(actualFindings, reviewJson);
        var engine = new SilverLabelEngine(store, new NullLlmLabeler());

        await engine.ApplyToFixtureAsync("test-fixture", "");

        Assert.Contains(store.SavedFindings, f => f.RuleId == "GCI0006" && f.ShouldTrigger);
    }

    [Fact]
    public async Task ApplyToFixture_FilePathCorrelation_NoMatchWhenPathsDiffer()
    {
        // Arrange: reviewer commented on a different file than where GCI0006 fired
        var actualFindings = new List<ActualFinding>
        {
            new() { RuleId = "GCI0006", DidTrigger = true, FilePath = "Src/Other.cs", Message = "Null risk" },
        };
        const string reviewJson = """[{"body": "Check this", "path": "src/my.cs"}]""";

        var store  = new FakeFixtureStore(actualFindings, reviewJson);
        var engine = new SilverLabelEngine(store, new NullLlmLabeler());

        await engine.ApplyToFixtureAsync("test-fixture", "");

        // GCI0006 should be negative (no path match, no Tier 1 signal)
        var gci6Label = store.SavedFindings.FirstOrDefault(f => f.RuleId == "GCI0006");
        Assert.NotNull(gci6Label);
        Assert.False(gci6Label.ShouldTrigger);
    }

    [Fact]
    public async Task ApplyToFixture_FilePathCorrelation_DoesNotDuplicateExistingPositiveLabel()
    {
        // Arrange: Tier 1 already produced a positive GCI0006 label via diff heuristic
        var actualFindings = new List<ActualFinding>
        {
            new() { RuleId = "GCI0006", DidTrigger = true, FilePath = "Foo/Bar.cs", Message = "Null risk" },
        };
        // Diff that triggers GCI0006 via Tier 1 (meaningful null assignment)
        const string diff = """
            --- a/Foo/Bar.cs
            +++ b/Foo/Bar.cs
            @@ -1 +1 @@
            +    _service = null;
            """;
        const string reviewJson = """[{"body": "Check this", "path": "Foo/Bar.cs"}]""";

        var store  = new FakeFixtureStore(actualFindings, reviewJson);
        var engine = new SilverLabelEngine(store, new NullLlmLabeler());

        await engine.ApplyToFixtureAsync("test-fixture", diff);

        // Should have exactly one GCI0006 label, not two
        Assert.Single(store.SavedFindings, f => f.RuleId == "GCI0006");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // NullLlmLabeler
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task NullLlmLabeler_AlwaysReturnsNull()
    {
        var labeler = new NullLlmLabeler();

        var result = await labeler.ClassifyAsync(
            "GCI0006", "message", "evidence", "file.cs",
            ["comment body"], "diff snippet", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task NullLlmLabeler_ReturnsNullForEmptyArgs()
    {
        var labeler = new NullLlmLabeler();

        var result = await labeler.ClassifyAsync("", "", "", null, [], "", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ApplyToFixture_NullLlmLabeler_DoesNotEmitTier3Log()
    {
        var actualFindings = new List<ActualFinding>
        {
            new() { RuleId = "GCI0004", DidTrigger = true, FilePath = "Src/PublicApi.cs", Message = "Breaking change risk" },
        };

        var store  = new FakeFixtureStore(actualFindings);
        var engine = new SilverLabelEngine(store, new NullLlmLabeler());
        var sw = new StringWriter();
        var original = Console.Out;
        Console.SetOut(sw);
        try
        {
            await engine.ApplyToFixtureAsync("test-fixture", "--- a/Src/PublicApi.cs\n+++ b/Src/PublicApi.cs\n@@ -1 +1 @@\n+public class PublicApi {}");
        }
        finally
        {
            Console.SetOut(original);
        }

        Assert.DoesNotContain("Tier 3 calling NullLlmLabeler", sw.ToString());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // LlmReview LabelSource handling in MergeLabels
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApplyToFixture_LlmReviewDoesNotOverwriteHumanReviewLabel()
    {
        // Arrange: existing GCI0006 has a HumanReview label (manually decided: false positive)
        var existingLabels = new List<ExpectedFinding>
        {
            new()
            {
                RuleId             = "GCI0006",
                ShouldTrigger      = false,
                ExpectedConfidence = 0.95,
                LabelSource        = LabelSource.HumanReview,
                Reason             = "Manual: assignment is always overwritten before use",
            },
        };

        // Actual finding that would normally send to LLM fallback
        var actualFindings = new List<ActualFinding>
        {
            new() { RuleId = "GCI0006", DidTrigger = true, FilePath = "Foo/Bar.cs", Message = "Null risk" },
        };

        // LLM says it's a TP: should be ignored because HumanReview is gold
        var llmLabeler = new FakeLlmLabeler(new LlmLabelResult(true, 0.9, "Looks risky"));
        var store      = new FakeFixtureStore(actualFindings, null, existingLabels);
        var engine     = new SilverLabelEngine(store, llmLabeler);

        await engine.ApplyToFixtureAsync("test-fixture", "");

        // Assert: HumanReview label must be preserved
        var gci6Label = Assert.Single(store.SavedFindings, f => f.RuleId == "GCI0006");
        Assert.Equal(LabelSource.HumanReview, gci6Label.LabelSource);
        Assert.False(gci6Label.ShouldTrigger);
    }

    [Fact]
    public async Task ApplyToFixture_LlmReviewLabelAppliedForUncertainFinding()
    {
        // Arrange: GCI0006 fires but no Tier 1/2 heuristic matches; LLM should label it
        var actualFindings = new List<ActualFinding>
        {
            new() { RuleId = "GCI0006", DidTrigger = true, FilePath = "Src/Foo.cs", Message = "Null risk" },
        };

        var llmLabeler = new FakeLlmLabeler(new LlmLabelResult(true, 0.8, "Clearly risky pattern"));
        var store      = new FakeFixtureStore(actualFindings);
        var engine     = new SilverLabelEngine(store, llmLabeler);

        // Benign diff: no Tier 1 signal for GCI0006
        await engine.ApplyToFixtureAsync("test-fixture", "--- a/x.cs\n+++ b/x.cs\n@@ -1 +1 @@\n+public class X {}");

        var gci6Label = store.SavedFindings.FirstOrDefault(f => f.RuleId == "GCI0006");
        Assert.NotNull(gci6Label);
        Assert.Equal(LabelSource.LlmReview, gci6Label.LabelSource);
        Assert.True(gci6Label.ShouldTrigger);
        Assert.StartsWith("[llm]", gci6Label.Reason);
    }

    [Fact]
    public async Task ApplyToFixture_InconclusiveLlmResult_EmitsNegativeLabel()
    {
        // Arrange: LLM returns a result with confidence < 0.4 (inconclusive)
        var actualFindings = new List<ActualFinding>
        {
            new() { RuleId = "GCI0006", DidTrigger = true, FilePath = "Src/Foo.cs", Message = "Null risk" },
        };

        var inconclusiveResult = new LlmLabelResult(true, 0.3, "Not sure", IsInconclusive: true);
        var llmLabeler = new FakeLlmLabeler(inconclusiveResult);
        var store      = new FakeFixtureStore(actualFindings);
        var engine     = new SilverLabelEngine(store, llmLabeler);

        await engine.ApplyToFixtureAsync("test-fixture", "");

        // Inconclusive → no LlmReview label; falls back to negative heuristic label
        var gci6Label = store.SavedFindings.FirstOrDefault(f => f.RuleId == "GCI0006");
        Assert.NotNull(gci6Label);
        Assert.NotEqual(LabelSource.LlmReview, gci6Label.LabelSource);
        Assert.False(gci6Label.ShouldTrigger);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // LlmReview enum value existence
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void LabelSource_ContainsLlmReviewValue()
    {
        Assert.True(Enum.IsDefined(typeof(LabelSource), LabelSource.LlmReview));
    }
}

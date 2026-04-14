// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Corpus.Interfaces;
using GauntletCI.Corpus.Labeling;
using GauntletCI.Corpus.Models;

namespace GauntletCI.Tests;

public sealed class SilverLabelEngineTests
{
    // Stub store — InferLabels* methods do not call _store
    private sealed class NullFixtureStore : IFixtureStore
    {
        public Task SaveMetadataAsync(FixtureMetadata metadata, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<FixtureMetadata?> GetMetadataAsync(string fixtureId, CancellationToken cancellationToken = default) =>
            Task.FromResult<FixtureMetadata?>(null);

        public Task SaveExpectedFindingsAsync(string fixtureId, IReadOnlyList<ExpectedFinding> findings, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task SaveActualFindingsAsync(string fixtureId, string runId, IReadOnlyList<ActualFinding> findings, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<FixtureMetadata>> ListFixturesAsync(FixtureTier? tier = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FixtureMetadata>>([]);
    }

    private readonly SilverLabelEngine _engine = new(new NullFixtureStore());

    private static string CommentsJson(params string[] bodies) =>
        "[" + string.Join(",", bodies.Select(b => $$"""{"body":"{{b}}"}""")) + "]";

    // -------------------------------------------------------------------------
    // InferLabelsFromCommentsAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InferLabelsFromComments_CommentMentioningNeedsTests_EmitsGCI0005Label()
    {
        // Arrange
        var json = CommentsJson("This PR needs tests for the new logic");

        // Act
        var labels = await _engine.InferLabelsFromCommentsAsync(json);

        // Assert
        var label = Assert.Single(labels, l => l.RuleId == "GCI0005");
        Assert.True(label.ShouldTrigger);
        Assert.Equal(LabelSource.Heuristic, label.LabelSource);
    }

    [Fact]
    public async Task InferLabelsFromComments_CommentMentioningBreakingChange_EmitsGCI0004Label()
    {
        // Arrange
        var json = CommentsJson("This looks like a breaking change to the public API");

        // Act
        var labels = await _engine.InferLabelsFromCommentsAsync(json);

        // Assert
        var label = Assert.Single(labels, l => l.RuleId == "GCI0004");
        Assert.True(label.ShouldTrigger);
        Assert.Equal(LabelSource.Heuristic, label.LabelSource);
    }

    [Fact]
    public async Task InferLabelsFromComments_CommentMentioningSecret_EmitsGCI0007Label()
    {
        // Arrange
        var json = CommentsJson("Are you sure you want to commit this password here?");

        // Act
        var labels = await _engine.InferLabelsFromCommentsAsync(json);

        // Assert
        var label = Assert.Single(labels, l => l.RuleId == "GCI0007");
        Assert.True(label.ShouldTrigger);
    }

    [Fact]
    public async Task InferLabelsFromComments_CommentMentioningThreadSafe_EmitsGCI0016Label()
    {
        // Arrange
        var json = CommentsJson("Is this method thread safe? Could there be a race condition here?");

        // Act
        var labels = await _engine.InferLabelsFromCommentsAsync(json);

        // Assert
        var label = Assert.Single(labels, l => l.RuleId == "GCI0016");
        Assert.True(label.ShouldTrigger);
    }

    [Fact]
    public async Task InferLabelsFromComments_EmptyJson_ReturnsEmpty()
    {
        // Act
        var labels = await _engine.InferLabelsFromCommentsAsync("[]");

        // Assert
        Assert.Empty(labels);
    }

    [Fact]
    public async Task InferLabelsFromComments_MalformedJson_ReturnsEmpty_NoException()
    {
        // Act — malformed JSON must not throw; engine silently swallows JsonException
        var labels = await _engine.InferLabelsFromCommentsAsync("{ not valid json {{{{");

        // Assert
        Assert.Empty(labels);
    }

    [Fact]
    public async Task InferLabelsFromComments_MultipleMatchingComments_DeduplicatesLabels()
    {
        // Arrange — two comments both match "needs tests" → only one GCI0005 label emitted
        var json = CommentsJson("You need to add test coverage here", "Also needs tests for the edge case");

        // Act
        var labels = await _engine.InferLabelsFromCommentsAsync(json);

        // Assert
        Assert.Single(labels, l => l.RuleId == "GCI0005");
    }

    // -------------------------------------------------------------------------
    // InferLabelsAsync (diff-based heuristics)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InferLabels_DiffWithNoMatchingPatterns_ReturnsEmptyList()
    {
        // Arrange — benign change with no heuristic triggers
        var diff = """
            --- a/src/Foo.cs
            +++ b/src/Foo.cs
            @@ -1,3 +1,4 @@
            +public class Bar { }
            """;

        // Act
        var labels = await _engine.InferLabelsAsync("fix-001", diff);

        // Assert — InferLabelsAsync only emits positive labels; no match → empty
        Assert.Empty(labels);
    }

    [Fact]
    public async Task InferLabels_EmptyDiff_ReturnsEmptyList()
    {
        // Act
        var labels = await _engine.InferLabelsAsync("fix-001", "");

        // Assert
        Assert.Empty(labels);
    }

    [Fact]
    public void RulesWithHeuristics_ContainsExpectedRuleIds()
    {
        // Arrange
        var expected = new[]
        {
            "GCI0003", "GCI0004", "GCI0005", "GCI0006", "GCI0007",
            "GCI0010", "GCI0016", "GCI0021", "GCI0022", "GCI0023",
        };

        // Assert — every expected rule is present and the set is exactly this size
        foreach (var ruleId in expected)
            Assert.Contains(ruleId, SilverLabelEngine.RulesWithHeuristics);

        Assert.Equal(expected.Length, SilverLabelEngine.RulesWithHeuristics.Count);
    }

    [Fact]
    public async Task InferLabels_DiffWithResultPattern_EmitsGCI0016Label()
    {
        // Arrange — added line accesses .Result (sync-over-async anti-pattern)
        var diff = """
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -5,5 +5,6 @@
            +    var data = _repo.GetAsync().Result;
            """;

        // Act
        var labels = await _engine.InferLabelsAsync("fix-001", diff);

        // Assert
        Assert.Contains(labels, l => l.RuleId == "GCI0016" && l.ShouldTrigger);
    }

    [Fact]
    public async Task InferLabels_DiffWithMigrationFilePath_EmitsGCI0021Label()
    {
        // Arrange — diff header includes a Migrations/ path
        var diff = """
            diff --git a/Migrations/20240101_AddUsersTable.cs b/Migrations/20240101_AddUsersTable.cs
            --- a/Migrations/20240101_AddUsersTable.cs
            +++ b/Migrations/20240101_AddUsersTable.cs
            @@ -0,0 +1,3 @@
            +public class AddUsersTable { }
            """;

        // Act
        var labels = await _engine.InferLabelsAsync("fix-001", diff);

        // Assert
        Assert.Contains(labels, l => l.RuleId == "GCI0021" && l.ShouldTrigger);
    }

    [Fact]
    public async Task InferLabels_DiffWithCredentialAssignment_EmitsGCI0007Label()
    {
        // Arrange — added line assigns a literal string to a credential keyword variable
        var diff = """
            --- a/src/Config.cs
            +++ b/src/Config.cs
            @@ -3,3 +3,4 @@
            +    var password = "SuperSecretValue";
            """;

        // Act
        var labels = await _engine.InferLabelsAsync("fix-001", diff);

        // Assert
        Assert.Contains(labels, l => l.RuleId == "GCI0007" && l.ShouldTrigger);
    }
}

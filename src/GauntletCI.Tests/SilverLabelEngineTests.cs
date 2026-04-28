// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Corpus.Interfaces;
using GauntletCI.Corpus.Labeling;
using GauntletCI.Corpus.Models;

namespace GauntletCI.Tests;

public sealed class SilverLabelEngineTests
{
    // Stub store: InferLabels* methods do not call _store
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

    private readonly SilverLabelEngine _engine = new(new NullFixtureStore());

    private static string CommentsJson(params string[] bodies) =>
        "[" + string.Join(",", bodies.Select(b => $$"""{"body":"{{b}}"}""")) + "]";

    // -------------------------------------------------------------------------
    // InferLabelsFromCommentsAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InferLabelsFromComments_CommentMentioningNeedsTests_EmitsGCI0041Label()
    {
        // Arrange
        var json = CommentsJson("This PR needs tests for the new logic");

        // Act
        var labels = await _engine.InferLabelsFromCommentsAsync(json);

        // Assert
        var label = Assert.Single(labels, l => l.RuleId == "GCI0041");
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
    public async Task InferLabelsFromComments_CommentMentioningSecret_EmitsGCI0012Label()
    {
        // Arrange
        var json = CommentsJson("Are you sure you want to commit this password here?");

        // Act
        var labels = await _engine.InferLabelsFromCommentsAsync(json);

        // Assert
        var label = Assert.Single(labels, l => l.RuleId == "GCI0012");
        Assert.True(label.ShouldTrigger);
    }

    [Fact]
    public async Task InferLabelsFromComments_CommentMentioningThreadSafe_DoesNotEmitGCI0016Label()
    {
        // Thread-safety comments no longer map to GCI0016: that scope was dropped when
        // static mutable field detection was removed from the rule. "thread safe / race condition"
        // concerns belong to static analysis tools, not this diff-pattern rule.

        // Arrange
        var json = CommentsJson("Is this method thread safe? Could there be a race condition here?");

        // Act
        var labels = await _engine.InferLabelsFromCommentsAsync(json);

        // Assert
        Assert.DoesNotContain(labels, l => l.RuleId == "GCI0016");
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
        // Act: malformed JSON must not throw; engine silently swallows JsonException
        var labels = await _engine.InferLabelsFromCommentsAsync("{ not valid json {{{{");

        // Assert
        Assert.Empty(labels);
    }

    [Fact]
    public async Task InferLabelsFromComments_MultipleMatchingComments_DeduplicatesLabels()
    {
        // Arrange: two comments both match "needs tests" → only one GCI0041 label emitted
        var json = CommentsJson("You need to add test coverage here", "Also needs tests for the edge case");

        // Act
        var labels = await _engine.InferLabelsFromCommentsAsync(json);

        // Assert
        Assert.Single(labels, l => l.RuleId == "GCI0041");
    }

    // -------------------------------------------------------------------------
    // InferLabelsAsync (diff-based heuristics)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InferLabels_DiffWithNoMatchingPatterns_ReturnsEmptyList()
    {
        // Arrange: benign change with no heuristic triggers
        var diff = """
            --- a/src/Foo.cs
            +++ b/src/Foo.cs
            @@ -1,3 +1,4 @@
            +public class Bar { }
            """;

        // Act
        var labels = await _engine.InferLabelsAsync("fix-001", diff);

        // Assert: InferLabelsAsync only emits positive labels; no match → empty
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
        // Arrange - spot-check a representative subset of rules with heuristics
        var expected = new[]
        {
            "GCI0003", "GCI0004", "GCI0006", "GCI0010",
            "GCI0012", "GCI0016", "GCI0021", "GCI0022",
            "GCI0024", "GCI0029", "GCI0036", "GCI0047",
        };

        // Assert: every expected rule is present and the set has the expected total
        foreach (var ruleId in expected)
            Assert.Contains(ruleId, SilverLabelEngine.RulesWithHeuristics);

        Assert.Equal(22, SilverLabelEngine.RulesWithHeuristics.Count);
    }

    [Fact]
    public async Task InferLabels_DiffWithResultPattern_EmitsGCI0016Label()
    {
        // Arrange: added line accesses .Result (sync-over-async anti-pattern)
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
    public async Task InferLabels_DiffWithAsyncVoid_EmitsGCI0016Label()
    {
        // Arrange: async void method (not an event handler)
        var diff = """
            --- a/src/Worker.cs
            +++ b/src/Worker.cs
            @@ -5,5 +5,6 @@
            +    public async void RunBackground() { await Task.Delay(1000); }
            """;

        // Act
        var labels = await _engine.InferLabelsAsync("fix-001", diff);

        // Assert
        Assert.Contains(labels, l => l.RuleId == "GCI0016" && l.ShouldTrigger);
    }

    [Fact]
    public async Task InferLabels_DiffWithLockThis_EmitsGCI0016Label()
    {
        // Arrange: lock(this) antipattern
        var diff = """
            --- a/src/Cache.cs
            +++ b/src/Cache.cs
            @@ -5,5 +5,6 @@
            +    lock (this) { _items.Add(item); }
            """;

        // Act
        var labels = await _engine.InferLabelsAsync("fix-001", diff);

        // Assert
        Assert.Contains(labels, l => l.RuleId == "GCI0016" && l.ShouldTrigger);
    }

    [Fact]
    public async Task InferLabels_DiffWithAsyncVoidEventHandler_DoesNotEmitGCI0016Label()
    {
        // Arrange: async void event handler is a legitimate pattern
        var diff = """
            --- a/src/Page.cs
            +++ b/src/Page.cs
            @@ -5,5 +5,6 @@
            +    private async void OnClick(object sender, EventArgs e) { await LoadAsync(); }
            """;

        // Act
        var labels = await _engine.InferLabelsAsync("fix-001", diff);

        // Assert
        Assert.DoesNotContain(labels, l => l.RuleId == "GCI0016" && l.ShouldTrigger);
    }

    [Fact]
    public async Task InferLabels_DiffWithMigrationFileAndDropColumn_EmitsGCI0021Label()
    {
        // Arrange: migration file modified with a removed migrationBuilder.DropColumn call
        var diff = """
            diff --git a/src/Migrations/20240101_AddUsersTable.cs b/src/Migrations/20240101_AddUsersTable.cs
            --- a/src/Migrations/20240101_AddUsersTable.cs
            +++ b/src/Migrations/20240101_AddUsersTable.cs
            @@ -5,4 +5,3 @@
             protected override void Down(MigrationBuilder migrationBuilder)
             {
            -    migrationBuilder.DropColumn(name: "LegacyField", table: "Users");
             }
            """;

        // Act
        var labels = await _engine.InferLabelsAsync("fix-001", diff);

        // Assert
        Assert.Contains(labels, l => l.RuleId == "GCI0021" && l.ShouldTrigger);
    }

    [Fact]
    public async Task InferLabels_DiffWithMigrationFileNoSchemaOp_ShouldNotEmitGCI0021Label()
    {
        // Arrange: migration file modified but removed lines have no schema operations (scaffolding logic only)
        var diff = """
            diff --git a/src/Migrations/Internal/SnapshotProcessor.cs b/src/Migrations/Internal/SnapshotProcessor.cs
            --- a/src/Migrations/Internal/SnapshotProcessor.cs
            +++ b/src/Migrations/Internal/SnapshotProcessor.cs
            @@ -10,3 +10,2 @@
            -    if (version.StartsWith("8.", StringComparison.Ordinal)) return true;
            """;

        // Act
        var labels = await _engine.InferLabelsAsync("fix-001", diff);

        // Assert: no GCI0021 trigger: modified migration-dir file but no schema op in removed lines
        Assert.DoesNotContain(labels, l => l.RuleId == "GCI0021" && l.ShouldTrigger);
    }

    [Fact]
    public async Task InferLabels_DiffWithCredentialAssignment_EmitsGCI0012Label()
    {
        // Arrange: added line assigns a literal string to a credential keyword variable
        var diff = """
            --- a/src/Config.cs
            +++ b/src/Config.cs
            @@ -3,3 +3,4 @@
            +    var password = "SuperSecretValue";
            """;

        // Act
        var labels = await _engine.InferLabelsAsync("fix-001", diff);

        // Assert
        Assert.Contains(labels, l => l.RuleId == "GCI0012" && l.ShouldTrigger);
    }

    [Fact]
    public async Task InferLabelsFromComments_NullInput_Throws()
    {
        // Null input throws ArgumentNullException - this documents actual behavior
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _engine.InferLabelsFromCommentsAsync(null!));
    }

    [Fact]
    public async Task InferLabelsFromComments_WhitespaceInput_ReturnsEmpty()
    {
        var labels = await _engine.InferLabelsFromCommentsAsync("   ");
        Assert.Empty(labels);
    }

    [Fact]
    public async Task InferLabels_VeryLargeDiff_HandlesGracefully()
    {
        // Build a 10,000 line diff
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("--- a/src/Large.cs");
        sb.AppendLine("+++ b/src/Large.cs");
        sb.AppendLine("@@ -1,3 +1,10003 @@");
        for (int i = 0; i < 10000; i++)
            sb.AppendLine($"+    int field{i} = {i};");

        var ex = await Record.ExceptionAsync(() => _engine.InferLabelsAsync("large", sb.ToString()));

        Assert.Null(ex);
    }
}

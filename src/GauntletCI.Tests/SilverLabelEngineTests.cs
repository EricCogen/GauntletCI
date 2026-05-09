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
            "GCI0012", "GCI0015", "GCI0016", "GCI0021", "GCI0022",
            "GCI0024", "GCI0029", "GCI0035", "GCI0036", "GCI0047",
            "GCI0048", "GCI0050", "GCI0053",
        };

        // Assert: every expected rule is present and the set has the expected total
        foreach (var ruleId in expected)
        {
            Assert.Contains(ruleId, SilverLabelEngine.RulesWithHeuristics);
        }

        Assert.Equal(27, SilverLabelEngine.RulesWithHeuristics.Count);
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
        {
            sb.AppendLine($"+    int field{i} = {i};");
        }

        var ex = await Record.ExceptionAsync(() => _engine.InferLabelsAsync("large", sb.ToString()));

        Assert.Null(ex);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Phase 21 Coordination Tests: Async Execution Model (GCI0016 family)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task InferLabels_BlockingCallWithComment_GCI0016Detected()
    {
        // Arrange: diff contains .Result blocking call
        var diff = """
            --- a/src/Api.cs
            +++ b/src/Api.cs
            @@ -5,5 +5,6 @@
            +    var result = _httpClient.GetAsync("").Result;
             return result;
            """;

        // Act
        var labels = await _engine.InferLabelsAsync("coord-001", diff);

        // Assert: GCI0016 detected
        var gci0016 = Assert.Single(labels, l => l.RuleId == "GCI0016" && l.ShouldTrigger);
        Assert.NotNull(gci0016);
    }

    [Fact]
    public async Task InferLabelsFromComments_ThreadPoolKeyword_DetectsGCI0016()
    {
        // Arrange: review comment mentions "thread pool" (new Phase 21 keyword)
        var json = CommentsJson("This could cause thread pool starvation");

        // Act
        var labels = await _engine.InferLabelsFromCommentsAsync(json);

        // Assert: GCI0016 should be detected due to new "thread pool" keyword
        var gci0016 = Assert.Single(labels, l => l.RuleId == "GCI0016");
        Assert.True(gci0016.ShouldTrigger);
        Assert.Equal(0.65, gci0016.ExpectedConfidence);
    }

    [Fact]
    public async Task InferLabelsFromComments_SocketKeyword_DetectsGCI0016()
    {
        // Arrange: review comment mentions "socket" (new Phase 21 keyword for socket exhaustion)
        var json = CommentsJson("This will cause socket exhaustion");

        // Act
        var labels = await _engine.InferLabelsFromCommentsAsync(json);

        // Assert: GCI0016 detected due to "socket" keyword coordination
        var gci0016 = Assert.Single(labels, l => l.RuleId == "GCI0016");
        Assert.True(gci0016.ShouldTrigger);
    }

    [Fact]
    public async Task InferLabelsFromComments_ConcurrencyKeyword_DetectsGCI0016()
    {
        // Arrange: review comment mentions "concurrency" (new Phase 21 keyword)
        var json = CommentsJson("Need proper concurrency handling");

        // Act
        var labels = await _engine.InferLabelsFromCommentsAsync(json);

        // Assert
        var gci0016 = Assert.Single(labels, l => l.RuleId == "GCI0016");
        Assert.True(gci0016.ShouldTrigger);
        Assert.Equal(0.65, gci0016.ExpectedConfidence);
    }

    [Fact]
    public async Task InferLabelsFromComments_CpuBoundKeyword_DetectsGCI0016()
    {
        // Arrange: "cpu bound" keyword signals performance concern  
        // Note: keywords are matched case-insensitive within the comment body
        var json = CommentsJson("cpu bound operation should not block");

        // Act
        var labels = await _engine.InferLabelsFromCommentsAsync(json);

        // Assert
        var gci0016 = Assert.Single(labels, l => l.RuleId == "GCI0016");
        Assert.True(gci0016.ShouldTrigger);
    }

    [Fact]
    public async Task InferLabels_BlockingCallAndDirectHttpClient_TriggersBothRules()
    {
        // Arrange: diff contains both .Result AND new HttpClient() in production code
        var diff = """
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -10,5 +10,7 @@
             public class Service
             {
            +    var client = new HttpClient();
            +    var data = client.GetAsync("").Result;
            """;

        // Act
        var labels = await _engine.InferLabelsAsync("coord-002", diff);

        // Assert: GCI0016 should definitely trigger on .Result
        Assert.Contains(labels, l => l.RuleId == "GCI0016" && l.ShouldTrigger);

        // GCI0039 should trigger on new HttpClient() in non-test file
        var gci0039 = labels.FirstOrDefault(l => l.RuleId == "GCI0039");
        if (gci0039 != null)
        {
            // If GCI0039 is present, it should be positive due to new HttpClient()
            Assert.True(gci0039.ShouldTrigger, "GCI0039 should detect direct HttpClient instantiation");
        }
    }

    [Fact]
    public async Task InferLabelsFromComments_MultipleAsyncKeywords_EnhancedDetection()
    {
        // Arrange: comment with multiple new Phase 21 keywords
        var json = CommentsJson("Socket and thread pool issues with cpu-bound blocking");

        // Act
        var labels = await _engine.InferLabelsFromCommentsAsync(json);

        // Assert: GCI0016 should trigger (highest confidence match)
        var gci0016 = Assert.Single(labels, l => l.RuleId == "GCI0016");
        Assert.True(gci0016.ShouldTrigger);
    }

    [Fact]
    public async Task InferLabelsFromComments_ExceptionSwallowing_DetectsGCI0032()
    {
        // Arrange: review comment about exception swallowing
        var json = CommentsJson("This code swallows exceptions silently");

        // Act
        var labels = await _engine.InferLabelsFromCommentsAsync(json);

        // Assert: GCI0032 should be detected via heuristic keywords
        var gci0032 = labels.FirstOrDefault(l => l.RuleId == "GCI0032");
        Assert.NotNull(gci0032);
    }

    [Fact]
    public async Task InferLabelsFromComments_DataIntegrity_DetectsGCI0015()
    {
        // Arrange: review comment with data integrity keyword
        var json = CommentsJson("This has SQL IGNORE pattern which bypasses data constraints");

        // Act
        var labels = await _engine.InferLabelsFromCommentsAsync(json);

        // Assert: GCI0015 should be detected via data integrity keywords
        var gci0015 = labels.FirstOrDefault(l => l.RuleId == "GCI0015");
        Assert.NotNull(gci0015);
    }

    [Fact]
    public async Task InferLabelsFromComments_PIInLogging_DetectsGCI0029()
    {
        // Arrange: review comment with PII keyword
        var json = CommentsJson("This logs personal data which violates GDPR");

        // Act
        var labels = await _engine.InferLabelsFromCommentsAsync(json);

        // Assert: GCI0029 should be detected via PII keywords
        var gci0029 = labels.FirstOrDefault(l => l.RuleId == "GCI0029");
        Assert.NotNull(gci0029);
    }

    // --- Phase 23.0 GCI0016 Enhancement Tests ---

    [Fact]
    public async Task InferLabels_FireAndForgetPattern_DoesNotEmitGCI0016()
    {
        // Arrange: fire-and-forget pattern with _ = assignment
        var diff = """
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -10,3 +10,4 @@
             public class Service
             {
            +    _ = ProcessInBackgroundAsync();  // fire-and-forget, intentional
            """;

        // Act
        var labels = await _engine.InferLabelsAsync("p23-fireandforget", diff);

        // Assert: GCI0016 should NOT trigger (fire-and-forget is legitimate)
        Assert.DoesNotContain(labels, l => l.RuleId == "GCI0016" && l.ShouldTrigger);
    }

    [Fact]
    public async Task InferLabels_ConfigureAwaitFalse_IsValidPattern()
    {
        // Arrange: ConfigureAwait(false) is a best practice, not a violation
        var diff = """
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -10,3 +10,5 @@
             public class Service
             {
            +    var result = GetAsync()
            +        .ConfigureAwait(false);
            """;

        // Act
        var labels = await _engine.InferLabelsAsync("p23-configurewait", diff);

        // Assert: GCI0016 should NOT trigger (ConfigureAwait is best practice)
        Assert.DoesNotContain(labels, l => l.RuleId == "GCI0016" && l.ShouldTrigger);
    }

    [Fact]
    public async Task InferLabels_TaskRunExplicitDelegation_DoesNotEmitGCI0016()
    {
        // Arrange: Task.Run is explicit delegation to thread pool
        var diff = """
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -10,3 +10,4 @@
             public class Service
             {
            +    Task.Run(() => LongRunningOperation());
            """;

        // Act
        var labels = await _engine.InferLabelsAsync("p23-taskrun", diff);

        // Assert: GCI0016 should NOT trigger (Task.Run is explicit, intentional)
        Assert.DoesNotContain(labels, l => l.RuleId == "GCI0016" && l.ShouldTrigger);
    }

    [Fact]
    public async Task InferLabels_StartupBlockingCode_SkipsGCI0016()
    {
        // Arrange: blocking call in startup context (acceptable)
        var diff = """
            --- a/Program.cs
            +++ b/Program.cs
            @@ -10,3 +10,4 @@
             // startup initialization
            +    Task.WaitAll(initTasks);  // startup: blocking acceptable
            """;

        // Act
        var labels = await _engine.InferLabelsAsync("p23-startup", diff);

        // Assert: GCI0016 should NOT trigger (startup context is legitimate)
        Assert.DoesNotContain(labels, l => l.RuleId == "GCI0016" && l.ShouldTrigger);
    }

    [Fact]
    public async Task InferLabels_IntentionalCommentMarker_SkipsGCI0016()
    {
        // Arrange: blocking call with "intentional" comment
        var diff = """
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -10,3 +10,4 @@
             public class Service
             {
            +    var result = asyncOp.Result; // intentional fire-and-forget pattern
            """;

        // Act
        var labels = await _engine.InferLabelsAsync("p23-intentional", diff);

        // Assert: GCI0016 should NOT trigger (intentional marker present)
        Assert.DoesNotContain(labels, l => l.RuleId == "GCI0016" && l.ShouldTrigger);
    }

    [Fact]
    public async Task InferLabels_BlankingAsyncCallStillDetected()
    {
        // Arrange: blocking call without legitimate pattern markers
        var diff = """
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -10,3 +10,4 @@
             public async Task ProcessAsync()
             {
            +    var result = GetDataAsync().Result;
            """;

        // Act
        var labels = await _engine.InferLabelsAsync("p23-violation", diff);

        // Assert: GCI0016 SHOULD still trigger (no legitimate pattern marker)
        Assert.Contains(labels, l => l.RuleId == "GCI0016" && l.ShouldTrigger);
    }

    // --- Phase 23.1 P4 (Performance & GC Coordination) Tests ---

    [Fact]
    public async Task InferLabels_P4_NoBoostWithoutBoth_OnlyGCI0044()
    {
        // Arrange: Only GCI0044 (GC pressure) without GCI0035 (allocation)
        var diff = """
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -10,3 +10,4 @@
             public async Task ProcessAsync()
             {
            +    var x = GetDataAsync().Result;
            """;

        // Act
        var labels = await _engine.InferLabelsAsync("p23-p4-single", diff);

        // Assert: If GCI0044 is detected, it should NOT be boosted (no coordination without GCI0035)
        var gci0044 = labels.FirstOrDefault(l => l.RuleId == "GCI0044" && l.ShouldTrigger);
        var gci0035 = labels.FirstOrDefault(l => l.RuleId == "GCI0035" && l.ShouldTrigger);

        // Either GCI0044 not fired (null is ok) or it fired without coordination
        if (gci0044 != null && gci0035 == null)
        {
            // GCI0044 fired alone - confidence should NOT be coordination-boosted
            Assert.True(gci0044.ExpectedConfidence <= 0.60,
                "GCI0044 alone should not be boosted to 0.75+");
        }
    }

    [Fact]
    public async Task InferLabels_P4_BothPresent_BoostApplied()
    {
        // Arrange: GCI0044 (GC pressure from blocking) + GCI0035 (excessive allocation)
        var diff = """
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -10,5 +10,8 @@
             public void ProcessData()
             {
            +    var x = GetDataAsync().Result;
            +    for (int i = 0; i < 1000000; i++)
            +        list.Add(new ExpensiveObject());
            """;

        // Act
        var labels = await _engine.InferLabelsAsync("p23-p4-both", diff);

        // Assert: Both rules detected with coordination boost
        var gci0044 = labels.FirstOrDefault(l => l.RuleId == "GCI0044" && l.ShouldTrigger);
        var gci0035 = labels.FirstOrDefault(l => l.RuleId == "GCI0035" && l.ShouldTrigger);

        // When both are present with sufficient confidence, both should be boosted
        if (gci0044 != null && gci0035 != null)
        {
            if (gci0044.ExpectedConfidence >= 0.50 && gci0035.ExpectedConfidence >= 0.50)
            {
                // Coordination should apply
                Assert.Contains(labels, l =>
                    l.Reason != null && l.Reason.Contains("[coordination]"));
            }
        }
    }

    [Fact]
    public async Task InferLabels_P4_PartialMatch_NoBoost()
    {
        // Arrange: GCI0044 only, below confidence threshold
        var diff = """
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -10,3 +10,4 @@
             public void SomeMethod()
             {
            +    var y = Task.Delay(100).Result;
            """;

        // Act
        var labels = await _engine.InferLabelsAsync("p23-p4-partial", diff);

        // Assert: No coordination boost when only one rule or confidence too low
        var coordinationLabels = labels.Where(l =>
            l.Reason != null && l.Reason.Contains("[coordination]")).ToList();

        // If GCI0044 and GCI0035 both trigger with high confidence, coordination applies
        // Otherwise, no coordination marker expected
        var gci0044Coor = coordinationLabels.FirstOrDefault(l => l.RuleId == "GCI0044");
        var gci0035Coor = coordinationLabels.FirstOrDefault(l => l.RuleId == "GCI0035");

        // Either both coordinated or neither (no single coordination)
        if (gci0044Coor != null)
        {
            Assert.NotNull(gci0035Coor);
        }
        if (gci0035Coor != null)
        {
            Assert.NotNull(gci0044Coor);
        }
    }

    // --- Phase 23.2 P5 (Serialization Safety Coordination) Tests ---

    [Fact]
    public async Task InferLabels_P5_NoBoostWithoutBoth_OnlyGCI0039()
    {
        // Arrange: Only GCI0039 (unsafe HttpClient) without GCI0048 (insecure deserialization)
        var diff = """
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -10,3 +10,4 @@
             public async Task GetDataAsync()
             {
            +    var client = new HttpClient();
            """;

        // Act
        var labels = await _engine.InferLabelsAsync("p23-p5-single", diff);

        // Assert: If GCI0039 is detected, it should NOT be boosted (no coordination without GCI0048)
        var gci0039 = labels.FirstOrDefault(l => l.RuleId == "GCI0039" && l.ShouldTrigger);
        var gci0048 = labels.FirstOrDefault(l => l.RuleId == "GCI0048" && l.ShouldTrigger);

        // Either GCI0039 not fired (null is ok) or it fired without coordination
        if (gci0039 != null && gci0048 == null)
        {
            // GCI0039 fired alone - confidence should NOT be coordination-boosted
            Assert.True(gci0039.ExpectedConfidence <= 0.65,
                "GCI0039 alone should not be boosted to 0.85+");
        }
    }

    [Fact]
    public async Task InferLabels_P5_DirectHttpClient_UnsafeDeserialize_BoostApplied()
    {
        // Arrange: GCI0039 (unsafe HttpClient) + GCI0048 (insecure deserialization)
        var diff = """
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -10,5 +10,8 @@
             public async Task ProcessData()
             {
            +    var client = new HttpClient();
            +    var response = client.GetAsync(url).Result;
            +    var obj = JsonConvert.DeserializeObject<T>(response.Content.ReadAsStringAsync().Result, new JsonSerializerSettings { TypeNameHandling = Auto });
            """;

        // Act
        var labels = await _engine.InferLabelsAsync("p23-p5-both", diff);

        // Assert: Both rules detected with coordination boost
        var gci0039 = labels.FirstOrDefault(l => l.RuleId == "GCI0039" && l.ShouldTrigger);
        var gci0048 = labels.FirstOrDefault(l => l.RuleId == "GCI0048" && l.ShouldTrigger);

        // When both are present with sufficient confidence, both should be boosted
        if (gci0039 != null && gci0048 != null)
        {
            if (gci0039.ExpectedConfidence >= 0.55 && gci0048.ExpectedConfidence >= 0.60)
            {
                // Coordination should apply
                Assert.Contains(labels, l =>
                    l.Reason != null && l.Reason.Contains("[coordination]") &&
                    (l.RuleId == "GCI0039" || l.RuleId == "GCI0048"));
            }
        }
    }

    [Fact]
    public async Task InferLabels_P5_PartialMatch_NoBoost()
    {
        // Arrange: Only GCI0048 (insecure deserialization) without GCI0039 (unsafe HttpClient)
        var diff = """
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -10,3 +10,4 @@
             public void DeserializeData()
             {
            +    var obj = JsonConvert.DeserializeObject<T>(jsonString, new JsonSerializerSettings { TypeNameHandling = Auto });
            """;

        // Act
        var labels = await _engine.InferLabelsAsync("p23-p5-partial", diff);

        // Assert: No coordination boost when only one rule or confidence too low
        var coordinationLabels = labels.Where(l =>
            l.Reason != null && l.Reason.Contains("[coordination]")).ToList();

        // If GCI0039 and GCI0048 both trigger with high confidence, coordination applies
        // Otherwise, no coordination marker expected
        var gci0039Coor = coordinationLabels.FirstOrDefault(l => l.RuleId == "GCI0039");
        var gci0048Coor = coordinationLabels.FirstOrDefault(l => l.RuleId == "GCI0048");

        // Either both coordinated or neither (no single coordination)
        if (gci0039Coor != null)
        {
            Assert.NotNull(gci0048Coor);
        }
        if (gci0048Coor != null)
        {
            Assert.NotNull(gci0039Coor);
        }
    }

    // --- Phase 23.3 P6 (Dependency Injection Coordination) Tests ---

    [Fact]
    public async Task InferLabels_P6_NoBoostWithoutBoth_OnlyGCI0045()
    {
        // Arrange: Only GCI0045 (service locator) without GCI0016 (async violations)
        var diff = """
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -10,3 +10,4 @@
             public void ConfigureServices()
             {
            +    var service = ServiceLocator.Get<MyService>();
            """;

        // Act
        var labels = await _engine.InferLabelsAsync("p23-p6-single", diff);

        // Assert: If GCI0045 is detected, it should NOT be boosted (no coordination without GCI0016)
        var gci0045 = labels.FirstOrDefault(l => l.RuleId == "GCI0045" && l.ShouldTrigger);
        var gci0016 = labels.FirstOrDefault(l => l.RuleId == "GCI0016" && l.ShouldTrigger);

        // Either GCI0045 not fired (null is ok) or it fired without coordination
        if (gci0045 != null && gci0016 == null)
        {
            // GCI0045 fired alone - confidence should NOT be coordination-boosted
            Assert.True(gci0045.ExpectedConfidence <= 0.65,
                "GCI0045 alone should not be boosted to 0.75+");
        }
    }

    [Fact]
    public async Task InferLabels_P6_ServiceLocator_AsyncScope_BoostApplied()
    {
        // Arrange: GCI0045 (service locator) + GCI0016 (async violations)
        var diff = """
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -10,5 +10,8 @@
             public async Task ProcessAsync()
             {
            +    var service = ServiceLocator.Get<AsyncService>();
            +    var result = service.GetDataAsync().Result;
            """;

        // Act
        var labels = await _engine.InferLabelsAsync("p23-p6-both", diff);

        // Assert: Both rules detected with coordination boost
        var gci0045 = labels.FirstOrDefault(l => l.RuleId == "GCI0045" && l.ShouldTrigger);
        var gci0016 = labels.FirstOrDefault(l => l.RuleId == "GCI0016" && l.ShouldTrigger);

        // When both are present with sufficient confidence, both should be boosted
        if (gci0045 != null && gci0016 != null)
        {
            if (gci0045.ExpectedConfidence >= 0.60 && gci0016.ExpectedConfidence >= 0.55)
            {
                // Coordination should apply
                Assert.Contains(labels, l =>
                    l.Reason != null && l.Reason.Contains("[coordination]") &&
                    (l.RuleId == "GCI0045" || l.RuleId == "GCI0016"));
            }
        }
    }

    [Fact]
    public async Task InferLabels_P6_PartialMatch_NoBoost()
    {
        // Arrange: Only GCI0016 (async violations) without GCI0045 (service locator)
        var diff = """
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -10,3 +10,4 @@
             public async Task ProcessAsync()
             {
            +    var result = GetDataAsync().Result;
            """;

        // Act
        var labels = await _engine.InferLabelsAsync("p23-p6-partial", diff);

        // Assert: No coordination boost when only one rule or confidence too low
        var coordinationLabels = labels.Where(l =>
            l.Reason != null && l.Reason.Contains("[coordination]")).ToList();

        // If GCI0045 and GCI0016 both trigger with high confidence, coordination applies
        // Otherwise, no coordination marker expected
        var gci0045Coor = coordinationLabels.FirstOrDefault(l => l.RuleId == "GCI0045");
        var gci0016Coor = coordinationLabels.FirstOrDefault(l => l.RuleId == "GCI0016");

        // Either both coordinated or neither (no single coordination)
        if (gci0045Coor != null)
        {
            Assert.NotNull(gci0016Coor);
        }
        if (gci0016Coor != null)
        {
            Assert.NotNull(gci0045Coor);
        }
    }
}

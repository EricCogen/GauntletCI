// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Corpus.Models;
using GauntletCI.Corpus.Normalization;
using GauntletCI.Corpus.Runners;
using GauntletCI.Corpus.Storage;
using Microsoft.Data.Sqlite;

namespace GauntletCI.Tests.Corpus;

// ─────────────────────────────────────────────────────────────────────────────
// 1. FixtureIdHelper
// ─────────────────────────────────────────────────────────────────────────────

public class FixtureIdHelperTests
{
    [Theory]
    [InlineData("torvalds", "linux",          4321, "torvalds_linux_pr4321")]
    [InlineData("MyOrg",    "MyRepo",         1,    "myorg_myrepo_pr1")]
    [InlineData("owner",    "repo/sub",       99,   "owner_repo_sub_pr99")]
    [InlineData("owner",    "repo with space", 5,   "owner_repo-with-space_pr5")]
    public void Build_ReturnsExpectedFixtureId(string owner, string repo, int prNumber, string expected)
    {
        var result = FixtureIdHelper.Build(owner, repo, prNumber);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(FixtureTier.Discovery, "owner_repo_pr1", "discovery/owner_repo_pr1")]
    [InlineData(FixtureTier.Silver,    "owner_repo_pr1", "silver/owner_repo_pr1")]
    [InlineData(FixtureTier.Gold,      "owner_repo_pr1", "gold/owner_repo_pr1")]
    public void GetFixturePath_ReturnsCorrectPathForEachTier(
        FixtureTier tier, string fixtureId, string expectedSuffix)
    {
        var result = FixtureIdHelper.GetFixturePath("/base", tier, fixtureId);
        Assert.EndsWith(expectedSuffix, result.Replace('\\', '/'));
    }

    [Fact]
    public void GetRawPath_AppendsRawSegment()
    {
        var fixturePath = Path.Combine("base", "discovery", "fixture1");
        var rawPath     = FixtureIdHelper.GetRawPath(fixturePath);
        Assert.Equal(Path.Combine(fixturePath, "raw"), rawPath);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 2. PrSizeBucketClassifier
// ─────────────────────────────────────────────────────────────────────────────

public class PrSizeBucketClassifierTests
{
    [Theory]
    [InlineData(0,   PrSizeBucket.Tiny)]   // lower boundary
    [InlineData(1,   PrSizeBucket.Tiny)]
    [InlineData(2,   PrSizeBucket.Tiny)]   // upper boundary (<= 2)
    [InlineData(3,   PrSizeBucket.Small)]  // lower boundary
    [InlineData(7,   PrSizeBucket.Small)]  // upper boundary (<= 7)
    [InlineData(8,   PrSizeBucket.Medium)] // lower boundary
    [InlineData(20,  PrSizeBucket.Medium)] // upper boundary (<= 20)
    [InlineData(21,  PrSizeBucket.Large)]  // lower boundary
    [InlineData(75,  PrSizeBucket.Large)]  // upper boundary (<= 75)
    [InlineData(76,  PrSizeBucket.Huge)]   // lower boundary
    [InlineData(500, PrSizeBucket.Huge)]
    public void Classify_ReturnsCorrectBucket(int filesChanged, PrSizeBucket expected)
    {
        var result = PrSizeBucketClassifier.Classify(filesChanged);
        Assert.Equal(expected, result);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 3. TestFileClassifier
// ─────────────────────────────────────────────────────────────────────────────

public class TestFileClassifierTests
{
    [Theory]
    [InlineData("src/GauntletCI.Tests/SomeTests.cs")]   // project-name hint: ends with .Tests
    [InlineData("MyApp.Tests/FooTests.cs")]             // project-name hint: ends with .Tests
    [InlineData("tests/integration/BarTest.cs")]        // project-name hint: segment == "tests"
    [InlineData("test/unit/BazTest.cs")]                // project-name hint: segment == "test"
    [InlineData("src/Foo.Tests.cs")]                    // name suffix: .tests.cs
    [InlineData("src/FooTests.cs")]                     // name suffix: tests.cs
    [InlineData("src/FooTest.cs")]                      // name suffix: test.cs
    [InlineData("src/Foo.Test.cs")]                     // name suffix: .test.cs
    public void IsTestFile_DetectsTestFiles(string path)
    {
        Assert.True(TestFileClassifier.IsTestFile(path));
    }

    [Theory]
    [InlineData("src/MyService.cs")]
    [InlineData("src/Controllers/HomeController.cs")]
    [InlineData("lib/utils.js")]
    [InlineData("src/GauntletCI.Core/Rules/GCI0001.cs")]
    public void IsTestFile_DoesNotMisclassifyProductionFiles(string path)
    {
        Assert.False(TestFileClassifier.IsTestFile(path));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void IsTestFile_ReturnsFalse_ForNullOrWhitespace(string path)
    {
        Assert.False(TestFileClassifier.IsTestFile(path));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 4. FixtureFolderStore (integration — real temp dir + real CorpusDb)
// ─────────────────────────────────────────────────────────────────────────────

public sealed class FixtureFolderStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly CorpusDb _db;
    private readonly FixtureFolderStore _store;

    public FixtureFolderStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _db    = new CorpusDb(Path.Combine(_tempDir, "corpus.db"));
        _db.InitializeAsync().GetAwaiter().GetResult();
        _store = new FixtureFolderStore(_db, _tempDir);
    }

    public void Dispose()
    {
        _db.Dispose();
        SqliteConnection.ClearAllPools();
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); }
        catch (IOException) { /* best-effort cleanup on Windows WAL lock */ }
    }

    private static FixtureMetadata MakeMetadata(
        string fixtureId = "testowner_testrepo_pr1",
        FixtureTier tier  = FixtureTier.Discovery) =>
        new()
        {
            FixtureId         = fixtureId,
            Tier              = tier,
            Repo              = "testowner/testrepo",
            PullRequestNumber = 1,
            Language          = "C#",
            RuleIds           = [],
            Tags              = [],
            PrSizeBucket      = PrSizeBucket.Tiny,
            FilesChanged      = 1,
            HasTestsChanged   = false,
            HasReviewComments = false,
            BaseSha           = "abc123",
            HeadSha           = "def456",
            Source            = "test",
            CreatedAtUtc      = DateTime.UtcNow,
        };

    [Fact]
    public async Task SaveMetadataAsync_WritesMetadataJson()
    {
        await _store.SaveMetadataAsync(MakeMetadata());

        var metaPath = Path.Combine(_tempDir, "discovery", "testowner_testrepo_pr1", "metadata.json");
        Assert.True(File.Exists(metaPath));
    }

    [Fact]
    public async Task SaveMetadataAsync_CreatesNotesTemplate()
    {
        await _store.SaveMetadataAsync(MakeMetadata());

        var notesPath = Path.Combine(_tempDir, "discovery", "testowner_testrepo_pr1", "notes.md");
        Assert.True(File.Exists(notesPath));
    }

    [Fact]
    public async Task GetMetadataAsync_RoundTripsCorrectly()
    {
        var metadata = MakeMetadata();
        await _store.SaveMetadataAsync(metadata);

        var result = await _store.GetMetadataAsync("testowner_testrepo_pr1");

        Assert.NotNull(result);
        Assert.Equal(metadata.FixtureId, result.FixtureId);
        Assert.Equal(metadata.Tier, result.Tier);
        Assert.Equal(metadata.Repo, result.Repo);
        Assert.Equal(metadata.PullRequestNumber, result.PullRequestNumber);
    }

    [Fact]
    public async Task SaveExpectedFindingsAsync_WritesExpectedJson()
    {
        await _store.SaveMetadataAsync(MakeMetadata());

        var findings = new List<ExpectedFinding>
        {
            new() { RuleId = "GCI0001", ShouldTrigger = true, Reason = "test" },
        };
        await _store.SaveExpectedFindingsAsync("testowner_testrepo_pr1", findings);

        var expectedPath = Path.Combine(_tempDir, "discovery", "testowner_testrepo_pr1", "expected.json");
        Assert.True(File.Exists(expectedPath));
    }

    [Fact]
    public async Task SaveActualFindingsAsync_WritesRunFileAndLatestFile()
    {
        await _store.SaveMetadataAsync(MakeMetadata());

        const string runId   = "run-001";
        var findings = new List<ActualFinding>
        {
            new() { RuleId = "GCI0001", DidTrigger = true },
        };
        await _store.SaveActualFindingsAsync("testowner_testrepo_pr1", runId, findings);

        var fixturePath = Path.Combine(_tempDir, "discovery", "testowner_testrepo_pr1");
        Assert.True(File.Exists(Path.Combine(fixturePath, $"actual.{runId}.json")));
        Assert.True(File.Exists(Path.Combine(fixturePath, "actual.json")));
    }

    [Fact]
    public async Task ListFixturesAsync_ReturnsOnlyRequestedTier()
    {
        await _store.SaveMetadataAsync(MakeMetadata("owner_repo_pr1", FixtureTier.Discovery));
        await _store.SaveMetadataAsync(MakeMetadata("owner_repo_pr2", FixtureTier.Silver));
        await _store.SaveMetadataAsync(MakeMetadata("owner_repo_pr3", FixtureTier.Gold));

        var discoveryFixtures = await _store.ListFixturesAsync(FixtureTier.Discovery);

        Assert.Single(discoveryFixtures);
        Assert.Equal("owner_repo_pr1", discoveryFixtures[0].FixtureId);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 5. NormalizationPipeline
// ─────────────────────────────────────────────────────────────────────────────

public sealed class NormalizationPipelineTests : IDisposable
{
    private readonly string _tempDir;
    private readonly CorpusDb _db;
    private readonly NormalizationPipeline _pipeline;

    public NormalizationPipelineTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _db = new CorpusDb(Path.Combine(_tempDir, "corpus.db"));
        _db.InitializeAsync().GetAwaiter().GetResult();
        _pipeline = new NormalizationPipeline(new FixtureFolderStore(_db, _tempDir));
    }

    public void Dispose()
    {
        _db.Dispose();
        SqliteConnection.ClearAllPools();
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); }
        catch (IOException) { /* best-effort cleanup on Windows WAL lock */ }
    }

    private static HydratedPullRequest MakePr(string owner, string repo, int number, int filesChanged = 1) =>
        new()
        {
            RepoOwner         = owner,
            RepoName          = repo,
            PullRequestNumber = number,
            FilesChangedCount = filesChanged,
            ChangedFiles      = [],
            ReviewComments    = [],
            DiffText          = "",
            HydratedAtUtc     = DateTime.UtcNow,
        };

    [Fact]
    public async Task NormalizeAsync_ProducesCorrectRepoAndPrNumber()
    {
        var metadata = await _pipeline.NormalizeAsync(MakePr("testowner", "testrepo", 42));

        Assert.Equal("testowner/testrepo", metadata.Repo);
        Assert.Equal(42, metadata.PullRequestNumber);
    }

    [Fact]
    public async Task NormalizeAsync_FixtureIdIsFormattedCorrectly()
    {
        var metadata = await _pipeline.NormalizeAsync(MakePr("MyOrg", "MyRepo", 123));

        Assert.Equal("myorg_myrepo_pr123", metadata.FixtureId);
    }

    [Fact]
    public async Task NormalizeAsync_SetsPrSizeBucketCorrectly()
    {
        // filesChanged = 1 → <= 2 → Tiny
        var metadata = await _pipeline.NormalizeAsync(MakePr("owner", "repo", 1, filesChanged: 1));

        Assert.Equal(PrSizeBucket.Tiny, metadata.PrSizeBucket);
    }

    [Fact]
    public async Task NormalizeAsync_TagsListIsNonNull()
    {
        var metadata = await _pipeline.NormalizeAsync(MakePr("owner", "repo", 2));

        Assert.NotNull(metadata.Tags);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 6. RuleCorpusRunner (integration)
// ─────────────────────────────────────────────────────────────────────────────

public sealed class RuleCorpusRunnerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly CorpusDb _db;
    private readonly FixtureFolderStore _store;
    private readonly RuleCorpusRunner _runner;

    // A minimal unified diff that mixes code and a markdown file — triggers GCI0001.
    private const string MixedScopeDiff = """
        diff --git a/src/Foo.cs b/src/Foo.cs
        index abc..def 100644
        --- a/src/Foo.cs
        +++ b/src/Foo.cs
        @@ -1,1 +1,1 @@
        -old
        +new
        diff --git a/README.md b/README.md
        index 111..222 100644
        --- a/README.md
        +++ b/README.md
        @@ -1,1 +1,1 @@
        -old docs
        +new docs
        """;

    public RuleCorpusRunnerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _db     = new CorpusDb(Path.Combine(_tempDir, "corpus.db"));
        _db.InitializeAsync().GetAwaiter().GetResult();
        _store  = new FixtureFolderStore(_db, _tempDir);
        _runner = new RuleCorpusRunner(_store, _db);
    }

    public void Dispose()
    {
        _db.Dispose();
        SqliteConnection.ClearAllPools();
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); }
        catch (IOException) { /* best-effort cleanup on Windows WAL lock */ }
    }

    private static FixtureMetadata MinimalMetadata(string fixtureId) =>
        new()
        {
            FixtureId         = fixtureId,
            Tier              = FixtureTier.Discovery,
            Repo              = "owner/repo",
            PullRequestNumber = 1,
            Language          = "C#",
            RuleIds           = [],
            Tags              = [],
            PrSizeBucket      = PrSizeBucket.Tiny,
            FilesChanged      = 1,
            HasTestsChanged   = false,
            HasReviewComments = false,
            Source            = "test",
            CreatedAtUtc      = DateTime.UtcNow,
        };

    [Fact]
    public async Task RunAsync_WithKnownTriggerDiff_ReturnsAtLeastOneFinding()
    {
        const string fixtureId = "owner_repo_pr1";
        await _store.SaveMetadataAsync(MinimalMetadata(fixtureId));

        var findings = await _runner.RunAsync(fixtureId, MixedScopeDiff);

        Assert.NotEmpty(findings);
        Assert.Contains(findings, f => f.DidTrigger);
    }

    [Fact]
    public async Task RunAsync_WithEmptyDiff_ReturnsEmptyOrAllFalseFindings()
    {
        const string fixtureId = "owner_repo_pr2";
        await _store.SaveMetadataAsync(MinimalMetadata(fixtureId));

        var findings = await _runner.RunAsync(fixtureId, "");

        Assert.True(findings.Count == 0 || findings.All(f => !f.DidTrigger));
    }

    [Fact]
    public async Task RunAsync_WritesActualJsonFileToFixtureFolder()
    {
        const string fixtureId = "owner_repo_pr3";
        await _store.SaveMetadataAsync(MinimalMetadata(fixtureId));

        await _runner.RunAsync(fixtureId, MixedScopeDiff);

        var actualPath = Path.Combine(_tempDir, "discovery", fixtureId, "actual.json");
        Assert.True(File.Exists(actualPath));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 7. Candidate deduplication (INSERT OR IGNORE via CorpusDb)
// ─────────────────────────────────────────────────────────────────────────────

public sealed class CandidateDeduplicationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly CorpusDb _db;

    public CandidateDeduplicationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _db = new CorpusDb(Path.Combine(_tempDir, "corpus.db"));
        _db.InitializeAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _db.Dispose();
        SqliteConnection.ClearAllPools();
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); }
        catch (IOException) { /* best-effort cleanup on Windows WAL lock */ }
    }

    [Fact]
    public async Task InsertOrIgnore_DuplicateCandidate_OnlyOneRowInserted()
    {
        const string insertSql = """
            INSERT OR IGNORE INTO candidates
                (id, source, repo_owner, repo_name, pr_number, url, discovered_at_utc)
            VALUES ($id, 'test', 'owner', 'repo', 1, 'https://example.com', datetime('now'))
            """;

        using var cmd1 = _db.Connection.CreateCommand();
        cmd1.CommandText = insertSql;
        cmd1.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        await cmd1.ExecuteNonQueryAsync();

        // Same (repo_owner, repo_name, pr_number), different primary key — should be ignored.
        using var cmd2 = _db.Connection.CreateCommand();
        cmd2.CommandText = insertSql;
        cmd2.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        await cmd2.ExecuteNonQueryAsync();

        using var countCmd = _db.Connection.CreateCommand();
        countCmd.CommandText =
            "SELECT COUNT(*) FROM candidates WHERE repo_owner='owner' AND repo_name='repo' AND pr_number=1";
        var count = (long)(await countCmd.ExecuteScalarAsync())!;

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task InsertOrIgnore_DistinctCandidates_AllRowsInserted()
    {
        const string insertSql = """
            INSERT OR IGNORE INTO candidates
                (id, source, repo_owner, repo_name, pr_number, url, discovered_at_utc)
            VALUES ($id, 'test', $owner, $repo, $pr, 'https://example.com', datetime('now'))
            """;

        foreach (var (owner, repo, pr) in new[] { ("a", "r", 1), ("a", "r", 2), ("b", "r", 1) })
        {
            using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = insertSql;
            cmd.Parameters.AddWithValue("$id",    Guid.NewGuid().ToString());
            cmd.Parameters.AddWithValue("$owner", owner);
            cmd.Parameters.AddWithValue("$repo",  repo);
            cmd.Parameters.AddWithValue("$pr",    pr);
            await cmd.ExecuteNonQueryAsync();
        }

        using var countCmd = _db.Connection.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM candidates";
        var count = (long)(await countCmd.ExecuteScalarAsync())!;

        Assert.Equal(3, count);
    }
}

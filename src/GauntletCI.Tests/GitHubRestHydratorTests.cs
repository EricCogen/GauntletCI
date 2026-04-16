// SPDX-License-Identifier: Elastic-2.0
using System.Net;
using GauntletCI.Corpus.Hydration;
using GauntletCI.Corpus.Storage;

namespace GauntletCI.Tests;

public class GitHubRestHydratorTests : IDisposable
{
    private readonly string _tempDir =
        Path.Combine(Path.GetTempPath(), $"gauntlet-tests-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ── Hand-rolled HTTP double ───────────────────────────────────────────────

    private sealed class FakeHttpHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public FakeHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
            => _respond = respond;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_respond(request));
    }

    private static HttpResponseMessage Ok(string json)
        => new(HttpStatusCode.OK) { Content = new StringContent(json) };

    // ── ParsePrUrl ────────────────────────────────────────────────────────────

    [Fact]
    public void ParsePrUrl_ValidUrl_ExtractsOwnerRepoNumber()
    {
        // Arrange
        const string url = "https://github.com/owner/repo/pull/1234";

        // Act
        var (owner, repo, prNumber) = GitHubRestHydrator.ParsePrUrl(url);

        // Assert
        Assert.Equal("owner", owner);
        Assert.Equal("repo", repo);
        Assert.Equal(1234, prNumber);
    }

    [Fact]
    public void ParsePrUrl_UrlWithTrailingSlash_Works()
    {
        // Arrange
        const string url = "https://github.com/owner/repo/pull/42/";

        // Act
        var (owner, repo, prNumber) = GitHubRestHydrator.ParsePrUrl(url);

        // Assert
        Assert.Equal("owner", owner);
        Assert.Equal("repo", repo);
        Assert.Equal(42, prNumber);
    }

    [Fact]
    public void ParsePrUrl_UrlWithLeadingWhitespace_Works()
    {
        // Arrange — leading space should be trimmed
        const string url = " https://github.com/owner/repo/pull/1";

        // Act
        var (owner, repo, prNumber) = GitHubRestHydrator.ParsePrUrl(url);

        // Assert
        Assert.Equal("owner", owner);
        Assert.Equal("repo", repo);
        Assert.Equal(1, prNumber);
    }

    [Fact]
    public void ParsePrUrl_InvalidUrl_NoSegments_Throws()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentException>(
            () => GitHubRestHydrator.ParsePrUrl("https://github.com"));
    }

    [Fact]
    public void ParsePrUrl_InvalidUrl_NotAPull_Throws()
    {
        // Arrange — "issues" is not "pull"
        const string url = "https://github.com/owner/repo/issues/1";

        // Act / Assert
        Assert.Throws<ArgumentException>(
            () => GitHubRestHydrator.ParsePrUrl(url));
    }

    [Fact]
    public void ParsePrUrl_NonNumericPrNumber_Throws()
    {
        // Arrange
        const string url = "https://github.com/owner/repo/pull/abc";

        // Act / Assert
        Assert.Throws<ArgumentException>(
            () => GitHubRestHydrator.ParsePrUrl(url));
    }

    // ── HydrateFromUrlAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task HydrateFromUrlAsync_InvalidUrl_ThrowsArgumentException()
    {
        // Arrange
        using var handler = new FakeHttpHandler(_ => Ok("{}"));
        using var http    = new HttpClient(handler);
        var store         = new RawSnapshotStore(_tempDir);
        using var sut     = new GitHubRestHydrator(http, store);

        // Act / Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => sut.HydrateFromUrlAsync("https://github.com/owner/repo/issues/99"));
    }

    [Fact]
    public async Task HydrateFromUrlAsync_ValidUrlWithFakeHttp_ReturnsMappedPullRequest()
    {
        // Arrange
        const string prJson =
            """{"title":"Test PR","body":"Closes #1","state":"merged","base":{"sha":"abc"},"head":{"sha":"def"},"merge_commit_sha":"ghi","changed_files":2,"additions":10,"deletions":5}""";
        const string filesJson    = "[]";
        const string commentsJson = "[]";
        const string commitsJson  = """[{"sha":"def123"}]""";
        const string diffText     = "+++ some diff text";

        using var handler = new FakeHttpHandler(req =>
        {
            var path   = req.RequestUri!.PathAndQuery;
            var accept = req.Headers.Accept.ToString();

            if (accept.Contains("diff"))        return Ok(diffText);
            if (path.Contains("/files"))        return Ok(filesJson);
            if (path.Contains("/comments"))     return Ok(commentsJson);
            if (path.Contains("/commits"))      return Ok(commitsJson);
            return Ok(prJson);
        });

        using var http = new HttpClient(handler);
        http.DefaultRequestHeaders.Add("User-Agent", "GauntletCI-Test");

        var store    = new RawSnapshotStore(_tempDir);
        using var sut = new GitHubRestHydrator(http, store);

        // Act
        var result = await sut.HydrateFromUrlAsync(
            "https://github.com/owner/repo/pull/1234");

        // Assert
        Assert.Equal("owner", result.RepoOwner);
        Assert.Equal("repo",  result.RepoName);
        Assert.Equal(1234,    result.PullRequestNumber);
        Assert.Equal("Test PR", result.Title);
        Assert.Equal("abc", result.BaseSha);
        Assert.Equal("def", result.HeadSha);
        Assert.Equal("ghi", result.MergeCommitSha);
        Assert.Equal(2,  result.FilesChangedCount);
        Assert.Equal(10, result.Additions);
        Assert.Equal(5,  result.Deletions);
        Assert.Equal(diffText, result.DiffText);
        var commit = Assert.Single(result.Commits);
        Assert.Equal("def123", commit);
    }

    [Fact]
    public async Task GetPermanentRepoRejectReasonAsync_NotFoundRepo_ReturnsRejectReason()
    {
        using var handler = new FakeHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var http = new HttpClient(handler);
        http.DefaultRequestHeaders.Add("User-Agent", "GauntletCI-Test");

        var store = new RawSnapshotStore(_tempDir);
        using var sut = new GitHubRestHydrator(http, store);

        var result = await sut.GetPermanentRepoRejectReasonAsync("missing", "repo");

        Assert.Equal("repo not found (deleted, private, or never existed)", result);
    }

    [Fact]
    public async Task GetPermanentRepoRejectReasonAsync_ArchivedRepo_ReturnsRejectReason()
    {
        using var handler = new FakeHttpHandler(_ => Ok("""{"full_name":"owner/repo","archived":true}"""));
        using var http = new HttpClient(handler);
        http.DefaultRequestHeaders.Add("User-Agent", "GauntletCI-Test");

        var store = new RawSnapshotStore(_tempDir);
        using var sut = new GitHubRestHydrator(http, store);

        var result = await sut.GetPermanentRepoRejectReasonAsync("owner", "repo");

        Assert.Equal("repo is archived", result);
    }

    [Fact]
    public async Task GetPermanentRepoRejectReasonAsync_RenamedRepo_ReturnsRejectReason()
    {
        using var handler = new FakeHttpHandler(_ => Ok("""{"full_name":"new-owner/new-repo","archived":false}"""));
        using var http = new HttpClient(handler);
        http.DefaultRequestHeaders.Add("User-Agent", "GauntletCI-Test");

        var store = new RawSnapshotStore(_tempDir);
        using var sut = new GitHubRestHydrator(http, store);

        var result = await sut.GetPermanentRepoRejectReasonAsync("old-owner", "old-repo");

        Assert.Equal("repo has moved permanently (update allowlist with new owner/name)", result);
    }

    [Fact]
    public async Task GetPermanentRepoRejectReasonAsync_HealthyRepo_ReturnsNull()
    {
        using var handler = new FakeHttpHandler(_ => Ok("""{"full_name":"owner/repo","archived":false}"""));
        using var http = new HttpClient(handler);
        http.DefaultRequestHeaders.Add("User-Agent", "GauntletCI-Test");

        var store = new RawSnapshotStore(_tempDir);
        using var sut = new GitHubRestHydrator(http, store);

        var result = await sut.GetPermanentRepoRejectReasonAsync("owner", "repo");

        Assert.Null(result);
    }
}

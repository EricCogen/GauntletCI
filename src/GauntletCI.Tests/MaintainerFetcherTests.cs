// SPDX-License-Identifier: Elastic-2.0
using System.Net;
using System.Net.Http;
using System.Text.Json;
using GauntletCI.Corpus.MaintainerFetcher;
using Xunit;

namespace GauntletCI.Tests;

public sealed class MaintainerFetcherTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static MaintainerFetcher BuildFetcher(FakeHttpHandler handler)
    {
        var http = new HttpClient(handler);
        http.DefaultRequestHeaders.Add("User-Agent", "GauntletCI-Test/1.0");
        return new MaintainerFetcher(http, ownsHttpClient: true);
    }

    // ── GetTopContributorLoginsAsync ──────────────────────────────────────────

    [Fact]
    public async Task GetTopContributorLogins_ReturnsMappedLogins()
    {
        // 10 contributors — top 5% = ceil(0.5) = 1, but MinTopCount=10, so we get all 10
        var json = MakeContributors(10);
        var handler = new FakeHttpHandler(url => url.Contains("/contributors")
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) }
            : NotFound());

        using var fetcher = BuildFetcher(handler);
        var logins = await fetcher.GetTopContributorLoginsAsync("dotnet", "runtime", default);

        Assert.Equal(10, logins.Count);
        Assert.Equal("user0", logins[0]);
    }

    [Fact]
    public async Task GetTopContributorLogins_TakesTopPercentileForLargeRepos()
    {
        // 200 contributors — top 5% = 10 = MinTopCount, so exactly 10 (or 200*0.05=10)
        var json = MakeContributors(200);
        var handler = new FakeHttpHandler(url => url.Contains("/contributors")
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) }
            : NotFound());

        using var fetcher = BuildFetcher(handler);
        var logins = await fetcher.GetTopContributorLoginsAsync("dotnet", "runtime", default);

        Assert.Equal(10, logins.Count); // max(10, ceil(200*0.05)) = max(10,10) = 10
    }

    [Fact]
    public async Task GetTopContributorLogins_LargeRepo_TakesPercentileWhenGreaterThanMin()
    {
        // 400 contributors — top 5% = 20 > MinTopCount(10), so we get 20
        var json = MakeContributors(400);
        var handler = new FakeHttpHandler(url => url.Contains("/contributors")
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) }
            : NotFound());

        using var fetcher = BuildFetcher(handler);
        var logins = await fetcher.GetTopContributorLoginsAsync("dotnet", "runtime", default);

        Assert.Equal(20, logins.Count);
    }

    // ── SearchItemsAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task SearchItems_FiltersByTopContributors()
    {
        var topLogins = new[] { "alice", "bob" };
        var searchJson = MakeSearchResponse([
            MakeItem(1, "alice", isPr: true),
            MakeItem(2, "charlie", isPr: true),  // NOT a top contributor
            MakeItem(3, "bob", isPr: false),
        ]);

        var handler = new FakeHttpHandler(url => url.Contains("/search/issues")
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(searchJson) }
            : NotFound());

        using var fetcher = BuildFetcher(handler);
        var results = await fetcher.SearchItemsAsync("dotnet", "runtime", "pr", "performance", topLogins, 100, default);

        // charlie is filtered out
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Contains(r.Author, topLogins));
    }

    [Fact]
    public async Task SearchItems_SetsTypeCorrectly()
    {
        var topLogins = new[] { "alice" };
        var searchJson = MakeSearchResponse([
            MakeItem(1, "alice", isPr: true),
            MakeItem(2, "alice", isPr: false),
        ]);

        var handler = new FakeHttpHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(searchJson) });

        using var fetcher = BuildFetcher(handler);
        var results = await fetcher.SearchItemsAsync("dotnet", "runtime", "pr", "performance", topLogins, 100, default);

        Assert.Contains(results, r => r.Type == "pr");
        Assert.Contains(results, r => r.Type == "issue");
    }

    // ── FetchAsync (integration) ──────────────────────────────────────────────

    [Fact]
    public async Task FetchAsync_DeduplicatesAcrossLabels()
    {
        // Same PR found via two different labels → should appear once
        var contributorsJson = MakeContributors(10);
        var item = MakeItem(42, "user0", isPr: true, reactions: 5);
        var searchJson = MakeSearchResponse([item]);

        var handler = new FakeHttpHandler(url =>
        {
            if (url.Contains("/contributors"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(contributorsJson) };
            if (url.Contains("/search/issues"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(searchJson) };
            return NotFound();
        });

        using var fetcher = BuildFetcher(handler);
        var targets = new[] { new MaintainerTarget("dotnet", "runtime", ["performance", "design-discussion"]) };
        var results = await fetcher.FetchAsync(targets, 100, default);

        // PR #42 found in both "performance" and "design-discussion" searches — deduplicated to 1
        Assert.Single(results);
        Assert.Equal(42, results[0].Number);
    }

    [Fact]
    public async Task FetchAsync_SortsResultsByReactionsDescending()
    {
        var contributorsJson = MakeContributors(10);
        var searchJson = MakeSearchResponse([
            MakeItem(1, "user0", isPr: true, reactions: 3),
            MakeItem(2, "user1", isPr: true, reactions: 99),
            MakeItem(3, "user2", isPr: false, reactions: 7),
        ]);

        var handler = new FakeHttpHandler(url =>
        {
            if (url.Contains("/contributors"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(contributorsJson) };
            if (url.Contains("/search/issues"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(searchJson) };
            return NotFound();
        });

        using var fetcher = BuildFetcher(handler);
        var targets = new[] { new MaintainerTarget("dotnet", "runtime", ["performance"]) };
        var results = await fetcher.FetchAsync(targets, 100, default);

        Assert.Equal(99, results[0].Reactions);
        Assert.Equal(3,  results[^1].Reactions);
    }

    [Fact]
    public async Task FetchAsync_EmptyContributors_ReturnsEmptyResults()
    {
        var handler = new FakeHttpHandler(url =>
        {
            if (url.Contains("/contributors"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") };
            return NotFound();
        });

        using var fetcher = BuildFetcher(handler);
        var targets = new[] { new MaintainerTarget("dotnet", "runtime", ["performance"]) };
        var results = await fetcher.FetchAsync(targets, 100, default);

        Assert.Empty(results);
    }

    // ── JSON builders ─────────────────────────────────────────────────────────

    private static string MakeContributors(int count)
    {
        var items = Enumerable.Range(0, count)
            .Select(i => $"{{\"login\":\"user{i}\",\"contributions\":{1000 - i}}}");
        return "[" + string.Join(",", items) + "]";
    }

    private static string MakeSearchResponse(IEnumerable<string> itemJsons) =>
        $"{{\"total_count\":{itemJsons.Count()},\"incomplete_results\":false,\"items\":[{string.Join(",", itemJsons)}]}}";

    private static string MakeItem(int number, string login, bool isPr, int reactions = 1)
    {
        var prRef = isPr ? ",\"pull_request\":{\"url\":\"https://api.github.com/fake\"}" : "";
        return $@"{{
            ""number"":{number},
            ""title"":""Fix performance issue #{number}"",
            ""body"":""Details for #{number}"",
            ""html_url"":""https://github.com/dotnet/runtime/issues/{number}"",
            ""created_at"":""2024-01-15T10:00:00Z"",
            ""user"":{{""login"":""{login}""}},
            ""labels"":[{{""name"":""performance""}}],
            ""reactions"":{{""total_count"":{reactions}}}{prRef}
        }}";
    }

    private static HttpResponseMessage NotFound() =>
        new(HttpStatusCode.NotFound) { Content = new StringContent("{}") };

    // ── Fake handler ──────────────────────────────────────────────────────────

    private sealed class FakeHttpHandler(Func<string, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(responder(request.RequestUri?.ToString() ?? ""));
    }
}

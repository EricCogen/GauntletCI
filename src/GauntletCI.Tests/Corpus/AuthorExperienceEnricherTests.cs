// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Corpus.Labeling;
using Xunit;

namespace GauntletCI.Tests.Corpus;

public sealed class AuthorExperienceEnricherTests
{
    // ── ClassifyExperienceTier ────────────────────────────────────────────────

    [Fact]
    public void ClassifyExperienceTier_Zero_ReturnsNone()
    {
        Assert.Equal("none", AuthorExperienceEnricher.ClassifyExperienceTier(0));
    }

    [Fact]
    public void ClassifyExperienceTier_Three_ReturnsLow()
    {
        Assert.Equal("low", AuthorExperienceEnricher.ClassifyExperienceTier(3));
    }

    [Fact]
    public void ClassifyExperienceTier_TwentyFive_ReturnsMedium()
    {
        Assert.Equal("medium", AuthorExperienceEnricher.ClassifyExperienceTier(25));
    }

    [Fact]
    public void ClassifyExperienceTier_TwoHundred_ReturnsHigh()
    {
        Assert.Equal("high", AuthorExperienceEnricher.ClassifyExperienceTier(200));
    }

    [Fact]
    public void ClassifyExperienceTier_BoundaryFive_ReturnsLow()
    {
        Assert.Equal("low", AuthorExperienceEnricher.ClassifyExperienceTier(5));
    }

    [Fact]
    public void ClassifyExperienceTier_BoundarySix_ReturnsMedium()
    {
        Assert.Equal("medium", AuthorExperienceEnricher.ClassifyExperienceTier(6));
    }

    [Fact]
    public void ClassifyExperienceTier_BoundaryFifty_ReturnsMedium()
    {
        Assert.Equal("medium", AuthorExperienceEnricher.ClassifyExperienceTier(50));
    }

    [Fact]
    public void ClassifyExperienceTier_BoundaryFiftyOne_ReturnsHigh()
    {
        Assert.Equal("high", AuthorExperienceEnricher.ClassifyExperienceTier(51));
    }

    // ── Commit count cap ──────────────────────────────────────────────────────

    // Note: The cap (1000) is applied in FetchCommitCountAsync.
    // We verify that ClassifyExperienceTier handles values > 1000 as "high".
    [Fact]
    public void ClassifyExperienceTier_AboveThousand_ReturnsHigh()
    {
        // Even if somehow above cap, tier should be high
        Assert.Equal("high", AuthorExperienceEnricher.ClassifyExperienceTier(1500));
    }

    // ── ParseLastPage ─────────────────────────────────────────────────────────

    [Fact]
    public void ParseLastPage_NullHeader_ReturnsZero()
    {
        Assert.Equal(0, AuthorExperienceEnricher.ParseLastPage(null));
    }

    [Fact]
    public void ParseLastPage_EmptyHeader_ReturnsZero()
    {
        Assert.Equal(0, AuthorExperienceEnricher.ParseLastPage(""));
    }

    [Fact]
    public void ParseLastPage_ValidLinkHeader_ReturnsLastPage()
    {
        var header = "<https://api.github.com/repos/owner/repo/commits?page=2&per_page=1>; rel=\"next\", " +
                     "<https://api.github.com/repos/owner/repo/commits?page=42&per_page=1>; rel=\"last\"";
        Assert.Equal(42, AuthorExperienceEnricher.ParseLastPage(header));
    }

    [Fact]
    public void ParseLastPage_OnlyNextRel_ReturnsZero()
    {
        var header = "<https://api.github.com/repos/owner/repo/commits?page=2>; rel=\"next\"";
        Assert.Equal(0, AuthorExperienceEnricher.ParseLastPage(header));
    }

    [Fact]
    public void ParseLastPage_SinglePage_ReturnsZero()
    {
        // No Link header means only one page; ParseLastPage returns 0
        Assert.Equal(0, AuthorExperienceEnricher.ParseLastPage(null));
    }
}

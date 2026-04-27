// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Corpus.Labeling;
using Xunit;

namespace GauntletCI.Tests.Corpus;

public sealed class PRDescriptionEnricherTests
{
    // ── HasLinkedIssue ─────────────────────────────────────────────────────────

    [Fact]
    public void HasLinkedIssue_FixesHash_ReturnsTrue()
    {
        Assert.True(PRDescriptionEnricher.HasLinkedIssue("Fixes #123 and more text"));
    }

    [Fact]
    public void HasLinkedIssue_ClosesUrl_ReturnsTrue()
    {
        Assert.True(PRDescriptionEnricher.HasLinkedIssue(
            "Closes https://github.com/owner/repo/issues/45"));
    }

    [Fact]
    public void HasLinkedIssue_ResolvesHash_ReturnsTrue()
    {
        Assert.True(PRDescriptionEnricher.HasLinkedIssue("Resolves #99 - fixes the thing"));
    }

    [Fact]
    public void HasLinkedIssue_NullBody_ReturnsFalse()
    {
        Assert.False(PRDescriptionEnricher.HasLinkedIssue(null));
    }

    [Fact]
    public void HasLinkedIssue_NoPattern_ReturnsFalse()
    {
        Assert.False(PRDescriptionEnricher.HasLinkedIssue("This is a normal PR description without issue links"));
    }

    // ── IsBodyEmpty ───────────────────────────────────────────────────────────

    [Fact]
    public void IsBodyEmpty_NullBody_ReturnsTrue()
    {
        Assert.True(PRDescriptionEnricher.IsBodyEmpty(null));
    }

    [Fact]
    public void IsBodyEmpty_ShortBody_ReturnsTrue()
    {
        // 25 chars is under the 30-char threshold
        Assert.True(PRDescriptionEnricher.IsBodyEmpty("Short body under 30 chars!!"));
    }

    [Fact]
    public void IsBodyEmpty_LongEnoughBody_ReturnsFalse()
    {
        // 100 chars should pass the threshold
        var body = new string('x', 100);
        Assert.False(PRDescriptionEnricher.IsBodyEmpty(body));
    }

    // ── HasWipKeywords ────────────────────────────────────────────────────────

    [Fact]
    public void HasWipKeywords_WipInTitle_ReturnsTrue()
    {
        Assert.True(PRDescriptionEnricher.HasWipKeywords("WIP: fix something", null));
    }

    [Fact]
    public void HasWipKeywords_DraftKeyword_ReturnsTrue()
    {
        Assert.True(PRDescriptionEnricher.HasWipKeywords("Add new feature", "draft implementation"));
    }

    [Fact]
    public void HasWipKeywords_NoWipContent_ReturnsFalse()
    {
        Assert.False(PRDescriptionEnricher.HasWipKeywords(
            "Implement user auth flow",
            "This PR adds JWT authentication to the login endpoint."));
    }

    // ── combined scenario ─────────────────────────────────────────────────────

    [Fact]
    public void AllFalse_LongBodyNoLinkedIssueNoWip()
    {
        var body = "This PR adds a new feature to improve performance of the query engine by caching results.";
        Assert.False(PRDescriptionEnricher.IsBodyEmpty(body));
        Assert.False(PRDescriptionEnricher.HasLinkedIssue(body));
        Assert.False(PRDescriptionEnricher.HasWipKeywords("Add caching", body));
    }
}

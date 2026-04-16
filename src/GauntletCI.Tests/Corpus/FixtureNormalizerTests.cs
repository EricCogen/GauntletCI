// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Corpus.Models;
using GauntletCI.Corpus.Normalization;
using GauntletCI.Corpus.Storage;

namespace GauntletCI.Tests.Corpus;

public class FixtureNormalizerTests
{
    private static HydratedPullRequest MakePr(
        string owner = "acme", string repo = "api", int prNumber = 1,
        string diff = "", IReadOnlyList<ChangedFile>? files = null,
        IReadOnlyList<ReviewComment>? comments = null) => new()
    {
        RepoOwner = owner,
        RepoName = repo,
        PullRequestNumber = prNumber,
        DiffText = diff,
        ChangedFiles = files ?? [],
        ReviewComments = comments ?? [],
        HydratedAtUtc = DateTime.UtcNow,
    };

    [Fact]
    public void Normalize_BuildsCorrectFixtureId()
    {
        var pr = MakePr(owner: "acme", repo: "api", prNumber: 42);
        var result = FixtureNormalizer.Normalize(pr);
        Assert.Equal("acme_api_pr42", result.FixtureId);
    }

    [Fact]
    public void Normalize_SetsRepo()
    {
        var pr = MakePr(owner: "acme", repo: "api");
        var result = FixtureNormalizer.Normalize(pr);
        Assert.Equal("acme/api", result.Repo);
    }

    [Fact]
    public void Normalize_SetsPullRequestNumber()
    {
        var pr = MakePr(prNumber: 42);
        var result = FixtureNormalizer.Normalize(pr);
        Assert.Equal(42, result.PullRequestNumber);
    }

    [Fact]
    public void Normalize_TagsContainAsync_WhenDiffHasAwait()
    {
        var pr = MakePr(diff: "var t = await GetAsync();");
        var result = FixtureNormalizer.Normalize(pr);
        Assert.Contains("async", result.Tags);
    }

    [Fact]
    public void Normalize_TagsContainContractChange_WhenDiffHasPublic()
    {
        var pr = MakePr(diff: "public void MyMethod() {}");
        var result = FixtureNormalizer.Normalize(pr);
        Assert.Contains("contract-change", result.Tags);
    }

    [Fact]
    public void Normalize_TagsContainNullSafety_WhenDiffHasNull()
    {
        var pr = MakePr(diff: "if (x == null) throw new ArgumentNullException();");
        var result = FixtureNormalizer.Normalize(pr);
        Assert.Contains("null-safety", result.Tags);
    }

    [Fact]
    public void Normalize_TagsEmpty_WhenDiffHasNoKeywords()
    {
        var pr = MakePr(diff: "var x = 5;");
        var result = FixtureNormalizer.Normalize(pr);
        Assert.Empty(result.Tags);
    }

    [Fact]
    public void Normalize_TagsAreSorted()
    {
        // "await" triggers "async", "null" triggers "null-safety"
        var pr = MakePr(diff: "await foo(); var x = null;");
        var result = FixtureNormalizer.Normalize(pr);
        Assert.Equal(result.Tags.OrderBy(t => t), result.Tags);
    }

    [Fact]
    public void Normalize_InferredLanguage_MostFrequentExtension()
    {
        var files = new[]
        {
            new ChangedFile { LanguageHint = "C#" },
            new ChangedFile { LanguageHint = "C#" },
            new ChangedFile { LanguageHint = "TypeScript" },
        };
        var pr = MakePr(files: files);
        var result = FixtureNormalizer.Normalize(pr);
        Assert.Equal("C#", result.Language);
    }

    [Fact]
    public void Normalize_InferredLanguage_EmptyFiles_ReturnsEmpty()
    {
        var pr = MakePr(files: []);
        var result = FixtureNormalizer.Normalize(pr);
        Assert.Equal("", result.Language);
    }

    [Fact]
    public void Normalize_HasReviewComments_WhenCommentsPresent()
    {
        var comments = new[] { new ReviewComment { Body = "looks risky" } };
        var pr = MakePr(comments: comments);
        var result = FixtureNormalizer.Normalize(pr);
        Assert.True(result.HasReviewComments);
    }

    [Fact]
    public void Normalize_HasTestsChanged_WhenTestFilePresent()
    {
        var files = new[] { new ChangedFile { IsTestFile = true } };
        var pr = MakePr(files: files);
        var result = FixtureNormalizer.Normalize(pr);
        Assert.True(result.HasTestsChanged);
    }

    [Fact]
    public void Normalize_DefaultSource_IsManual()
    {
        var pr = MakePr();
        var result = FixtureNormalizer.Normalize(pr);
        Assert.Equal("manual", result.Source);
    }

    [Fact]
    public void Normalize_DefaultTier_IsDiscovery()
    {
        var pr = MakePr();
        var result = FixtureNormalizer.Normalize(pr);
        Assert.Equal(FixtureTier.Discovery, result.Tier);
    }
}

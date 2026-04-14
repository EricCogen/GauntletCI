// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Corpus.Hydration;

namespace GauntletCI.Tests;

public class IssueEnricherTests
{
    private const string DefaultOwner = "test-owner";
    private const string DefaultRepo  = "test-repo";

    // ── ParseBodyRefs ────────────────────────────────────────────────────────

    [Fact]
    public void ParseBodyRefs_EmptyBody_ReturnsEmpty()
    {
        // Arrange / Act
        var result = IssueEnricher.ParseBodyRefs(DefaultOwner, DefaultRepo, "");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ParseBodyRefs_WhitespaceBody_ReturnsEmpty()
    {
        // Arrange / Act
        var result = IssueEnricher.ParseBodyRefs(DefaultOwner, DefaultRepo, "   ");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ParseBodyRefs_ClosesHash_ExtractsIssue()
    {
        // Arrange / Act
        var result = IssueEnricher.ParseBodyRefs(DefaultOwner, DefaultRepo, "Closes #123");

        // Assert
        var (owner, repo, number) = Assert.Single(result);
        Assert.Equal(DefaultOwner, owner);
        Assert.Equal(DefaultRepo, repo);
        Assert.Equal(123, number);
    }

    [Fact]
    public void ParseBodyRefs_FixesHash_ExtractsIssue()
    {
        // Arrange / Act
        var result = IssueEnricher.ParseBodyRefs(DefaultOwner, DefaultRepo, "fixes #456");

        // Assert
        var (owner, repo, number) = Assert.Single(result);
        Assert.Equal(DefaultOwner, owner);
        Assert.Equal(DefaultRepo, repo);
        Assert.Equal(456, number);
    }

    [Fact]
    public void ParseBodyRefs_ResolvedHash_ExtractsIssue()
    {
        // Arrange / Act
        var result = IssueEnricher.ParseBodyRefs(DefaultOwner, DefaultRepo, "Resolved #789");

        // Assert
        var (owner, repo, number) = Assert.Single(result);
        Assert.Equal(DefaultOwner, owner);
        Assert.Equal(DefaultRepo, repo);
        Assert.Equal(789, number);
    }

    [Fact]
    public void ParseBodyRefs_CrossRepoReference_UsesExplicitOwnerRepo()
    {
        // Arrange / Act
        var result = IssueEnricher.ParseBodyRefs(DefaultOwner, DefaultRepo, "Closes other-owner/other-repo#42");

        // Assert
        var (owner, repo, number) = Assert.Single(result);
        Assert.Equal("other-owner", owner);
        Assert.Equal("other-repo", repo);
        Assert.Equal(42, number);
    }

    [Fact]
    public void ParseBodyRefs_DuplicateReference_DeduplicatesResult()
    {
        // Arrange / Act
        var result = IssueEnricher.ParseBodyRefs(DefaultOwner, DefaultRepo, "Closes #1\nFixes #1");

        // Assert
        var (owner, repo, number) = Assert.Single(result);
        Assert.Equal(DefaultOwner, owner);
        Assert.Equal(DefaultRepo, repo);
        Assert.Equal(1, number);
    }

    [Fact]
    public void ParseBodyRefs_MultipleReferences_ReturnsAll()
    {
        // Arrange / Act
        var result = IssueEnricher.ParseBodyRefs(DefaultOwner, DefaultRepo, "Closes #1 and fixes #2");

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.Number == 1);
        Assert.Contains(result, r => r.Number == 2);
    }

    [Fact]
    public void ParseBodyRefs_NoMatchingKeywords_ReturnsEmpty()
    {
        // Arrange / Act — "See" is not a closing keyword
        var result = IssueEnricher.ParseBodyRefs(DefaultOwner, DefaultRepo, "See #123 for context");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ParseBodyRefs_ClosedKeyword_Matches()
    {
        // Arrange / Act
        var result = IssueEnricher.ParseBodyRefs(DefaultOwner, DefaultRepo, "closed #99");

        // Assert
        var (_, _, number) = Assert.Single(result);
        Assert.Equal(99, number);
    }

    [Fact]
    public void ParseBodyRefs_CaseInsensitive_Matches()
    {
        // Arrange / Act
        var result = IssueEnricher.ParseBodyRefs(DefaultOwner, DefaultRepo, "FIXES #55");

        // Assert
        var (_, _, number) = Assert.Single(result);
        Assert.Equal(55, number);
    }
}

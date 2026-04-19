// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Cli.TicketProviders;
using GauntletCI.Core.Model;

namespace GauntletCI.Tests;

public class TicketResolverTests
{
    [Theory]
    [InlineData("feature/PROJ-1234", null, "PROJ-1234", "Jira")]
    [InlineData("feature/eng-123-my-task", null, "eng-123", "Linear")]
    [InlineData("fix/#42-crash", null, "42", "GitHub")]
    [InlineData("feature/COMP-99", "Related to COMP-99", "COMP-99", "Jira")]
    public void DetectIssueKey_DetectsCorrectKey(string branch, string? prBody, string expectedKey, string expectedProvider)
    {
        var (key, provider) = TicketResolver.DetectIssueKey(branch, prBody);
        Assert.Equal(expectedKey, key);
        Assert.Equal(expectedProvider, provider);
    }

    [Fact]
    public void DetectIssueKey_NoBranchNoPrBody_ReturnsNull()
    {
        var (key, provider) = TicketResolver.DetectIssueKey("main", null);
        Assert.Null(key);
        Assert.Null(provider);
    }

    [Fact]
    public void DetectIssueKey_PrBodyHasGitHubIssue_Detected()
    {
        var (key, provider) = TicketResolver.DetectIssueKey("fix/login", "Fixes #99");
        Assert.Equal("99", key);
        Assert.Equal("GitHub", provider);
    }

    [Fact]
    public void DetectIssueKey_GhPrefix_Detected()
    {
        // GH-77 matches the Jira pattern (project key "GH"), which takes priority
        var (key, provider) = TicketResolver.DetectIssueKey("fix/GH-77-crash", null);
        Assert.Equal("GH-77", key);
        Assert.Equal("Jira", provider);
    }

    [Fact]
    public void DetectIssueKey_HashGitHubIssue_InBranch_Detected()
    {
        var (key, provider) = TicketResolver.DetectIssueKey("fix/#77-crash", null);
        Assert.Equal("77", key);
        Assert.Equal("GitHub", provider);
    }

    [Fact]
    public void DetectIssueKey_JiraPriorityOverLinear_WhenBothPresent()
    {
        // Branch contains both a Jira key and a Linear-looking fragment
        var (key, provider) = TicketResolver.DetectIssueKey("feature/PROJ-10/eng-5", null);
        Assert.Equal("PROJ-10", key);
        Assert.Equal("Jira", provider);
    }

    [Fact]
    public void DetectIssueKey_LinearPriorityOverGitHub_WhenNoJira()
    {
        var (key, provider) = TicketResolver.DetectIssueKey("fix/eng-42-bug", "Closes #10");
        Assert.Equal("eng-42", key);
        Assert.Equal("Linear", provider);
    }

    [Fact]
    public void DetectIssueKey_EmptyBranchAndPrBody_ReturnsNull()
    {
        var (key, provider) = TicketResolver.DetectIssueKey(null, null);
        Assert.Null(key);
        Assert.Null(provider);
    }

    [Fact]
    public void DetectIssueKey_BranchWithNoIssueKey_ReturnsNull()
    {
        var (key, provider) = TicketResolver.DetectIssueKey("feature/add-login-page", null);
        Assert.Null(key);
        Assert.Null(provider);
    }

    [Fact]
    public void JiraProvider_IsNotAvailable_WhenEnvVarsMissing()
    {
        var prevUrl   = Environment.GetEnvironmentVariable("JIRA_BASE_URL");
        var prevToken = Environment.GetEnvironmentVariable("JIRA_API_TOKEN");
        var prevEmail = Environment.GetEnvironmentVariable("JIRA_USER_EMAIL");
        try
        {
            Environment.SetEnvironmentVariable("JIRA_BASE_URL",   null);
            Environment.SetEnvironmentVariable("JIRA_API_TOKEN",  null);
            Environment.SetEnvironmentVariable("JIRA_USER_EMAIL", null);
            var provider = new JiraTicketProvider();
            Assert.False(provider.IsAvailable);
        }
        finally
        {
            Environment.SetEnvironmentVariable("JIRA_BASE_URL",   prevUrl);
            Environment.SetEnvironmentVariable("JIRA_API_TOKEN",  prevToken);
            Environment.SetEnvironmentVariable("JIRA_USER_EMAIL", prevEmail);
        }
    }

    [Fact]
    public void LinearProvider_IsNotAvailable_WhenEnvVarMissing()
    {
        var prev = Environment.GetEnvironmentVariable("LINEAR_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("LINEAR_API_KEY", null);
            var provider = new LinearTicketProvider();
            Assert.False(provider.IsAvailable);
        }
        finally
        {
            Environment.SetEnvironmentVariable("LINEAR_API_KEY", prev);
        }
    }

    [Fact]
    public void GitHubProvider_IsNotAvailable_WhenEnvVarMissing()
    {
        var prevToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        var prevRepo  = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY");
        try
        {
            Environment.SetEnvironmentVariable("GITHUB_TOKEN",      null);
            Environment.SetEnvironmentVariable("GITHUB_REPOSITORY", null);
            var provider = new GitHubIssueProvider();
            Assert.False(provider.IsAvailable);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GITHUB_TOKEN",      prevToken);
            Environment.SetEnvironmentVariable("GITHUB_REPOSITORY", prevRepo);
        }
    }

    [Fact]
    public void TicketInfo_Properties_RoundTrip()
    {
        var t = new TicketInfo { Id = "PROJ-1", Title = "Fix login", Provider = "Jira", Url = "https://x.atlassian.net/browse/PROJ-1" };
        Assert.Equal("PROJ-1", t.Id);
        Assert.Equal("Fix login", t.Title);
        Assert.Equal("Jira", t.Provider);
        Assert.Equal("https://x.atlassian.net/browse/PROJ-1", t.Url);
        Assert.Null(t.Description);
    }

    [Fact]
    public void TicketInfo_Description_Truncation_CanBeSet()
    {
        var longDesc = new string('x', 600);
        var truncated = longDesc.Length > 500 ? longDesc[..500] : longDesc;
        var t = new TicketInfo { Id = "ENG-1", Title = "Long desc", Provider = "Linear", Description = truncated };
        Assert.Equal(500, t.Description!.Length);
    }

    [Fact]
    public void ResolveProvider_ReturnsJiraProvider_ForJira()
    {
        var provider = TicketResolver.ResolveProvider("Jira");
        Assert.NotNull(provider);
        Assert.Equal("Jira", provider.ProviderName);
    }

    [Fact]
    public void ResolveProvider_ReturnsLinearProvider_ForLinear()
    {
        var provider = TicketResolver.ResolveProvider("Linear");
        Assert.NotNull(provider);
        Assert.Equal("Linear", provider.ProviderName);
    }

    [Fact]
    public void ResolveProvider_ReturnsGitHubProvider_ForGitHub()
    {
        var provider = TicketResolver.ResolveProvider("GitHub");
        Assert.NotNull(provider);
        Assert.Equal("GitHub", provider.ProviderName);
    }

    [Fact]
    public void ResolveProvider_ReturnsNull_ForUnknownProvider()
    {
        var provider = TicketResolver.ResolveProvider("Unknown");
        Assert.Null(provider);
    }

    [Fact]
    public void Finding_TicketContext_CanBeAssigned()
    {
        var finding = new Finding
        {
            RuleId = "GCI0001",
            RuleName = "Test Rule",
            Summary = "Test",
            Evidence = "evidence",
            WhyItMatters = "why",
            SuggestedAction = "action",
        };
        var ticket = new TicketInfo { Id = "PROJ-42", Title = "Deploy fix", Provider = "Jira" };
        finding.TicketContext = ticket;

        Assert.NotNull(finding.TicketContext);
        Assert.Equal("PROJ-42", finding.TicketContext.Id);
        Assert.Equal("Jira", finding.TicketContext.Provider);
    }

    [Fact]
    public async Task AnnotateFindingsAsync_NoFindings_DoesNotThrow()
    {
        // No findings → should return immediately without throwing, even on branches with no key.
        var prev = Environment.GetEnvironmentVariable("GITHUB_PR_BODY");
        try
        {
            Environment.SetEnvironmentVariable("GITHUB_PR_BODY", null);
            await TicketResolver.AnnotateFindingsAsync("main", [], CancellationToken.None);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GITHUB_PR_BODY", prev);
        }
    }
}

// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Cli.LlmDaemon;

namespace GauntletCI.Tests;

public class LlmDaemonAuthTests
{
    [Fact]
    public void IsValidToken_AcceptsMatchingToken()
    {
        const string token = "abc123";
        Assert.True(LlmDaemonAuth.IsValidToken(token, token));
    }

    [Theory]
    [InlineData(null, "abc")]
    [InlineData("abc", null)]
    [InlineData("", "abc")]
    [InlineData("abc", "abcd")]
    [InlineData("abc", "abd")]
    public void IsValidToken_RejectsMissingOrMismatchedTokens(string? expected, string? provided)
    {
        Assert.False(LlmDaemonAuth.IsValidToken(expected, provided));
    }

    [Fact]
    public void GenerateToken_ProducesDistinctValues()
    {
        var first = LlmDaemonAuth.GenerateToken();
        var second = LlmDaemonAuth.GenerateToken();

        Assert.NotEqual(first, second);
        Assert.True(first.Length >= 32);
    }
}

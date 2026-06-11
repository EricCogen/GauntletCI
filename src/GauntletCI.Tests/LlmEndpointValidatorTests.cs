// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Security;

namespace GauntletCI.Tests;

public class LlmEndpointValidatorTests
{
    [Theory]
    [InlineData("http://localhost:11434")]
    [InlineData("http://127.0.0.1:11434")]
    [InlineData("https://localhost:11434")]
    public void TryValidateMcpOllamaBaseUrl_AcceptsLoopback(string url)
    {
        Assert.True(LlmEndpointValidator.TryValidateMcpOllamaBaseUrl(url, out var error));
        Assert.Null(error);
    }

    [Theory]
    [InlineData("http://10.0.0.5:11434")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("http://192.168.1.10:11434")]
    public void TryValidateMcpOllamaBaseUrl_RejectsNonLoopback(string url)
    {
        Assert.False(LlmEndpointValidator.TryValidateMcpOllamaBaseUrl(url, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Theory]
    [InlineData("http://10.0.0.5:11434")]
    [InlineData("http://192.168.1.10:11434")]
    public void TryValidateConfigOllamaBaseUrl_AcceptsPrivateLan(string url)
    {
        Assert.True(LlmEndpointValidator.TryValidateConfigOllamaBaseUrl(url, out var error));
        Assert.Null(error);
    }

    [Theory]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("http://127.0.0.2:11434")]
    public void TryValidateConfigOllamaBaseUrl_RejectsMetadataAndNonLoopback127(string url)
    {
        Assert.False(LlmEndpointValidator.TryValidateConfigOllamaBaseUrl(url, out _));
    }
}

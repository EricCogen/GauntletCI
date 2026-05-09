// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Corpus.Labeling;

namespace GauntletCI.Tests;

public class LlmLabelerFactoryTests
{
    [Fact]
    public void Create_NoneProvider_ReturnsNullLabeler()
    {
        var labeler = LlmLabelerFactory.Create("none");
        Assert.IsType<NullLlmLabeler>(labeler);
    }

    [Fact]
    public void Create_NullProvider_ReturnsNullLabeler()
    {
        var labeler = LlmLabelerFactory.Create("null");
        Assert.IsType<NullLlmLabeler>(labeler);
    }

    [Fact]
    public void Create_UnknownProvider_ReturnsNullLabeler()
    {
        var labeler = LlmLabelerFactory.Create("unknown-provider");
        Assert.IsType<NullLlmLabeler>(labeler);
    }

    [Fact]
    public void Create_EmptyProvider_ReturnsNullLabeler()
    {
        var labeler = LlmLabelerFactory.Create("");
        Assert.IsType<NullLlmLabeler>(labeler);
    }

    [Fact]
    public void Create_AnthropicWithoutApiKey_ReturnsNullLabeler()
    {
        var savedKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", null);
            var labeler = LlmLabelerFactory.Create("anthropic");
            Assert.IsType<NullLlmLabeler>(labeler);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", savedKey);
        }
    }

    [Fact]
    public void Create_GitHubModelsWithoutToken_ReturnsLabelerBasedOnAvailability()
    {
        // With GitHubTokenResolver, clearing GITHUB_TOKEN still allows gh CLI fallback.
        // Assert the correct labeler type based on what the resolver can actually find.
        var savedToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        try
        {
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", null);
            var hasToken = GauntletCI.Corpus.GitHubTokenResolver.IsAvailable;
            var labeler  = LlmLabelerFactory.Create("github-models");
            if (hasToken)
                Assert.IsType<GitHubModelsLlmLabeler>(labeler);
            else
                Assert.IsType<NullLlmLabeler>(labeler);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", savedToken);
        }
    }

    [Fact]
    public void Create_OllamaProvider_ReturnsOllamaLabeler()
    {
        var labeler = LlmLabelerFactory.Create("ollama", "phi4-mini:latest", "http://localhost:11434");
        Assert.IsType<OllamaLlmLabeler>(labeler);
        (labeler as IDisposable)?.Dispose();
    }

    [Fact]
    public void Create_OllamaProviderUppercase_ReturnsOllamaLabeler()
    {
        var labeler = LlmLabelerFactory.Create("OLLAMA");
        Assert.IsType<OllamaLlmLabeler>(labeler);
        (labeler as IDisposable)?.Dispose();
    }

    [Fact]
    public void SupportedProviders_ContainsExpectedValues()
    {
        var providers = LlmLabelerFactory.SupportedProviders;
        Assert.Contains("ollama", providers);
        Assert.Contains("anthropic", providers);
        Assert.Contains("github-models", providers);
        Assert.Contains("none", providers);
    }
}

// SPDX-License-Identifier: Elastic-2.0
using System.Net.Http.Headers;

namespace GauntletCI.Core;

/// <summary>
/// Centralized factory for creating and managing <see cref="HttpClient"/> instances.
/// Prevents socket exhaustion by maintaining pooled, reusable clients instead of creating one per constructor.
/// 
/// Clients are static and long-lived. The factory manages configuration and header injection.
/// Callers should NOT dispose clients obtained from this factory.
/// </summary>
public static class HttpClientFactory
{
    // GitHub API client: uses auth token if available, 30-60 second timeout depending on use case
    private static readonly Lazy<HttpClient> GithubClient = new(() => CreateGitHubClient());
    
    // SonarCloud client: unauthenticated, public API only, 30 second timeout
    private static readonly Lazy<HttpClient> SonarCloudClientInstance = new(() => CreateSonarCloudClient());
    
    // Generic client: no auth, 30 second default timeout
    private static readonly Lazy<HttpClient> GenericClient = new(() => CreateGenericClient());
    
    // Anthropic client: API key auth, 120 second timeout for inference calls
    private static readonly Lazy<HttpClient> AnthropicClientInstance = new(() => CreateAnthropicClient());
    
    // Codecov client: Bearer token auth, 15 second timeout
    private static readonly Lazy<HttpClient> CodecovClientInstance = new(() => CreateCodecovClient());
    
    // Long-timeout client: for expensive operations, 120 second timeout
    private static readonly Lazy<HttpClient> LongTimeoutClient = new(() => CreateLongTimeoutClient());

    /// <summary>
    /// Gets a GitHub API client pre-configured with auth headers (if token available).
    /// Do NOT dispose; client is managed by the factory.
    /// </summary>
    public static HttpClient GetGitHubClient() => GithubClient.Value;

    /// <summary>
    /// Gets a SonarCloud API client (unauthenticated, for public projects).
    /// Do NOT dispose; client is managed by the factory.
    /// </summary>
    public static HttpClient GetSonarCloudClient() => SonarCloudClientInstance.Value;

    /// <summary>
    /// Gets a generic HTTP client with minimal configuration.
    /// Do NOT dispose; client is managed by the factory.
    /// </summary>
    public static HttpClient GetGenericClient() => GenericClient.Value;

    /// <summary>
    /// Gets an Anthropic API client pre-configured with auth headers.
    /// Do NOT dispose; client is managed by the factory.
    /// </summary>
    public static HttpClient GetAnthropicClient() => AnthropicClientInstance.Value;

    /// <summary>
    /// Gets a Codecov API client pre-configured with auth headers (if token available).
    /// Do NOT dispose; client is managed by the factory.
    /// </summary>
    public static HttpClient GetCodecovClient() => CodecovClientInstance.Value;

    /// <summary>
    /// Gets an HTTP client with a long timeout (120 seconds) for expensive remote operations.
    /// Do NOT dispose; client is managed by the factory.
    /// </summary>
    public static HttpClient GetLongTimeoutClient() => LongTimeoutClient.Value;

    /// <summary>
    /// Creates a new HttpClient with GitHub API configuration.
    /// Includes auth token if GITHUB_TOKEN is set or gh auth is available.
    /// </summary>
    private static HttpClient CreateGitHubClient()
    {
        var client = new HttpClient(new SocketsHttpHandler 
        { 
            PooledConnectionLifetime = TimeSpan.FromMinutes(5) 
        })
        {
            Timeout = TimeSpan.FromSeconds(60)
        };
        
        client.DefaultRequestHeaders.Add("User-Agent", "GauntletCI/2.0");
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
        
        var token = GitHubTokenResolver.Resolve();
        if (!string.IsNullOrEmpty(token))
            client.DefaultRequestHeaders.Add("Authorization", $"token {token}");
        
        return client;
    }

    /// <summary>
    /// Creates a new HttpClient for SonarCloud API (unauthenticated).
    /// </summary>
    private static HttpClient CreateSonarCloudClient()
    {
        var client = new HttpClient(new SocketsHttpHandler 
        { 
            PooledConnectionLifetime = TimeSpan.FromMinutes(5) 
        })
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        
        client.DefaultRequestHeaders.Add("User-Agent", "GauntletCI/2.0");
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        
        return client;
    }

    /// <summary>
    /// Creates a generic HTTP client with minimal configuration.
    /// </summary>
    private static HttpClient CreateGenericClient()
    {
        var client = new HttpClient(new SocketsHttpHandler 
        { 
            PooledConnectionLifetime = TimeSpan.FromMinutes(5) 
        })
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        
        client.DefaultRequestHeaders.Add("User-Agent", "GauntletCI/2.0");
        
        return client;
    }

    /// <summary>
    /// Creates a new HttpClient for Anthropic API (requires api key to be set by caller).
    /// </summary>
    private static HttpClient CreateAnthropicClient()
    {
        var client = new HttpClient(new SocketsHttpHandler 
        { 
            PooledConnectionLifetime = TimeSpan.FromMinutes(5) 
        })
        {
            Timeout = TimeSpan.FromSeconds(120)
        };
        
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        
        return client;
    }

    /// <summary>
    /// Creates a new HttpClient for Codecov API (requires token to be set by caller).
    /// </summary>
    private static HttpClient CreateCodecovClient()
    {
        var client = new HttpClient(new SocketsHttpHandler 
        { 
            PooledConnectionLifetime = TimeSpan.FromMinutes(5) 
        })
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
        
        client.DefaultRequestHeaders.Add("User-Agent", "GauntletCI/2.0");
        
        return client;
    }

    /// <summary>
    /// Creates a new HttpClient with a long timeout for expensive operations.
    /// </summary>
    private static HttpClient CreateLongTimeoutClient()
    {
        var client = new HttpClient(new SocketsHttpHandler 
        { 
            PooledConnectionLifetime = TimeSpan.FromMinutes(5) 
        })
        {
            Timeout = TimeSpan.FromSeconds(120)
        };
        
        client.DefaultRequestHeaders.Add("User-Agent", "GauntletCI/2.0");
        
        return client;
    }
}

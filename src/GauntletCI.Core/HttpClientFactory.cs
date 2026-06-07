// SPDX-License-Identifier: Elastic-2.0
using System.Net.Http.Headers;

namespace GauntletCI.Core;

/// <summary>
/// Centralized factory for creating and managing <see cref="HttpClient"/> instances.
/// Prevents socket exhaustion by maintaining pooled, reusable clients instead of creating one per constructor.
/// 
/// Clients are static and long-lived. Credentials are NEVER injected into DefaultRequestHeaders.
/// Callers must attach authentication per-request using HttpRequestMessage.Headers to avoid credential bleed.
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

    // Anthropic / webhook client: no redirect, 120 second timeout (webhooks share this pool)
    private static readonly Lazy<HttpClient> AnthropicClientInstance = new(() => CreateAnthropicClient());

    // Codecov client: Bearer token auth, 15 second timeout
    private static readonly Lazy<HttpClient> CodecovClientInstance = new(() => CreateCodecovClient());

    // Long-timeout client: for expensive operations, 120 second timeout
    private static readonly Lazy<HttpClient> LongTimeoutClient = new(() => CreateLongTimeoutClient());

    /// <summary>
    /// Gets a GitHub API client. Credentials must be attached per-request via HttpRequestMessage.Headers.
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
    /// Gets an Anthropic API client. API key must be attached per-request via HttpRequestMessage.Headers.
    /// Disables automatic redirects to prevent credential leakage on unexpected redirects.
    /// Do NOT dispose; client is managed by the factory.
    /// </summary>
    public static HttpClient GetAnthropicClient() => AnthropicClientInstance.Value;

    /// <summary>
    /// Gets an HTTP client for Slack/Teams incoming webhook POSTs.
    /// Shares the no-redirect client pool (redirects disabled for SSRF hardening).
    /// Do NOT dispose; client is managed by the factory.
    /// </summary>
    public static HttpClient GetWebhookClient() => AnthropicClientInstance.Value;

    /// <summary>
    /// Gets a Codecov API client. Token must be attached per-request via HttpRequestMessage.Headers.
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
    /// Callers must attach GitHub token per-request via HttpRequestMessage.Headers to avoid credential bleed.
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

        // Do NOT inject auth here. Auth tokens must be added per-request to avoid bleed to other endpoints.
        // Components must add Authorization header via HttpRequestMessage.Headers per request.

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
    /// Creates a new HttpClient for Anthropic API (requires api key to be set by caller per-request).
    /// Disables automatic redirects to prevent custom sensitive headers (x-api-key) from leaking
    /// to unintended endpoints on redirects.
    /// </summary>
    private static HttpClient CreateAnthropicClient()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            AllowAutoRedirect = false
        };

        var client = new HttpClient(handler)
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

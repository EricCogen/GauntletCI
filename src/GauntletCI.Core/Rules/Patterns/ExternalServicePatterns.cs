// SPDX-License-Identifier: Elastic-2.0

// ========== GauntletCI Self-Analysis: WHITELISTED FILE ==========
// This file contains pattern strings and regexes used by GauntletCI detection rules.
// Pattern strings (e.g., "new FileStream(", "TODO", ".GetService<") appear in the code by design.
// These are NOT actual code violations, but rather PATTERN DEFINITIONS used to detect violations in other code.
// GauntletCI Self-Analysis should skip this file from analysis to avoid false positives on pattern data.
// =================================================================

namespace GauntletCI.Core.Rules.Patterns;

/// <summary>
/// Patterns used to detect external service and HTTP client safety issues.
/// </summary>
internal static class ExternalServicePatterns
{
    /// <summary>
    /// HTTP method calls (on HttpClient or HttpRequestMessage) that should have timeouts and cancellation tokens.
    /// Used by GCI0039 to detect unsafe external service calls.
    /// </summary>
    public static readonly string[] HttpCallMethods =
    [
        ".GetAsync(", ".PostAsync(", ".PutAsync(", ".DeleteAsync(", ".SendAsync("
    ];

    /// <summary>
    /// Subset of HTTP methods for cancellation token checking (excludes DeleteAsync which conflicts with SDK methods).
    /// Used by GCI0039 to detect missing CancellationToken parameters on HTTP calls.
    /// </summary>
    public static readonly string[] CtCheckHttpMethods =
    [
        ".GetAsync(", ".PostAsync(", ".PutAsync(", ".SendAsync("
    ];

    /// <summary>
    /// Polly resilience patterns that indicate retry/timeout policies are already in place.
    /// Used by GCI0039 to skip flagging HTTP calls when Polly policies manage timeouts.
    /// </summary>
    public static readonly string[] PollyPatterns =
    [
        ".WaitAndRetry", ".CircuitBreaker", ".Timeout", "TimeoutPolicy",
        "PolicyBuilder", ".AddPolicyHandler", "AddResilienceHandler"
    ];

    /// <summary>
    /// HttpClientFactory configuration patterns indicating managed HTTP clients with centralized timeout configuration.
    /// Used by GCI0039 to skip timeout checks on factory-configured clients.
    /// </summary>
    public static readonly string[] FactoryConfigPatterns =
    [
        "AddHttpClient", "IHttpClientFactory", "CreateClient", "typed client",
        "ConfigureHttpClient", "AddHttpMessageHandler"
    ];

    /// <summary>
    /// Fire-and-forget async patterns where cancellation tokens are not applicable.
    /// Used by GCI0039 to skip CancellationToken requirements on intentional fire-and-forget operations.
    /// </summary>
    public static readonly string[] FireAndForgetPatterns =
    [
        "_ =", ".FireAndForget", "#pragma warning disable",
        ".Forget(", "// fire and forget"
    ];
}

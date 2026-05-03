// SPDX-License-Identifier: Elastic-2.0

// ========== GauntletCI Self-Analysis: WHITELISTED FILE ==========
// This file contains pattern strings and regexes used by GauntletCI detection rules.
// Pattern strings (e.g., "new FileStream(", "TODO", ".GetService<") appear in the code by design.
// These are NOT actual code violations, but rather PATTERN DEFINITIONS used to detect violations in other code.
// GauntletCI Self-Analysis should skip this file from analysis to avoid false positives on pattern data.
// =================================================================

using GauntletCI.Core.Diff;

namespace GauntletCI.Core.Rules.Patterns;

/// <summary>
/// HTTP client, gRPC, timeout, and external service detection patterns.
/// Used by GCI0039 and related rules to reduce false positives on framework-specific timeout patterns.
/// </summary>
internal static class HttpExternalServicePatterns
{
    /// <summary>
    /// Returns <c>true</c> when the file path indicates gRPC-related code.
    /// gRPC channels manage timeouts at the channel/connection level, not per-HttpClient.
    /// Used by GCI0039 to skip false positive timeout checks in gRPC contexts.
    /// </summary>
    public static bool IsGrpcRelatedFile(string path)
    {
        return path.Contains("grpc", StringComparison.OrdinalIgnoreCase)
            || path.Contains("channel", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns <c>true</c> when the added lines indicate HttpClient configuration via IHttpClientFactory.
    /// Factory-managed clients configure timeout at the handler/channel level, not per-client.
    /// </summary>
    public static bool IsHttpFactoryConfigured(List<DiffLine> addedLines)
    {
        return addedLines.Any(l =>
            l.Content.Contains("IHttpClientFactory", StringComparison.Ordinal)
            || l.Content.Contains("AddHttpClient", StringComparison.Ordinal)
            || l.Content.Contains("HttpClientFactoryOptions", StringComparison.Ordinal));
    }

    /// <summary>
    /// Returns <c>true</c> when the added lines indicate gRPC channel configuration.
    /// gRPC channels manage timeouts via GrpcChannelOptions at the connection level.
    /// </summary>
    public static bool UsesGrpcChannel(List<DiffLine> addedLines)
    {
        return addedLines.Any(l =>
            l.Content.Contains("GrpcChannel", StringComparison.Ordinal)
            || l.Content.Contains("ChannelOptions", StringComparison.Ordinal)
            || l.Content.Contains("GrpcChannelOptions", StringComparison.Ordinal))
            || addedLines.Any(l => 
                l.Content.Contains("HttpClientHandler", StringComparison.Ordinal)
                && addedLines.Any(hl => hl.Content.Contains("GrpcChannel", StringComparison.Ordinal)));
    }

    /// <summary>
    /// Returns <c>true</c> when the added lines indicate use of factory-managed or injected HTTP clients.
    /// Factory patterns, Polly policies, and DI-managed clients manage timeouts externally.
    /// </summary>
    public static bool UsesFactoryManagedHttpClients(List<DiffLine> addedLines)
    {
        var factoryPatterns = new[]
        {
            "IHttpClientFactory", "AddHttpClient", "HttpClientFactoryOptions",
            "AddPolicyHandler", "AddTransientHttpErrorPolicy", "Polly"
        };

        return addedLines.Any(l =>
            factoryPatterns.Any(p => l.Content.Contains(p, StringComparison.Ordinal)));
    }

    /// <summary>
    /// Returns <c>true</c> when the content indicates an injected or static HttpClient is being used.
    /// Patterns like _httpClient.GetAsync() or this.client.PostAsync() are typically DI-managed.
    /// </summary>
    public static bool IsInjectedOrStaticClient(string content)
    {
        var injectionPatterns = new[]
        {
            "_httpClient", "_client", "this.client", "this._client",
            "httpClient.", "_http.", "HttpClient."
        };

        var httpMethods = new[] { ".GetAsync(", ".PostAsync(", ".PutAsync(", ".SendAsync(" };

        return injectionPatterns.Any(p =>
            content.Contains(p, StringComparison.Ordinal) &&
            httpMethods.Any(m => content.Contains(m)));
    }

    /// <summary>
    /// Returns <c>true</c> when the file uses .NET 9+ modern patterns (checked operators, required members, etc).
    /// These patterns strongly indicate NRT is enabled in modern project contexts.
    /// </summary>
    public static bool UsesModernDotNetPatterns(string fileContent)
    {
        // .NET 9+ patterns
        var modernPatterns = new[]
        {
            "checked(", "unchecked(", // Checked operators
            "required ", // Required members (C# 11+)
            "file class", "file struct", // File-scoped types (C# 11+)
            "field ", // Field keyword in properties (C# 13+)
            "collection", // Collection expression syntax
            ".. ", // Range operator in more contexts
            "namespace ", // File-scoped namespaces (C# 10+)
        };

        return modernPatterns.Any(p => fileContent.Contains(p, StringComparison.Ordinal));
    }

    /// <summary>
    /// Returns <c>true</c> when a method signature uses record type parameters or other modern patterns.
    /// Helps identify rules applied to modern code that typically has NRT enabled.
    /// </summary>
    public static bool HasModernTypeParameters(string paramSection)
    {
        // Record parameters, required parameters, init properties
        return paramSection.Contains(" record ", StringComparison.Ordinal)
            || paramSection.Contains("required ", StringComparison.Ordinal)
            || paramSection.Contains("{ init; }", StringComparison.Ordinal)
            || paramSection.Contains("{ get; init; }", StringComparison.Ordinal);
    }
}

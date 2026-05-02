// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0039, External Service Safety
/// Detects unsafe HTTP client and external service usage patterns in C# code.
/// </summary>
public class GCI0039_ExternalServiceSafety : RuleBase
{
    public override string Id => "GCI0039";
    public override string Name => "External Service Safety";

    private static readonly string[] HttpCallMethods =
    [
        ".GetAsync(", ".PostAsync(", ".PutAsync(", ".DeleteAsync(", ".SendAsync("
    ];

    // Subset used for CheckMissingCancellationToken: .DeleteAsync( is excluded because
    // many non-HTTP SDKs (DynamoDB, CosmosDB, etc.) expose identically-named methods.
    private static readonly string[] CtCheckHttpMethods =
    [
        ".GetAsync(", ".PostAsync(", ".PutAsync(", ".SendAsync("
    ];

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var diff = context.Diff;
        var findings = new List<Finding>();

        foreach (var file in diff.Files)
        {
            if (IsTestFile(file.NewPath)) continue;

            CheckHttpClientInstantiation(file, findings);
            CheckMissingTimeout(file, findings);
            CheckMissingCancellationToken(file, findings);
        }

        return Task.FromResult(findings);
    }

    private static bool IsTestFile(string path) =>
        path.Contains("test", StringComparison.OrdinalIgnoreCase)
        || path.Contains("spec", StringComparison.OrdinalIgnoreCase);

    private void CheckHttpClientInstantiation(DiffFile file, List<Finding> findings)
    {
        // Skip gRPC files entirely - gRPC Channel initialization IS the timeout mechanism
        if (IsGrpcRelatedFile(file.NewPath)) return;

        foreach (var line in file.AddedLines)
        {
            var content = line.Content;
            if (content.TrimStart().StartsWith("//")) continue;
            if (!content.Contains("new HttpClient(")) continue;

            findings.Add(CreateFinding(
                file,
                summary: "Direct HttpClient instantiation",
                evidence: $"Line {line.LineNumber}: {content.Trim()}",
                whyItMatters: "Directly instantiating HttpClient bypasses the socket pool managed by IHttpClientFactory, causing socket exhaustion under load.",
                suggestedAction: "Use IHttpClientFactory.CreateClient() or typed clients registered in the DI container.",
                confidence: Confidence.High,
                line: line));
        }
    }

    private void CheckMissingTimeout(DiffFile file, List<Finding> findings)
    {
        var addedLines = file.AddedLines.ToList();

        // Only flag files that directly instantiate a new HttpClient; using
        // an injected/pre-existing client means timeout is someone else's responsibility.
        bool hasNewHttpClient = addedLines.Any(l =>
            l.Content.Contains("new HttpClient(", StringComparison.Ordinal));

        if (!hasNewHttpClient) return;

        // Code that configures HttpClient via factory (IHttpClientFactory / AddHttpClient)
        // manages timeout at the channel/handler level: not via client.Timeout directly.
        bool isFactoryConfig = addedLines.Any(l =>
            l.Content.Contains("IHttpClientFactory", StringComparison.Ordinal)
            || l.Content.Contains("AddHttpClient", StringComparison.Ordinal)
            || l.Content.Contains("HttpClientFactoryOptions", StringComparison.Ordinal));

        if (isFactoryConfig) return;

        // gRPC channels manage timeouts at the channel/connection level via GrpcChannelOptions.
        // HttpClient is typically wrapping a gRPC handler, so per-client timeout is not applicable.
        bool usesGrpcChannel = addedLines.Any(l =>
            l.Content.Contains("GrpcChannel", StringComparison.Ordinal)
            || l.Content.Contains("ChannelOptions", StringComparison.Ordinal)
            || l.Content.Contains("GrpcChannelOptions", StringComparison.Ordinal)
            || l.Content.Contains("HttpClientHandler", StringComparison.Ordinal)
                && addedLines.Any(hl => hl.Content.Contains("GrpcChannel", StringComparison.Ordinal)));

        if (usesGrpcChannel) return;

        bool hasTimeoutConfig = addedLines.Any(l =>
            l.Content.Contains(".Timeout =")
            || l.Content.Contains("TimeoutPolicy")
            || l.Content.Contains("timeout", StringComparison.OrdinalIgnoreCase));

        if (!hasTimeoutConfig)
        {
            findings.Add(CreateFinding(
                file,
                summary: "HttpClient used without explicit timeout",
                evidence: $"File {file.NewPath} adds HttpClient usage with no timeout configuration.",
                whyItMatters: "HttpClient has a default timeout of 100 seconds. Without explicit configuration, slow external services can exhaust thread pool resources.",
                suggestedAction: "Set an explicit Timeout on the HttpClient or configure a timeout policy via Polly/Refit.",
                confidence: Confidence.Medium));
        }
    }

    private void CheckMissingCancellationToken(DiffFile file, List<Finding> findings)
    {
        var addedLines = file.AddedLines.ToList();

        // If this file uses factory-managed or injected HTTP clients, skip CancellationToken checks
        // (timeout is managed at factory/handler level, not per-call)
        if (UsesFactoryManagedClients(addedLines))
            return;

        foreach (var line in addedLines)
        {
            var content = line.Content;
            if (content.TrimStart().StartsWith("//")) continue;

            bool hasHttpCall = CtCheckHttpMethods.Any(m => content.Contains(m));
            if (!hasHttpCall) continue;

            // Skip if this is a static/injected client being reused (pattern: _client.GetAsync)
            if (IsInjectedOrStaticClient(content))
                continue;

            bool hasCancellationToken =
                content.Contains("cancellationToken")
                || content.Contains("CancellationToken")
                || content.Contains("ct)");

            if (!hasCancellationToken)
            {
                findings.Add(CreateFinding(
                    file,
                    summary: "HTTP call missing CancellationToken",
                    evidence: $"Line {line.LineNumber}: {content.Trim()}",
                    whyItMatters: "Without propagating CancellationToken, cancelled requests continue executing on the server, wasting resources.",
                    suggestedAction: "Pass the CancellationToken from the calling method to all async HTTP operations.",
                    confidence: Confidence.Low,
                    line: line));
            }
        }
    }

    private static bool UsesFactoryManagedClients(List<DiffLine> addedLines)
    {
        var factoryPatterns = new[]
        {
            "IHttpClientFactory", "AddHttpClient", "HttpClientFactoryOptions",
            "AddPolicyHandler", "AddTransientHttpErrorPolicy", "Polly"
        };

        return addedLines.Any(l =>
            factoryPatterns.Any(p => l.Content.Contains(p, StringComparison.Ordinal)));
    }

    private static bool IsInjectedOrStaticClient(string content)
    {
        // Pattern: _httpClient.GetAsync, this.client.PostAsync, httpClient.SendAsync
        // These are typically injected via DI or stored as static/field
        var injectionPatterns = new[]
        {
            "_httpClient", "_client", "this.client", "this._client",
            "httpClient.", "_http.", "HttpClient."
        };

        return injectionPatterns.Any(p =>
            content.Contains(p, StringComparison.Ordinal) &&
            CtCheckHttpMethods.Any(m => content.Contains(m)));
    }

    private static bool IsGrpcRelatedFile(string path)
    {
        // gRPC files manage connection timeout at the channel level, not per-HttpClient
        return path.Contains("grpc", StringComparison.OrdinalIgnoreCase)
            || path.Contains("channel", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase); // gRPC generated code
    }
}

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
    public GCI0039_ExternalServiceSafety(IPatternProvider patterns) : base(patterns)
    {
    }
    public override string Id => "GCI0039";
    public override string Name => "External Service Safety";

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var diff = context.Diff;
        var findings = new List<Finding>();

        foreach (var file in diff.Files)
        {
            if (WellKnownPatterns.IsTestFile(file.NewPath)) continue;

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
        if (WellKnownPatterns.IsGrpcRelatedFile(file.NewPath)) return;

        foreach (var line in file.AddedLines)
        {
            var content = line.Content;
            if (content.TrimStart().StartsWith("//")) continue;
            
            // Skip mock HTTP clients in test code
            if (WellKnownPatterns.HasMockPattern(content)) continue;
            
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
        if (WellKnownPatterns.IsHttpFactoryConfigured(addedLines))
            return;

        // gRPC channels manage timeouts at the channel/connection level via GrpcChannelOptions.
        // HttpClient is typically wrapping a gRPC handler, so per-client timeout is not applicable.
        if (WellKnownPatterns.UsesGrpcChannel(addedLines))
            return;

        // Skip if Polly resilience policies already handle timeouts
        if (HasPollyTimeoutPolicy(addedLines))
            return;

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

            bool hasHttpCall = WellKnownPatterns.ExternalServicePatterns.CtCheckHttpMethods.Any(m => content.Contains(m));
            if (!hasHttpCall) continue;

            // Skip if this is a static/injected client being reused (pattern: _client.GetAsync)
            if (IsInjectedOrStaticClient(content))
                continue;

            // Skip fire-and-forget patterns (intentional ignoring of async result)
            if (IsFireAndForgetPattern(content))
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

    /// <summary>
    /// Returns true if the code contains Polly resilience patterns that manage timeouts.
    /// </summary>
    private static bool HasPollyTimeoutPolicy(List<DiffLine> addedLines)
    {
        return WellKnownPatterns.ExternalServicePatterns.PollyPatterns.Any(pattern =>
            addedLines.Any(l => l.Content.Contains(pattern, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Returns true if this line is a fire-and-forget async pattern where CancellationToken isn't needed.
    /// Examples: "_ = await GetAsync()" or ".FireAndForget()"
    /// </summary>
    private static bool IsFireAndForgetPattern(string content)
    {
        return WellKnownPatterns.ExternalServicePatterns.FireAndForgetPatterns.Any(pattern =>
            content.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    private static bool UsesFactoryManagedClients(List<DiffLine> addedLines)
    {
        return WellKnownPatterns.UsesFactoryManagedHttpClients(addedLines);
    }

    private static bool IsInjectedOrStaticClient(string content)
    {
        return WellKnownPatterns.IsInjectedOrStaticClient(content);
    }

    private static bool IsGrpcRelatedFile(string path)
    {
        return WellKnownPatterns.IsGrpcRelatedFile(path);
    }
}


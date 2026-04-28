// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0039 – External Service Safety
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
        // manages timeout at the channel/handler level — not via client.Timeout directly.
        bool isFactoryConfig = addedLines.Any(l =>
            l.Content.Contains("IHttpClientFactory", StringComparison.Ordinal)
            || l.Content.Contains("AddHttpClient", StringComparison.Ordinal)
            || l.Content.Contains("HttpClientFactoryOptions", StringComparison.Ordinal));

        if (isFactoryConfig) return;

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

        foreach (var line in addedLines)
        {
            var content = line.Content;
            if (content.TrimStart().StartsWith("//")) continue;

            bool hasHttpCall = CtCheckHttpMethods.Any(m => content.Contains(m));
            if (!hasHttpCall) continue;

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
}

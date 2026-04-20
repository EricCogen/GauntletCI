// SPDX-License-Identifier: Elastic-2.0
using System.Text.RegularExpressions;
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.StaticAnalysis;
using GauntletCI.Core.Rules;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0024 – Resource Lifecycle
/// Detects disposable resources allocated without a using statement or try/finally disposal.
/// Covers both explicit known types (FileStream, SqlConnection, …) and any type whose name
/// ends with a disposable suffix (Stream, Reader, Writer, Connection, Client, etc.).
/// Absorbs GCI0030 detection scope; GCI0030 is now superseded by this rule.
/// </summary>
public class GCI0024_ResourceLifecycle : RuleBase
{
    public override string Id => "GCI0024";
    public override string Name => "Resource Lifecycle";

    // Explicit known-type prefixes (fast path)
    private static readonly string[] DisposableTypes =
    [
        "new FileStream(", "new StreamWriter(", "new StreamReader(", "new MemoryStream(",
        "new SqlConnection(", "new SqlCommand(", "new SqlDataReader(",
        "new HttpClient(", "new TcpClient(", "new UdpClient(", "new Socket(",
        "new Mutex(", "new Semaphore(", "new SemaphoreSlim(",
        "new EventWaitHandle(", "new ManualResetEvent(",
        "new BinaryWriter(", "new BinaryReader(",
        "new GZipStream(", "new DeflateStream(", "new CryptoStream(",
        "new X509Certificate(", "new RSACryptoServiceProvider("
    ];

    // Suffix-based heuristic (from GCI0030) — catches any type whose name ends in these.
    // Note: "Command" is intentionally excluded; SqlCommand is covered by DisposableTypes above,
    // and System.CommandLine.Command is not IDisposable — including the suffix causes FPs.
    private static readonly string[] DisposableSuffixes =
    [
        "Stream", "Reader", "Writer", "Connection", "Client",
        "Listener", "Channel", "Context", "Provider", "Session", "Transaction",
        "Certificate", "Scope", "Timer", "Enumerator"
    ];

    // Types that end with a disposable-looking suffix but do NOT implement IDisposable.
    // The suffix heuristic is skipped for these to avoid false positives.
    private static readonly HashSet<string> KnownNonDisposableTypes = new(StringComparer.Ordinal)
    {
        // Microsoft.CodeAnalysis / Roslyn analysis context types
        "SyntaxContext", "AnalysisContext", "SemanticContext",
        "SyntaxNodeAnalysisContext", "OperationAnalysisContext", "CodeBlockAnalysisContext",
        // System.CommandLine types
        "InvocationContext",
        // ASP.NET Core filter/action context types (not disposable on their own)
        "HttpContext", "RouteContext", "FilterContext", "ActionContext",
        "AuthorizationFilterContext", "ResourceExecutingContext", "ResourceExecutedContext",
        "ResultExecutingContext", "ResultExecutedContext", "ExceptionContext",
        // Other common non-disposable context types
        "ValidationContext", "NavigationContext",
    };

    private static readonly Regex NewTypeRegex =
        new(@"new ([A-Z][A-Za-z0-9]+)\(", RegexOptions.Compiled);

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var diff = context.Diff;
        var findings = new List<Finding>();

        foreach (var file in diff.Files)
        {
            CheckUnguardedDisposables(file, findings);
        }

        AddRoslynFindings(context.StaticAnalysis, findings);

        return Task.FromResult(findings);
    }

    private void CheckUnguardedDisposables(DiffFile file, List<Finding> findings)
    {
        if (WellKnownPatterns.IsTestFile(file.NewPath)) return;
        if (WellKnownPatterns.IsGeneratedFile(file.NewPath)) return;

        var allLines = file.Hunks.SelectMany(h => h.Lines).ToList();

        for (int i = 0; i < allLines.Count; i++)
        {
            var line = allLines[i];
            if (line.Kind != DiffLineKind.Added) continue;
            var content = line.Content;

            var (typeName, isExplicit) = MatchDisposableType(content);
            if (typeName is null) continue;

            if (typeName == "HttpClient" && LooksLikeFactoryProvidedHttpClient(allLines, i))
                continue;

            if (content.Contains("using ", StringComparison.Ordinal)) continue;

            bool prevHasUsing = false;
            for (int j = i - 1; j >= Math.Max(0, i - 3); j--)
            {
                var prev = allLines[j].Content.Trim();
                if (string.IsNullOrWhiteSpace(prev)) continue;
                if (prev.StartsWith("using ") || prev.StartsWith("await using "))
                { prevHasUsing = true; break; }
                break;
            }
            if (prevHasUsing) continue;

            int winStart = Math.Max(0, i - 2);
            int winEnd = Math.Min(allLines.Count, i + 20);
            bool hasDispose = allLines[winStart..winEnd].Any(l =>
                l.Content.Contains(".Dispose()", StringComparison.Ordinal) ||
                l.Content.Contains("finally", StringComparison.Ordinal));
            if (hasDispose) continue;

            findings.Add(CreateFinding(
                file,
                summary: $"{typeName} allocated without using statement in {file.NewPath}.",
                evidence: $"Line {line.LineNumber}: {content.Trim()}",
                whyItMatters: $"{typeName} implements IDisposable. Without using, it leaks OS handles or connection pool slots under exceptions.",
                suggestedAction: $"Wrap in `using var resource = new {typeName}(...);` to guarantee disposal.",
                confidence: isExplicit ? Confidence.High : Confidence.Medium,
                line: line));
        }
    }

    private static bool LooksLikeFactoryProvidedHttpClient(List<DiffLine> allLines, int idx)
    {
        int start = Math.Max(0, idx - 20);
        int end = Math.Min(allLines.Count, idx + 20);

        return allLines[start..end].Any(l =>
            l.Content.Contains("CreateClient(", StringComparison.Ordinal) ||
            l.Content.Contains("_httpClientFactory", StringComparison.Ordinal) ||
            l.Content.Contains("IHttpClientFactory", StringComparison.Ordinal));
    }

    private static (string? TypeName, bool IsExplicit) MatchDisposableType(string content)
    {
        // Fast path: explicit known types — High confidence
        foreach (var knownType in DisposableTypes)
        {
            if (content.Contains(knownType, StringComparison.Ordinal))
                return (knownType.Replace("new ", "").TrimEnd('('), true);
        }

        // Suffix heuristic — Medium confidence
        var match = NewTypeRegex.Match(content);
        if (match.Success)
        {
            var name = match.Groups[1].Value;
            foreach (var suffix in DisposableSuffixes)
            {
                if (name.EndsWith(suffix, StringComparison.Ordinal))
                {
                    // Skip types known NOT to be disposable despite having a disposable-looking suffix
                    if (KnownNonDisposableTypes.Contains(name)) return (null, false);
                    return (name, false);
                }
            }
        }

        return (null, false);
    }

    private static void AddRoslynFindings(AnalyzerResult? staticAnalysis, List<Finding> findings)
    {
        if (staticAnalysis is null) return;
        foreach (var diag in staticAnalysis.Diagnostics.Where(d => d.Id is "CA2000" or "CA1001" or "CA2213"))
        {
            findings.Add(new Finding
            {
                RuleId = "GCI0024",
                RuleName = "Resource Lifecycle",
                Summary = $"{diag.Id}: {diag.Message}",
                Evidence = $"{diag.FilePath}:{diag.Line}",
                WhyItMatters = "Roslyn detected a resource that may not be properly disposed.",
                SuggestedAction = "Use a using statement or implement IDisposable correctly to ensure deterministic cleanup.",
                Confidence = Confidence.High,
            });
        }
    }
}

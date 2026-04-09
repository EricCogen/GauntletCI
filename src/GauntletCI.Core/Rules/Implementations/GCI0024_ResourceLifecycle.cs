// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.StaticAnalysis;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0024 – Resource Lifecycle
/// Detects disposable resources allocated without a using statement or try/finally disposal.
/// </summary>
public class GCI0024_ResourceLifecycle : RuleBase
{
    public override string Id => "GCI0024";
    public override string Name => "Resource Lifecycle";

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

    public override Task<List<Finding>> EvaluateAsync(
        DiffContext diff, AnalyzerResult? staticAnalysis, CancellationToken ct = default)
    {
        var findings = new List<Finding>();

        foreach (var file in diff.Files.Where(f => f.NewPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
        {
            CheckUnguardedDisposables(file, findings);
        }

        AddRoslynFindings(staticAnalysis, findings);

        return Task.FromResult(findings);
    }

    private void CheckUnguardedDisposables(DiffFile file, List<Finding> findings)
    {
        var allLines = file.Hunks.SelectMany(h => h.Lines).ToList();

        for (int i = 0; i < allLines.Count; i++)
        {
            var line = allLines[i];
            if (line.Kind != DiffLineKind.Added) continue;
            var content = line.Content;

            string? matched = null;
            foreach (var type in DisposableTypes)
            {
                if (content.Contains(type, StringComparison.OrdinalIgnoreCase))
                { matched = type; break; }
            }
            if (matched is null) continue;

            // Safe if: line itself contains "using " or "await using"
            if (content.Contains("using ", StringComparison.Ordinal)) continue;

            // Safe if preceded by "using" on previous non-blank added/context line
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

            // Safe if there's a .Dispose() or try/finally in the surrounding window
            int winStart = Math.Max(0, i - 2);
            int winEnd = Math.Min(allLines.Count, i + 20);
            bool hasDispose = allLines[winStart..winEnd].Any(l =>
                l.Content.Contains(".Dispose()", StringComparison.Ordinal) ||
                l.Content.Contains("finally", StringComparison.Ordinal));
            if (hasDispose) continue;

            var typeName = matched.Replace("new ", "").TrimEnd('(');
            findings.Add(CreateFinding(
                summary: $"{typeName} allocated without using statement in {file.NewPath}.",
                evidence: $"Line {line.LineNumber}: {content.Trim()}",
                whyItMatters: $"{typeName} implements IDisposable. If not disposed, it leaks OS handles, file locks, or connection pool slots — especially under exceptions.",
                suggestedAction: "Wrap in a using statement: using var resource = new " + typeName + "(...); — this guarantees disposal even when exceptions occur.",
                confidence: Confidence.High));
        }
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

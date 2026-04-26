// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0022 – Idempotency &amp; Retry Safety
/// Detects HTTP POST endpoints without idempotency keys, raw INSERT without upsert guards,
/// and event handler registrations without deduplication.
/// </summary>
public class GCI0022_IdempotencyRetrySafety : RuleBase
{
    public override string Id => "GCI0022";
    public override string Name => "Idempotency & Retry Safety";

    private static readonly string[] IdempotencySignals =
    [
        "IdempotencyKey", "Idempotency-Key", "idempotencyKey", "idempotent",
        "dedup", "Dedup", "RequestId", "requestId", "MessageId", "messageId"
    ];

    private static readonly string[] UpsertPatterns =
    [
        "ON DUPLICATE KEY", "ON CONFLICT", "INSERT OR REPLACE",
        "INSERT OR IGNORE", "MERGE INTO", "UPSERT"
    ];

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var diff = context.Diff;
        var findings = new List<Finding>();

        foreach (var file in diff.Files)
        {
            CheckHttpPostWithoutIdempotency(file, findings);
            CheckEventHandlerWithoutDedup(file, findings);
            CheckRawInsertWithoutUpsert(file, findings);
        }

        return Task.FromResult(findings);
    }

    private void CheckHttpPostWithoutIdempotency(DiffFile file, List<Finding> findings)
    {
        var allLines = file.Hunks.SelectMany(h => h.Lines).ToList();

        for (int i = 0; i < allLines.Count; i++)
        {
            var line = allLines[i];
            if (line.Kind != DiffLineKind.Added) continue;
            var content = line.Content.Trim();

            if (!content.Equals("[HttpPost]", StringComparison.Ordinal) &&
                !content.Equals("[HttpPost(\"\")]", StringComparison.Ordinal) &&
                !content.StartsWith("[HttpPost(", StringComparison.Ordinal)) continue;

            // Look in a window around this line for idempotency signals
            int start = Math.Max(0, i - 2);
            int end = Math.Min(allLines.Count, i + 25);
            var window = allLines[start..end].Select(l => l.Content);

            bool hasIdempotency = window.Any(l =>
                IdempotencySignals.Any(sig => l.Contains(sig, StringComparison.OrdinalIgnoreCase)));

            if (!hasIdempotency)
            {
                findings.Add(CreateFinding(
                    file,
                    summary: $"[HttpPost] endpoint in {file.NewPath} has no idempotency key handling.",
                    evidence: $"Line {line.LineNumber}: {content}",
                    whyItMatters: "Non-idempotent POST endpoints executed multiple times (retries, duplicate submissions) can create duplicate records or double-charge customers.",
                    suggestedAction: "Add an idempotency key header (e.g. Idempotency-Key), validate it server-side, and cache the response for duplicate requests.",
                    confidence: Confidence.Medium,
                    line: line));
            }
        }
    }

    private void CheckRawInsertWithoutUpsert(DiffFile file, List<Finding> findings)
    {
        foreach (var line in file.AddedLines)
        {
            var content = line.Content;
            if (!content.Contains("INSERT INTO", StringComparison.OrdinalIgnoreCase)) continue;

            // Check if this line or nearby lines have upsert protection
            bool hasUpsert = UpsertPatterns.Any(p => content.Contains(p, StringComparison.OrdinalIgnoreCase));
            if (!hasUpsert)
            {
                findings.Add(CreateFinding(
                    file,
                    summary: $"Raw INSERT without upsert guard in {file.NewPath}.",
                    evidence: $"Line {line.LineNumber}: {content.Trim()}",
                    whyItMatters: "Plain INSERT statements fail or create duplicates on retry. Retried operations (network errors, message queue redelivery) need safe insert semantics.",
                    suggestedAction: "Use INSERT OR IGNORE / ON CONFLICT DO NOTHING / UPSERT / MERGE, or add a unique constraint with application-level duplicate detection.",
                    confidence: Confidence.Medium,
                    line: line));
            }
        }
    }

    private void CheckEventHandlerWithoutDedup(DiffFile file, List<Finding> findings)
    {
        // Test files intentionally exercise event subscription patterns — skip them
        if (WellKnownPatterns.IsTestFile(file.NewPath)) return;

        var allLines = file.Hunks.SelectMany(h => h.Lines).ToList();

        for (int i = 0; i < allLines.Count; i++)
        {
            var line = allLines[i];
            if (line.Kind != DiffLineKind.Added) continue;
            var content = line.Content.Trim();

            // Event subscription pattern: "SomeEvent += Handler;"
            if (!content.Contains(" += ") || !content.EndsWith(';')) continue;
            if (!content.Contains("Event") && !content.Contains("Handler") &&
                !content.Contains("Listener") && !content.Contains("Callback")) continue;

            // Generic typed event channels (e.g. MessageBus<T>.Subscribers +=) are intentional
            // single-registration patterns at startup — deduplication concern does not apply.
            {
                int plusEq = content.IndexOf(" += ", StringComparison.Ordinal);
                int gtIdx  = content.IndexOf('>', StringComparison.Ordinal);
                if (gtIdx > 0 && gtIdx < plusEq) continue;
            }

            // Exempt += inside a static constructor (runs exactly once — inherently idempotent)
            if (IsInsideStaticConstructor(allLines, i)) continue;

            // Look for deduplication guard nearby (unsubscribe or bool guard)
            int start = Math.Max(0, i - 5);
            int end = Math.Min(allLines.Count, i + 10);
            var window = allLines[start..end].Select(l => l.Content);

            bool hasDedup = window.Any(l =>
                l.Contains(" -= ") ||
                l.Contains("_subscribed", StringComparison.Ordinal) ||
                l.Contains("_registered", StringComparison.Ordinal) ||
                l.Contains("_attached", StringComparison.Ordinal));

            if (!hasDedup)
            {
                findings.Add(CreateFinding(
                    file,
                    summary: $"Event handler registered without deduplication guard in {file.NewPath}.",
                    evidence: $"Line {line.LineNumber}: {content}",
                    whyItMatters: "Event handlers registered multiple times fire multiple times, causing duplicate side effects that are hard to debug.",
                    suggestedAction: "Unsubscribe before subscribing (-= then +=), or guard with a boolean flag to prevent duplicate registration.",
                    confidence: Confidence.Low,
                    line: line));
            }
        }
    }

    /// <summary>
    /// Returns true when the line at <paramref name="idx"/> appears to be inside a static constructor,
    /// which is inherently idempotent (runs exactly once per AppDomain lifetime).
    /// Detects the pattern <c>static ClassName()</c> within the preceding 20 lines.
    /// A static constructor has no return type, so the identifier immediately follows "static " with
    /// no whitespace-separated token between it and the opening parenthesis.
    /// </summary>
    private static bool IsInsideStaticConstructor(List<DiffLine> allLines, int idx)
    {
        int searchStart = Math.Max(0, idx - 20);
        for (int j = idx - 1; j >= searchStart; j--)
        {
            var trimmed = allLines[j].Content.Trim();
            if (!trimmed.StartsWith("static ")) continue;

            // Extract everything after "static " and trim leading whitespace
            var afterStatic = trimmed["static ".Length..].TrimStart();

            // Find the first '(' — everything before it must contain NO whitespace.
            // Static constructor: "static TypeName(" — one token before '('
            // Static method:      "static ReturnType MethodName(" — two tokens before '('
            var parenIdx = afterStatic.IndexOf('(');
            if (parenIdx < 0) continue;

            var beforeParen = afterStatic[..parenIdx].TrimEnd(); // trim trailing whitespace (e.g. "static Foo ()")
            if (beforeParen.Contains(' ') || beforeParen.Contains('\t')) continue;

            return true;
        }
        return false;
    }
}

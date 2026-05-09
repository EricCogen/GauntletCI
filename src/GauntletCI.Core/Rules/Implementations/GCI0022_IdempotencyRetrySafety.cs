// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0022, Idempotency &amp; Retry Safety
/// Detects HTTP POST endpoints without idempotency keys, raw INSERT without upsert guards,
/// and event handler registrations without deduplication.
/// </summary>
public class GCI0022_IdempotencyRetrySafety : RuleBase
{
    public GCI0022_IdempotencyRetrySafety(IPatternProvider patterns) : base(patterns)
    {
    }
    public override string Id => "GCI0022";
    public override string Name => "Idempotency & Retry Safety";

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
        // Skip test files - test endpoints don't need production-level idempotency
        if (WellKnownPatterns.IsTestFile(file.NewPath))
            return;

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
                WellKnownPatterns.IdempotencyPatterns.IdempotencySignals.Any(sig => l.Contains(sig, StringComparison.OrdinalIgnoreCase)));

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
        // Skip migration and seed data files - they use raw INSERT intentionally
        if (WellKnownPatterns.GuardPatterns.IsMigrationOrSeedFile(file.NewPath))
            return;

        foreach (var line in file.AddedLines)
        {
            var content = line.Content;
            if (!content.Contains("INSERT INTO", StringComparison.OrdinalIgnoreCase)) continue;

            // Skip benign INSERT patterns that don't need upsert protection:
            // - SELECT INTO (copying data structure)
            // - INSERT ... DEFAULT (schema-only, no actual data)
            if (content.Contains("SELECT", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("DEFAULT", StringComparison.OrdinalIgnoreCase))
                continue;

            // Check if this line or nearby lines have upsert protection
            bool hasUpsert = WellKnownPatterns.IdempotencyPatterns.UpsertPatterns.Any(p => content.Contains(p, StringComparison.OrdinalIgnoreCase));
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
        var allLines = file.Hunks.SelectMany(h => h.Lines).ToList();

        for (int i = 0; i < allLines.Count; i++)
        {
            var line = allLines[i];
            if (line.Kind != DiffLineKind.Added) continue;
            var content = line.Content.Trim();

            // Event subscription pattern: "SomeEvent += Handler;"
            if (!content.Contains(" += ") || !content.EndsWith(';')) continue;
            var contentLower = content.ToLowerInvariant();
            if (!contentLower.Contains("event") && !contentLower.Contains("handler") &&
                !contentLower.Contains("listener") && !contentLower.Contains("callback")) continue;

            // Exempt += inside a static constructor (runs exactly once -- inherently idempotent)
            if (WellKnownPatterns.GuardPatterns.IsInsideStaticConstructor(allLines, i)) continue;

            // Exempt if in UI/XAML context (WPF, WinUI events are often attached once per control lifecycle)
            if (WellKnownPatterns.GuardPatterns.IsUiEventHandler(file.NewPath)) continue;

            // Exempt MVVM View/ViewModel files - event subscriptions in these files are typically
            // done in constructor or initialization, which runs once per instance
            if (IsMvvmComponentFile(file.NewPath)) continue;

            // Look for deduplication guard nearby (unsubscribe or bool guard)
            int start = Math.Max(0, i - 5);
            int end = Math.Min(allLines.Count, i + 10);
            var window = allLines[start..end].Select(l => l.Content);

            bool hasDedup = window.Any(l =>
                l.Contains(" -= ") ||
                l.Contains("_subscribed", StringComparison.Ordinal) ||
                l.Contains("_registered", StringComparison.Ordinal) ||
                l.Contains("_attached", StringComparison.Ordinal) ||
                l.Contains("_initialized", StringComparison.Ordinal));

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
    /// Returns true if the file is a MVVM View or ViewModel component.
    /// These files typically initialize event handlers in constructors which run once per instance.
    /// </summary>
    private static bool IsMvvmComponentFile(string path)
    {
        return path.Contains("ViewModel", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith("View.cs", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith("Presenter.cs", StringComparison.OrdinalIgnoreCase) ||
               path.Contains(".xaml.cs", StringComparison.OrdinalIgnoreCase);
    }
}


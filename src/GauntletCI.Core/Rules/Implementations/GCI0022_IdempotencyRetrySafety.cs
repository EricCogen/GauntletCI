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
        if (IsMigrationOrSeedFile(file.NewPath))
            return;

        foreach (var line in file.AddedLines)
        {
            var content = line.Content;
            if (!content.Contains("INSERT INTO", StringComparison.OrdinalIgnoreCase)) continue;

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
            if (IsInsideStaticConstructor(allLines, i)) continue;

            // Exempt if in UI/XAML context (WPF, WinUI events are often attached once per control lifecycle)
            if (IsUiEventHandler(file.NewPath)) continue;

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

    private static bool IsMigrationOrSeedFile(string filePath)
    {
        // Migration files: EF Core migrations directory
        if (filePath.Contains("Migrations/", StringComparison.OrdinalIgnoreCase) ||
            filePath.Contains("\\Migrations\\", StringComparison.OrdinalIgnoreCase))
            return true;

        // Migration SQL scripts
        if (filePath.EndsWith(".sql", StringComparison.OrdinalIgnoreCase) &&
            (filePath.Contains("Migration", StringComparison.OrdinalIgnoreCase) ||
             filePath.Contains("Seed", StringComparison.OrdinalIgnoreCase) ||
             filePath.Contains("Setup", StringComparison.OrdinalIgnoreCase)))
            return true;

        // EF seed configurations (ModelBuilder.Entity.HasData)
        if (filePath.Contains("SeedData", StringComparison.OrdinalIgnoreCase) ||
            filePath.Contains("DataSeeding", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    /// <summary>
    /// Returns true when the line at <paramref name="idx"/> appears to be inside a static constructor,
    /// which is inherently idempotent (runs exactly once per AppDomain lifetime).
    /// Detects the pattern "static ClassName()" or "static()" within the preceding 20 lines.
    /// </summary>
    private static bool IsInsideStaticConstructor(List<DiffLine> allLines, int idx)
    {
        int searchStart = Math.Max(0, idx - 20);
        for (int j = idx - 1; j >= searchStart; j--)
        {
            var trimmed = allLines[j].Content.Trim();
            if (!trimmed.StartsWith("static ", StringComparison.Ordinal)) continue;

            var afterStatic = trimmed["static ".Length..].TrimStart();
            // If the next token is a C# keyword that can be a return type, it's a method not a constructor
            if (afterStatic.StartsWith("void ", StringComparison.Ordinal) ||
                afterStatic.StartsWith("Task", StringComparison.Ordinal) ||
                afterStatic.StartsWith("bool ", StringComparison.Ordinal) ||
                afterStatic.StartsWith("int ", StringComparison.Ordinal) ||
                afterStatic.StartsWith("string ", StringComparison.Ordinal) ||
                afterStatic.StartsWith("async ", StringComparison.Ordinal) ||
                afterStatic.StartsWith("readonly ", StringComparison.Ordinal) ||
                afterStatic.StartsWith("class ", StringComparison.Ordinal) ||
                afterStatic.StartsWith("IEnumerable", StringComparison.Ordinal) ||
                afterStatic.StartsWith("List<", StringComparison.Ordinal) ||
                afterStatic.StartsWith("Dictionary<", StringComparison.Ordinal))
                continue;

            // The remainder should look like "TypeName()" - contains "()"
            if (afterStatic.Contains("()")) return true;
        }
        return false;
    }

    /// <summary>
    /// Returns true if the file is a UI/XAML context (WPF, WinUI, Blazor)
    /// where event handler registration patterns are often benign.
    /// </summary>
    private static bool IsUiEventHandler(string filePath)
    {
        var lower = filePath.ToLowerInvariant();
        // XAML code-behind files
        if (lower.EndsWith(".xaml.cs", StringComparison.OrdinalIgnoreCase)) return true;
        // Blazor component files
        if (lower.EndsWith(".razor.cs", StringComparison.OrdinalIgnoreCase)) return true;
        // Common UI namespaces
        if (lower.Contains("\\ui\\") || lower.Contains("/ui/") ||
            lower.Contains("\\components\\") || lower.Contains("/components/") ||
            lower.Contains("\\views\\") || lower.Contains("/views/") ||
            lower.Contains("\\pages\\") || lower.Contains("/pages/")) return true;

        return false;
    }
}

// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Core.Model;

/// <summary>
/// Collapses repeated <see cref="Finding"/> entries from the same rule against the same file
/// into a single <see cref="GroupedFinding"/>. Findings that share <c>(RuleId, FilePath)</c>
/// are merged: their lines and evidence are aggregated; summary / why / action are taken from
/// the first occurrence (rules emit identical narrative for repeats).
/// </summary>
public static class FindingGrouper
{
    /// <summary>Groups <paramref name="findings"/> by (RuleId, FilePath ?? "") preserving first-seen order.</summary>
    public static List<GroupedFinding> Group(IEnumerable<Finding> findings)
    {
        ArgumentNullException.ThrowIfNull(findings);

        var entries = new Dictionary<string, GroupAccumulator>(StringComparer.Ordinal);
        var order = 0;

        foreach (var f in findings)
        {
            var key = $"{f.RuleId}|{f.FilePath ?? string.Empty}";
            if (!entries.TryGetValue(key, out var acc))
            {
                acc = new GroupAccumulator(f, order++);
                entries[key] = acc;
            }
            else
            {
                acc.Add(f);
            }
        }

        return entries.Values
            .OrderBy(g => g.Order)
            .Select(g => g.Build())
            .ToList();
    }

    private sealed class GroupAccumulator
    {
        public int Order { get; }
        private readonly Finding _first;
        private readonly List<Finding> _all = new();

        public GroupAccumulator(Finding first, int order)
        {
            _first = first;
            Order = order;
            _all.Add(first);
        }

        public void Add(Finding f) => _all.Add(f);

        public GroupedFinding Build()
        {
            var lines = _all.Where(f => f.Line.HasValue)
                            .Select(f => f.Line!.Value)
                            .Distinct()
                            .OrderBy(x => x)
                            .ToList();

            var evidence = _all.Select(f => f.Evidence ?? string.Empty)
                               .Where(e => !string.IsNullOrWhiteSpace(e))
                               .Distinct(StringComparer.Ordinal)
                               .ToList();

            return new GroupedFinding
            {
                RuleId          = _first.RuleId,
                RuleName        = _first.RuleName,
                Summary         = _first.Summary,
                WhyItMatters    = _first.WhyItMatters,
                SuggestedAction = _first.SuggestedAction,
                Confidence      = _first.Confidence,
                Severity        = _first.Severity,
                FilePath        = _first.FilePath,
                PrimaryLine     = lines.Count > 0 ? lines[0] : _first.Line,
                Lines           = lines,
                Evidence        = evidence,
                Count           = _all.Count,
                LlmExplanation  = _first.LlmExplanation,
                ExpertContext   = _first.ExpertContext,
                CodeSnippet     = _first.CodeSnippet,
                CoverageNote    = _first.CoverageNote,
                TicketContext   = _first.TicketContext,
            };
        }
    }
}

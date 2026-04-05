namespace GauntletCI.Benchmarks;

/// <summary>
/// Maps legacy PreCommitGuard (PCG) rule IDs to GauntletCI (GCI) rule IDs.
/// PCG0007–PCG0016 are direct numeric matches (GCI007–GCI016).
/// All others shift due to restructuring during the GCI redesign.
/// </summary>
public static class PcgToGciRuleMap
{
    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        // PCG0001 fired when a guard clause was added without test changes → Test Coverage Relevance
        ["PCG0001"] = "GCI005",

        // PCG0002 fired on modified conditional logic → Behavioral Change Detection
        ["PCG0002"] = "GCI003",

        // PCG0003 fired on null check additions/removals → Edge Case Handling
        ["PCG0003"] = "GCI006",

        // PCG0004 fired on async/await pattern shifts and blocking calls → Concurrency and State Risk
        ["PCG0004"] = "GCI016",

        // PCG0005 fired on public method signature changes → Breaking Change Risk
        ["PCG0005"] = "GCI004",

        // PCG0006 fired on missing edge-case validation → Edge Case Handling (same concept as PCG0003)
        ["PCG0006"] = "GCI006",

        // PCG0007–PCG0016: direct 1-to-1 name and number match
        ["PCG0007"] = "GCI007", // Error Handling Integrity
        ["PCG0008"] = "GCI008", // Complexity Control
        ["PCG0009"] = "GCI009", // Consistency with Existing Patterns
        ["PCG0010"] = "GCI010", // Hardcoding and Configuration
        ["PCG0011"] = "GCI011", // Performance Risk
        ["PCG0012"] = "GCI012", // Security Risk
        ["PCG0013"] = "GCI013", // Observability and Debuggability
        ["PCG0014"] = "GCI014", // Rollback Safety
        ["PCG0015"] = "GCI015", // Data Integrity Risk
        ["PCG0016"] = "GCI016", // Concurrency and State Risk

        // PCG0017 (Scope Discipline) → Diff Integrity and Scope (renumbered to GCI001 in GCI)
        ["PCG0017"] = "GCI001",

        // PCG0018 (Production Readiness) → Production Readiness (renumbered to GCI017)
        ["PCG0018"] = "GCI017",

        // PCG0019 (Confidence and Evidence Discipline) → Test Coverage Relevance (closest GCI equivalent)
        ["PCG0019"] = "GCI005",

        // PCG0020 (Accountability Standard) → Accountability Standard (renumbered to GCI018)
        ["PCG0020"] = "GCI018",
    };

    /// <summary>
    /// Returns the GCI rule ID for the given PCG rule ID, or null if unmapped.
    /// </summary>
    public static string? ToGci(string pcgRuleId) =>
        Map.TryGetValue(pcgRuleId, out var gci) ? gci : null;

    /// <summary>
    /// Returns the GCI rule ID if the input looks like a PCG rule; otherwise returns the input unchanged.
    /// This allows fixture metadata to use either PCG or GCI IDs.
    /// </summary>
    public static string Normalize(string ruleId) =>
        ruleId.StartsWith("PCG", StringComparison.OrdinalIgnoreCase)
            ? ToGci(ruleId) ?? ruleId
            : ruleId;
}

namespace GauntletCI.Core.Models;

public sealed record EvaluationResult(
    int ExitCode,
    GateResult? BranchCurrencyGate,
    GateResult? TestPassageGate,
    IReadOnlyList<Finding> Findings,
    string? ErrorMessage,
    bool DiffTrimmed,
    string Model)
{
    public bool HasHighSeverity => Findings.Any(static finding => finding.Severity.Equals("high", StringComparison.OrdinalIgnoreCase));
}

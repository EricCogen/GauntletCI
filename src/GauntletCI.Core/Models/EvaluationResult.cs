// SPDX-License-Identifier: Elastic-2.0
// Copyright (c) Eric Cogen. All rights reserved.

namespace GauntletCI.Core.Models;

public sealed record EvaluationResult(
    int ExitCode,
    GateResult? BranchCurrencyGate,
    GateResult? TestPassageGate,
    IReadOnlyList<Finding> Findings,
    string? ErrorMessage,
    bool DiffTrimmed,
    string Model,
    DiffMetadata? DiffMetadata,
    int EvaluationDurationMs,
    string? WarningMessage = null,
    bool ModelStepSkipped = false)
{
    public bool HasHighSeverity => Findings.Any(static finding => finding.Severity.Equals("high", StringComparison.OrdinalIgnoreCase));
}

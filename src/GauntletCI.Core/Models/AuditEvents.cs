// SPDX-License-Identifier: Elastic-2.0
// Copyright (c) Eric Cogen. All rights reserved.

namespace GauntletCI.Core.Models;

public sealed record EvaluationAuditTrail(
    string SchemaVersion,
    Guid EvaluationId,
    DateTimeOffset CapturedAtUtc,
    ConfigResolvedEvent ConfigResolved,
    AnalysisCompletedEvent AnalysisCompleted,
    IReadOnlyList<RuleFiredEvent> RuleFirings);

public sealed record ConfigResolvedEvent(
    string EventId,
    DateTimeOffset OccurredAtUtc,
    string ConfigFormat,
    string Model,
    bool ModelRequired,
    bool TelemetryEnabled,
    bool TelemetryConsentRecorded,
    IReadOnlyList<string> DisabledRules,
    IReadOnlyList<string> BlockingRules,
    IReadOnlyList<string> PolicyReferences);

public sealed record AnalysisCompletedEvent(
    string EventId,
    DateTimeOffset OccurredAtUtc,
    int ExitCode,
    bool ModelStepSkipped,
    bool DiffTrimmed,
    bool HasError,
    int EvaluationDurationMs,
    string Model,
    bool BranchCurrencyPassed,
    bool TestPassagePassed,
    int FindingCount,
    DiffMetadata? DiffMetadata);

public sealed record RuleFiredEvent(
    string EventId,
    DateTimeOffset OccurredAtUtc,
    string RuleId,
    string RuleName,
    string Severity,
    string Confidence,
    string Evidence);

public sealed record ConfigChangedEvent(
    string EventId,
    DateTimeOffset OccurredAtUtc,
    string Setting,
    string? PreviousValue,
    string NewValue,
    string Source);

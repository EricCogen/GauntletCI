// SPDX-License-Identifier: Elastic-2.0

// ========== GauntletCI Self-Analysis: WHITELISTED FILE ==========
// This file contains pattern strings and regexes used by GauntletCI detection rules.
// Pattern strings (e.g., "new FileStream(", "TODO", ".GetService<") appear in the code by design.
// These are NOT actual code violations, but rather PATTERN DEFINITIONS used to detect violations in other code.
// GauntletCI Self-Analysis should skip this file from analysis to avoid false positives on pattern data.
// =================================================================

namespace GauntletCI.Core.Rules.Patterns;

/// <summary>
/// Patterns used to detect idempotency and retry safety issues.
/// </summary>
internal static class IdempotencyPatterns
{
    /// <summary>
    /// Idempotency key signals (headers, parameters, field names) indicating idempotent request handling.
    /// Used by GCI0022 to detect HTTP POST endpoints with idempotency key support.
    /// </summary>
    public static readonly string[] IdempotencySignals =
    [
        "IdempotencyKey", "Idempotency-Key", "idempotencyKey", "idempotent",
        "dedup", "Dedup", "RequestId", "requestId", "MessageId", "messageId",
        // Additional patterns for common retry frameworks
        "duplicateCheck", "checkDuplicate", "DuplicateKey", "duplicate_key",
        "uniqueRequest", "UniqueRequest", "uniqueId", "UniqueId",
        "idempotencyMode", "RequestHash", "request_hash", "traceId"
    ];

    /// <summary>
    /// SQL/database upsert patterns indicating conflict resolution for duplicate inserts.
    /// Used by GCI0022 to detect raw INSERT statements without upsert guards.
    /// </summary>
    public static readonly string[] UpsertPatterns =
    [
        "ON DUPLICATE KEY", "ON CONFLICT", "INSERT OR REPLACE",
        "INSERT OR IGNORE", "MERGE INTO", "UPSERT"
    ];
}

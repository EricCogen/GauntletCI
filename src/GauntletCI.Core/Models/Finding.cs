// SPDX-License-Identifier: Elastic-2.0
// Copyright (c) Eric Cogen. All rights reserved.

using System.Text.Json.Serialization;

namespace GauntletCI.Core.Models;

public sealed record Finding(
    [property: JsonPropertyName("rule_id")] string RuleId,
    [property: JsonPropertyName("rule_name")] string RuleName,
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("finding")] string FindingText,
    [property: JsonPropertyName("evidence")] string Evidence,
    [property: JsonPropertyName("why_it_matters")] string WhyItMatters,
    [property: JsonPropertyName("suggested_action")] string SuggestedAction,
    [property: JsonPropertyName("confidence")] string Confidence);

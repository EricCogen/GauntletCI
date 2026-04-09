// SPDX-License-Identifier: Elastic-2.0
using System.Text.Json.Serialization;

namespace GauntletCI.Cli.LlmDaemon;

internal sealed record DaemonRequest(
    [property: JsonPropertyName("op")]       string  Op,
    [property: JsonPropertyName("ruleId")]   string? RuleId   = null,
    [property: JsonPropertyName("ruleName")] string? RuleName = null,
    [property: JsonPropertyName("summary")]  string? Summary  = null,
    [property: JsonPropertyName("evidence")] string? Evidence = null);

internal sealed record DaemonResponse(
    [property: JsonPropertyName("ok")]     bool   Ok,
    [property: JsonPropertyName("result")] string Result = "");

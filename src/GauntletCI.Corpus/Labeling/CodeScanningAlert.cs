// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Corpus.Labeling;

/// <summary>
/// A single open BUG or VULNERABILITY alert returned by the GitHub Code Scanning API.
/// </summary>
public sealed class CodeScanningAlert
{
    /// <summary>"{owner}/{repo}", e.g. "dotnet/runtime".</summary>
    public string Repo { get; init; } = "";

    /// <summary>
    /// Repo-relative file path from <c>most_recent_instance.location.path</c>,
    /// e.g. "src/libraries/System.Net.Http/src/System/Net/Http/HttpClient.cs".
    /// No prefix stripping required - GitHub returns paths without a project-key prefix.
    /// </summary>
    public string FilePath { get; init; } = "";

    /// <summary>CodeQL rule ID, e.g. "cs/sql-injection".</summary>
    public string RuleId { get; init; } = "";

    /// <summary>Human-readable rule name.</summary>
    public string RuleName { get; init; } = "";

    /// <summary>error, warning, note, or none (from rule.severity).</summary>
    public string Severity { get; init; } = "";

    /// <summary>open, dismissed, or fixed.</summary>
    public string State { get; init; } = "";

    /// <summary>Tool that produced the alert (e.g. "CodeQL").</summary>
    public string ToolName { get; init; } = "";

    /// <summary>Alert message from <c>most_recent_instance.message.text</c>.</summary>
    public string Message { get; init; } = "";

    /// <summary>Line number of the finding in <see cref="FilePath"/>.</summary>
    public int StartLine { get; init; }
}

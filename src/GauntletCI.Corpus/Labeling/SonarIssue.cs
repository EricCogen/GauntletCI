// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Corpus.Labeling;

/// <summary>
/// A single BUG or VULNERABILITY issue returned by the SonarCloud public API.
/// </summary>
public sealed class SonarIssue
{
    /// <summary>SonarCloud project key, e.g. "dotnet_runtime".</summary>
    public string ProjectKey { get; init; } = "";

    /// <summary>
    /// Repo-relative file path, e.g. "src/libraries/System.Net.Http/src/System/Net/Http/HttpClient.cs".
    /// Stripped of the "projectKey:" prefix that SonarCloud embeds in component paths.
    /// </summary>
    public string FilePath { get; init; } = "";

    /// <summary>SonarCloud rule ID, e.g. "csharpsquid:S2259".</summary>
    public string Rule { get; init; } = "";

    /// <summary>BLOCKER, CRITICAL, MAJOR, MINOR, or INFO.</summary>
    public string Severity { get; init; } = "";

    /// <summary>BUG or VULNERABILITY.</summary>
    public string Type { get; init; } = "";

    /// <summary>Human-readable issue message.</summary>
    public string Message { get; init; } = "";
}

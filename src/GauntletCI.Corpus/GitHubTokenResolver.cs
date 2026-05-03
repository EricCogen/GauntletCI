// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core;

namespace GauntletCI.Corpus;

/// <summary>
/// Compatibility wrapper for <see cref="GauntletCI.Core.GitHubTokenResolver"/>.
/// The implementation has been moved to GauntletCI.Core to avoid circular dependencies.
/// </summary>
public static class GitHubTokenResolver
{
    /// <summary>
    /// Returns the best available GitHub token, or <c>null</c> if none is configured.
    /// Checks <c>GITHUB_TOKEN</c> first, then falls back to <c>gh auth token</c>.
    /// Not cached - always re-resolves to reflect environment changes.
    /// </summary>
    public static string? Resolve() => Core.GitHubTokenResolver.Resolve();

    /// <summary>Returns <c>true</c> when a token is available from any source.</summary>
    public static bool IsAvailable => Core.GitHubTokenResolver.IsAvailable;
}

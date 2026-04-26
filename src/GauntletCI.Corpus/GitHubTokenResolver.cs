// SPDX-License-Identifier: Elastic-2.0
using System.Diagnostics;

namespace GauntletCI.Corpus;

/// <summary>
/// Resolves a GitHub personal access token from the environment or the local <c>gh</c> CLI credential store.
/// Resolution order:
/// <list type="number">
///   <item><c>GITHUB_TOKEN</c> environment variable</item>
///   <item><c>gh auth token</c> (GitHub CLI, if installed and authenticated)</item>
/// </list>
/// Returns <c>null</c> if neither source yields a token.
/// </summary>
public static class GitHubTokenResolver
{
    /// <summary>
    /// Returns the best available GitHub token, or <c>null</c> if none is configured.
    /// Checks <c>GITHUB_TOKEN</c> first, then falls back to <c>gh auth token</c>.
    /// Not cached - always re-resolves to reflect environment changes.
    /// </summary>
    public static string? Resolve()
    {
        var env = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (!string.IsNullOrWhiteSpace(env))
            return env;

        return TryGhCli();
    }

    /// <summary>Returns <c>true</c> when a token is available from any source.</summary>
    public static bool IsAvailable => Resolve() is not null;

    private static string? TryGhCli()
    {
        try
        {
            var psi = new ProcessStartInfo("gh", "auth token")
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };

            using var proc = Process.Start(psi);
            if (proc is null) return null;

            var token = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(5_000);

            return string.IsNullOrWhiteSpace(token) ? null : token;
        }
        catch
        {
            return null;
        }
    }
}

// SPDX-License-Identifier: Elastic-2.0
using System.Security.Cryptography;
using System.Text;

namespace GauntletCI.Cli.Telemetry;

/// <summary>
/// One-way SHA-256 hashing for telemetry anonymisation.
/// Nothing hashed here can be reversed to recover the original value.
/// </summary>
public static class TelemetryHasher
{
    /// <summary>
    /// Returns the first 8 hex characters of SHA-256(input), lower-case.
    /// Used to produce a stable but anonymous repo identifier.
    /// </summary>
    /// <param name="input">The raw string to hash (trimmed and lowercased before hashing).</param>
    public static string Hash8(string input)
    {
        if (string.IsNullOrEmpty(input)) return "00000000";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input.Trim().ToLowerInvariant()));
        return Convert.ToHexString(bytes)[..8].ToLowerInvariant();
    }

    /// <summary>
    /// Hashes the git remote URL of <paramref name="repoRoot"/> to produce
    /// an anonymous 8-character repo ID. Returns "local" if no remote exists.
    /// </summary>
    /// <param name="repoRoot">Absolute path to the git repository root.</param>
    public static async Task<string> HashRepoAsync(string repoRoot)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("git",
                $"-C \"{repoRoot}\" remote get-url origin")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc is null) return "local";
            var url = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();
            return string.IsNullOrWhiteSpace(url) ? "local" : Hash8(url);
        }
        catch
        {
            return "local";
        }
    }
}

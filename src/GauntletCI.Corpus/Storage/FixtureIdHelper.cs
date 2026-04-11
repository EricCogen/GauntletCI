// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Corpus.Models;

namespace GauntletCI.Corpus.Storage;

/// <summary>
/// Builds deterministic fixture IDs and resolves fixture paths on disk.
/// </summary>
public static class FixtureIdHelper
{
    /// <summary>
    /// Builds a fixture ID from repo owner, name, and PR number.
    /// Example: "torvalds", "linux", 4321 → "torvalds_linux_pr4321"
    /// </summary>
    public static string Build(string repoOwner, string repoName, int prNumber)
    {
        var owner = Sanitize(repoOwner);
        var repo  = Sanitize(repoName);
        return $"{owner}_{repo}_pr{prNumber}";
    }

    /// <summary>Resolves the fixture folder path for the given tier and fixture ID.</summary>
    public static string GetFixturePath(string basePath, FixtureTier tier, string fixtureId)
        => Path.Combine(basePath, tier.ToString().ToLowerInvariant(), fixtureId);

    /// <summary>Resolves the raw snapshot sub-folder inside a fixture folder.</summary>
    public static string GetRawPath(string fixturePath) => Path.Combine(fixturePath, "raw");

    private static string Sanitize(string s)
        => s.ToLowerInvariant()
            .Replace('/', '_')
            .Replace('\\', '_')
            .Replace(' ', '-');
}

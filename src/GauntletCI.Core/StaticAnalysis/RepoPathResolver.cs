// SPDX-License-Identifier: Elastic-2.0

namespace GauntletCI.Core.StaticAnalysis;

/// <summary>
/// Resolves repository-relative paths without allowing traversal outside the repo root.
/// </summary>
public static class RepoPathResolver
{
    /// <summary>
    /// Resolves <paramref name="relativePath"/> under <paramref name="repoPath"/> when it stays inside the repo.
    /// </summary>
    public static bool TryResolvePathUnderRoot(string repoPath, string relativePath, out string absolutePath)
    {
        absolutePath = string.Empty;

        if (string.IsNullOrWhiteSpace(repoPath) || string.IsNullOrWhiteSpace(relativePath))
            return false;

        if (Path.IsPathRooted(relativePath))
            return false;

        var fullRoot = Path.GetFullPath(repoPath);
        var normalizedRelative = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var candidate = Path.GetFullPath(Path.Combine(fullRoot, normalizedRelative));

        var rootPrefix = fullRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        if (!candidate.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        absolutePath = candidate;
        return true;
    }
}

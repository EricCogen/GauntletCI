// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Rules;

namespace GauntletCI.Core.StaticAnalysis;

/// <summary>
/// Detects test infrastructure in a repository by scanning project manifests and test file paths.
/// </summary>
public static class TestFrameworkDetector
{
    private static readonly string[] TestFrameworkTokens =
    [
        "xunit", "nunit", "mstest", "microsoft.net.test.sdk",
        "nsubstitute", "moq", "autofixture", "verify", "shouldly",
        "jest", "mocha", "vitest", "chai", "jasmine",
        "pytest",
    ];

    private static readonly string[] ProjectManifestNames =
    [
        ".csproj", "package.json", "pyproject.toml",
    ];

    private static readonly string[] SkipDirectoryNames =
    [
        "bin", "obj", "node_modules", ".git", ".github",
    ];

    /// <summary>
    /// Returns true when the repository under <paramref name="repoPath"/> has test files
    /// or project manifests referencing known test frameworks.
    /// </summary>
    public static bool HasTestInfrastructure(string? repoPath)
    {
        if (string.IsNullOrWhiteSpace(repoPath) || !Directory.Exists(repoPath))
            return false;

        try
        {
            if (HasTestFiles(repoPath))
                return true;

            return HasTestFrameworkReferences(repoPath);
        }
        catch
        {
            return false;
        }
    }

    private static bool HasTestFiles(string repoPath)
    {
        foreach (var file in Directory.EnumerateFiles(repoPath, "*.*", SearchOption.AllDirectories))
        {
            if (ShouldSkipPath(file))
                continue;

            var relative = Path.GetRelativePath(repoPath, file).Replace('\\', '/');
            if (WellKnownPatterns.IsTestFile(relative))
                return true;
        }

        return false;
    }

    private static bool HasTestFrameworkReferences(string repoPath)
    {
        foreach (var file in Directory.EnumerateFiles(repoPath, "*.*", SearchOption.AllDirectories))
        {
            if (ShouldSkipPath(file))
                continue;

            if (!IsProjectManifest(file))
                continue;

            string content;
            try { content = File.ReadAllText(file); }
            catch (IOException) { continue; }

            if (ContainsTestFrameworkToken(content))
                return true;
        }

        return false;
    }

    private static bool IsProjectManifest(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        return ProjectManifestNames.Any(name =>
            fileName.EndsWith(name, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsTestFrameworkToken(string content)
    {
        foreach (var token in TestFrameworkTokens)
        {
            if (content.Contains(token, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool ShouldSkipPath(string absolutePath)
    {
        var parts = absolutePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(part =>
            SkipDirectoryNames.Contains(part, StringComparer.OrdinalIgnoreCase));
    }
}

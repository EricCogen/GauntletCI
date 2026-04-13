// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Corpus.Normalization;

/// <summary>
/// Classifies source files as test files based on path and naming heuristics.
/// </summary>
public static class TestFileClassifier
{
    private static readonly string[] PathSegments = ["/test/", "/tests/", "\\test\\", "\\tests\\"];
    private static readonly string[] NameSuffixes  = [".tests.cs", ".test.cs", "tests.cs", "test.cs"];
    private static readonly string[] ProjectHints  = ["test", "tests"];

    public static bool IsTestFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return false;

        var lower = filePath.ToLowerInvariant();

        foreach (var seg in PathSegments)
            if (lower.Contains(seg)) return true;

        var fileName = Path.GetFileName(lower);
        foreach (var suffix in NameSuffixes)
            if (fileName.EndsWith(suffix, StringComparison.Ordinal)) return true;

        // Project-name hint: any path segment that is purely a test project name
        var parts = filePath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var lp = part.ToLowerInvariant();
            foreach (var hint in ProjectHints)
                if (lp == hint || lp.EndsWith('.' + hint) || lp.StartsWith(hint + '.'))
                    return true;
        }

        return false;
    }
}

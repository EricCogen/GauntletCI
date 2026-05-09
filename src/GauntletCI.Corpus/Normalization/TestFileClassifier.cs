// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Corpus.Normalization;

/// <summary>
/// Classifies source files as test files based on path and naming heuristics.
/// Uses word boundary checking to avoid false positives on words that contain "test"
/// (e.g., "Contests.cs", "Latest.cs", "Protest.cs" are NOT test files).
/// </summary>
public static class TestFileClassifier
{
    private static readonly string[] PathSegments = ["/test/", "/tests/", "\\test\\", "\\tests\\"];
    private static readonly string[] SpecSegments = ["/spec/", "/specs/", "\\spec\\", "\\specs\\"];
    private static readonly string[] NonProductionDirs =
    [
        "/sample/", "/samples/", "\\sample\\", "\\samples\\",
        "/example/", "/examples/", "\\example\\", "\\examples\\",
        "/benchmark/", "/benchmarks/", "\\benchmark\\", "\\benchmarks\\",
        "/mock/", "/mocks/", "\\mock\\", "\\mocks\\",
        "/fake/", "/fakes/", "\\fake\\", "\\fakes\\",
    ];
    private static readonly string[] DotSuffixes = [".tests.cs", ".test.cs"];
    private static readonly string[] PascalSuffixes = ["Tests.cs", "Test.cs", "Spec.cs", "Benchmark.cs", "Benchmarks.cs"];
    private static readonly string[] ProjectHints = ["test", "tests"];

    public static bool IsTestFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        var lowerPath = filePath.ToLowerInvariant();

        // Check for test directories (/test/, /tests/)
        foreach (var seg in PathSegments)
        {
            if (lowerPath.Contains(seg, StringComparison.Ordinal))
            {
                return true;
            }
        }

        // Check for spec directories (/spec/, /specs/)
        foreach (var seg in SpecSegments)
        {
            if (lowerPath.Contains(seg, StringComparison.Ordinal))
            {
                return true;
            }
        }

        // Check for non-production directories (samples, examples, benchmarks, mocks, fakes)
        foreach (var seg in NonProductionDirs)
        {
            if (lowerPath.Contains(seg, StringComparison.Ordinal))
            {
                return true;
            }
        }

        var fileName = Path.GetFileName(filePath);
        var lowerFileName = fileName.ToLowerInvariant();

        // Check for dot-separated test suffixes (.tests.cs, .test.cs)
        foreach (var suffix in DotSuffixes)
        {
            if (lowerFileName.EndsWith(suffix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        // Check for PascalCase test suffixes (Tests.cs, Test.cs, Spec.cs)
        // Must match original casing to avoid false positives like "Contests.cs"
        foreach (var suffix in PascalSuffixes)
        {
            if (fileName.EndsWith(suffix, StringComparison.Ordinal))
            {
                // Additional check: ensure this is a word boundary, not embedded in another word
                // e.g., "Contests.cs" ends with "Tests.cs" but is not a test file
                if (IsWordBoundarySuffix(fileName, suffix))
                {
                    return true;
                }
            }
        }

        // Check filename prefix: "Test", "Tests" at the start (case-insensitive)
        if (lowerFileName.StartsWith("test", StringComparison.Ordinal))
        {
            // "Test" or "Tests" must be followed by a capital letter, number, or underscore
            // Valid: "TestHelper.cs", "TestAsync.cs"
            // Invalid: unlikely to have non-test files starting with "Test"
            if (lowerFileName.Length == 4 ||
                char.IsUpper(fileName[4]) || char.IsDigit(fileName[4]) || fileName[4] == '_')
            {
                return true;
            }
        }

        // Project-name hint: any path segment matching test project naming patterns
        var parts = filePath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (IsTestProjectName(part))
            {
                return true;
            }

            // PascalCase compound directory names: "IntegrationTests", "UnitTest", etc.
            if (part.EndsWith("Tests", StringComparison.Ordinal) ||
                part.EndsWith("Test", StringComparison.Ordinal) ||
                part.EndsWith("Spec", StringComparison.OrdinalIgnoreCase) ||
                part.EndsWith("Benchmark", StringComparison.OrdinalIgnoreCase) ||
                part.EndsWith("Benchmarks", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if a filename ends with a suffix at a word boundary.
    /// For example, "Contests.cs" ends with "Tests.cs" but NOT at a word boundary
    /// because the character before "Tests" is 't' (lowercase - part of same word).
    /// "MyServiceTests.cs" ends with "Tests.cs" AT a word boundary
    /// because the character before "Tests" is 'e' followed immediately by capital 'T'.
    /// Returns true if:
    /// 1. The suffix is the entire filename, OR
    /// 2. The character before the suffix is not a letter, OR
    /// 3. The character before the suffix is lowercase and the suffix starts with uppercase
    ///    (indicating a PascalCase word boundary)
    /// </summary>
    private static bool IsWordBoundarySuffix(string fileName, string suffix)
    {
        if (fileName.Length <= suffix.Length)
        {
            return true; // Exact match is definitely a word boundary
        }

        int boundaryIndex = fileName.Length - suffix.Length - 1;
        if (boundaryIndex < 0)
        {
            return true;
        }

        char charBefore = fileName[boundaryIndex];
        char suffixStart = suffix[0];

        // Non-letter before suffix (e.g., ".", "-", "_") → word boundary
        if (!char.IsLetter(charBefore))
        {
            return true;
        }

        // Both characters are letters: check for PascalCase boundary
        // e.g., "ServiceTests" → 'e' is lowercase, 'T' is uppercase → valid boundary
        // e.g., "Contests" → 't' is lowercase, 'T' is uppercase BUT this is embedded
        // Need additional check: if char before is lowercase and suffix starts with capital,
        // it's likely a valid word boundary (PascalCase convention)
        if (char.IsLower(charBefore) && char.IsUpper(suffixStart))
        {
            return true;
        }

        // Otherwise, it's embedded in the same word (e.g., "Contests")
        return false;
    }

    /// <summary>
    /// Checks if a path segment represents a test project using word boundary matching.
    /// Returns true for patterns like "Test.Api", "MyProject.Tests", "test-utils"
    /// but NOT for "Contest", "Protest", "Latest", or "Contests".
    /// </summary>
    private static bool IsTestProjectName(string part)
    {
        var lp = part.ToLowerInvariant();

        // Exact match: "test" or "tests" as the entire segment
        if (lp == "test" || lp == "tests")
        {
            return true;
        }

        // Suffix: ".test" or ".tests" (e.g., "MyProject.Tests", "My.test")
        if (lp.EndsWith(".test", StringComparison.Ordinal) ||
            lp.EndsWith(".tests", StringComparison.Ordinal))
        {
            return true;
        }

        // Prefix: "test." or "tests." (e.g., "Test.Api", "Tests.Core")
        if (lp.StartsWith("test.", StringComparison.Ordinal) ||
            lp.StartsWith("tests.", StringComparison.Ordinal))
        {
            return true;
        }

        // Word boundary check for "test" or "tests" in the middle
        // e.g., "unit-test", "integration_tests" should match
        // but "contest", "protest", "latest" should NOT
        if (lp.Contains("-test") || lp.Contains("_test") ||
            lp.Contains("-tests") || lp.Contains("_tests"))
        {
            return true;
        }

        // No hyphen or underscore? Check for word boundaries more carefully
        // Find "test" or "tests" preceded by a non-letter
        int testIndex = lp.IndexOf("test", StringComparison.Ordinal);
        if (testIndex > 0 && !char.IsLetter(lp[testIndex - 1]))
        {
            // Check if it's "test" or "tests" at a word boundary
            int afterTestIndex = testIndex + 4;
            if (afterTestIndex >= lp.Length)
            {
                return true; // Ends with "test" after a non-letter
            }

            if (!char.IsLetter(lp[afterTestIndex]))
            {
                return true; // "test" followed by non-letter (e.g., "test-foo")
            }
        }

        return false;
    }
}

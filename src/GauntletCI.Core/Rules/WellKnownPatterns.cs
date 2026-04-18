// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Core.Rules;

internal static class WellKnownPatterns
{
    /// <summary>Variable and field name fragments used to detect hardcoded secrets by name (GCI0010, GCI0012).</summary>
    public static readonly string[] SecretNamePatterns = [ "password", "passwd", "secret", "apikey", "api_key", "token", "credential", "private_key", "privatekey", "access_key", "auth_key" ];

    /// <summary>Log-level keywords indicating high-severity log calls that warrant review (GCI0007, GCI0013).</summary>
    public static readonly string[] HighSeverityLogKeywords = [ "error", "exception", "critical", "fatal", "warn", "warning" ];

    /// <summary>
    /// Returns <c>true</c> when the given path belongs to a test or spec file.
    /// Used across rules to avoid false positives in test code.
    /// </summary>
    /// <param name="path">The file path to inspect.</param>
    public static bool IsTestFile(string path)
    {
        var norm = path.Replace('\\', '/').ToLowerInvariant();

        // Any path segment containing "spec" is a strong test signal (unchanged)
        if (norm.Contains("spec")) return true;

        // Directory segments: each segment must start or cleanly end with "test(s)".
        // "latest" ends with "test" but has a letter before it — use word-boundary guard.
        var lastSlash = norm.LastIndexOf('/');
        if (lastSlash > 0)
        {
            foreach (var segment in norm[..lastSlash].Split('/'))
            {
                if (IsTestSegment(segment))
                    return true;
            }
        }

        // File name: use original casing to distinguish PascalCase "Tests"/"Test" suffix from
        // English words that embed "test" (e.g. "Contest.cs", "Latest.cs", "Protest.cs").
        // "FooTests" ends with capital "Tests" → test file ✓
        // "Contest"  ends with lowercase "test" → not a test file ✓
        // StartsWith check is case-insensitive to catch "testFoo.cs" and "TestFoo.cs".
        var normPath  = path.Replace('\\', '/');
        var origFile  = lastSlash >= 0 ? normPath[(lastSlash + 1)..] : normPath;
        var origNoExt = origFile.Contains('.') ? origFile[..origFile.LastIndexOf('.')] : origFile;
        return origNoExt.StartsWith("test", StringComparison.OrdinalIgnoreCase)
            || origNoExt.EndsWith("Tests", StringComparison.Ordinal)
            || origNoExt.EndsWith("Test",  StringComparison.Ordinal);
    }

    // Returns true when a lowercase directory segment represents a test directory.
    // Requires "test" to appear at a word boundary — avoids "latest", "protest", etc.
    private static bool IsTestSegment(string segment)
    {
        if (segment.StartsWith("test")) return true;
        // EndsWith "test": only when the character immediately before "test" is non-letter
        // e.g. ".test", "-test", "_test" → yes; "latest" → 'a' precedes "test" → no
        if (segment.Length > 4 && segment.EndsWith("test") && !char.IsLetter(segment[^5])) return true;
        if (segment.Length > 5 && segment.EndsWith("tests") && !char.IsLetter(segment[^6])) return true;
        return false;
    }
}

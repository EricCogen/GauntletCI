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
        var normPath = path.Replace('\\', '/');
        var lastSlash = normPath.LastIndexOf('/');

        // Directory segment checks (both original-case and lowercase variants).
        if (lastSlash > 0)
        {
            foreach (var segment in normPath[..lastSlash].Split('/'))
            {
                var lower = segment.ToLowerInvariant();
                // Exact match for spec/specs directories (covers RSpec, Jest, etc.)
                if (lower == "spec" || lower == "specs") return true;
                // Word-boundary "test(s)" check on lowercase segment (avoids "latest", "protest")
                if (IsTestSegment(lower)) return true;
                // PascalCase compound directory names: "IntegrationTests", "UnitTest", etc.
                if (segment.EndsWith("Tests", StringComparison.Ordinal)
                    || segment.EndsWith("Test", StringComparison.Ordinal)) return true;
            }
        }

        // File name: use original casing to distinguish PascalCase "Tests"/"Test"/"Spec" suffix
        // from English words that embed "test" (e.g. "Contest.cs", "Latest.cs", "Protest.cs").
        var origFile  = lastSlash >= 0 ? normPath[(lastSlash + 1)..] : normPath;
        var origNoExt = origFile.Contains('.') ? origFile[..origFile.LastIndexOf('.')] : origFile;
        return origNoExt.StartsWith("test", StringComparison.OrdinalIgnoreCase)
            || origNoExt.EndsWith("Tests", StringComparison.Ordinal)
            || origNoExt.EndsWith("Test",  StringComparison.Ordinal)
            || origNoExt.EndsWith("Spec",  StringComparison.OrdinalIgnoreCase);
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

    /// <summary>
    /// Returns <c>true</c> when the given path is an auto-generated file that should not be
    /// subject to rule analysis (source generators, designer files, scaffolded API clients, etc.).
    /// </summary>
    /// <param name="path">The file path to inspect.</param>
    public static bool IsGeneratedFile(string path)
    {
        var normPath = path.Replace('\\', '/');

        // Directory segment: any path with a /Generated/ folder is auto-generated
        if (normPath.Contains("/Generated/", StringComparison.OrdinalIgnoreCase)) return true;
        // Build output or intermediate artifacts
        if (normPath.Contains("/obj/", StringComparison.OrdinalIgnoreCase)) return true;

        var fileName = normPath.Contains('/')
            ? normPath[(normPath.LastIndexOf('/') + 1)..]
            : normPath;

        // Roslyn source generator outputs: Foo.g.cs, Foo.g.i.cs
        if (fileName.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)) return true;
        if (fileName.EndsWith(".g.i.cs", StringComparison.OrdinalIgnoreCase)) return true;
        // WinForms / WPF designer files
        if (fileName.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase)) return true;
        // Assembly-level attribute file emitted by SDK
        if (fileName.Equals("AssemblyInfo.cs", StringComparison.OrdinalIgnoreCase)) return true;

        return false;
    }
}

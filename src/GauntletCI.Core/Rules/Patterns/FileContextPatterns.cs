// SPDX-License-Identifier: Elastic-2.0

// ========== GauntletCI Self-Analysis: WHITELISTED FILE ==========
// This file contains pattern strings and regexes used by GauntletCI detection rules.
// Pattern strings (e.g., "new FileStream(", "TODO", ".GetService<") appear in the code by design.
// These are NOT actual code violations, but rather PATTERN DEFINITIONS used to detect violations in other code.
// GauntletCI Self-Analysis should skip this file from analysis to avoid false positives on pattern data.
// =================================================================

using System.Text.RegularExpressions;

namespace GauntletCI.Core.Rules.Patterns;

/// <summary>
/// File-level context and metadata detection patterns.
/// Used to identify test files, generated code, infrastructure files, and security-critical paths.
/// </summary>
internal static class FileContextPatterns
{
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
                // Benchmark / sample / example directories are not consumer-facing APIs
                if (IsNonProductionSegment(lower)) return true;
                // Mock / Fake infrastructure directories
                if (lower == "mock" || lower == "mocks" || lower == "fake" || lower == "fakes") return true;
                // PascalCase compound directory names: "IntegrationTests", "UnitTest", etc.
                if (segment.EndsWith("Tests", StringComparison.Ordinal)
                    || segment.EndsWith("Test", StringComparison.Ordinal)) return true;
                // PascalCase benchmark directories: "MyProject.Benchmarks", "Perf.Benchmark"
                if (segment.EndsWith("Benchmark", StringComparison.Ordinal)
                    || segment.EndsWith("Benchmarks", StringComparison.Ordinal)) return true;
            }
        }

        // File name: use original casing to distinguish PascalCase "Tests"/"Test"/"Spec" suffix
        // from English words that embed "test" (e.g. "Contest.cs", "Latest.cs", "Protest.cs").
        var origFile  = lastSlash >= 0 ? normPath[(lastSlash + 1)..] : normPath;
        var origNoExt = origFile.Contains('.') ? origFile[..origFile.LastIndexOf('.')] : origFile;
        return origNoExt.StartsWith("test", StringComparison.OrdinalIgnoreCase)
            || origNoExt.EndsWith("Tests", StringComparison.Ordinal)
            || origNoExt.EndsWith("Test",  StringComparison.Ordinal)
            || origNoExt.EndsWith("Spec",  StringComparison.OrdinalIgnoreCase)
            || origNoExt.EndsWith("Benchmark",  StringComparison.OrdinalIgnoreCase)
            || origNoExt.EndsWith("Benchmarks", StringComparison.OrdinalIgnoreCase);
    }

    // Returns true when a lowercase directory segment represents a test directory.
    // Requires "test" to appear at a word boundary: avoids "latest", "protest", etc.
    private static bool IsTestSegment(string segment)
    {
        if (segment.StartsWith("test")) return true;
        // EndsWith "test": only when the character immediately before "test" is non-letter
        // e.g. ".test", "-test", "_test" → yes; "latest" → 'a' precedes "test" → no
        if (segment.Length > 4 && segment.EndsWith("test") && !char.IsLetter(segment[^5])) return true;
        if (segment.Length > 5 && segment.EndsWith("tests") && !char.IsLetter(segment[^6])) return true;
        return false;
    }

    // Returns true when a directory segment represents a benchmark, sample, or example directory
    // that is not consumer-facing and should be treated like test code for rule suppression.
    private static bool IsNonProductionSegment(string segment)
    {
        // Benchmark projects: BenchmarkDotNet, microbenchmarks, perf projects
        if (segment.EndsWith("benchmark") || segment.EndsWith("benchmarks")) return true;
        if (segment == "benchmark" || segment == "benchmarks") return true;
        // Sample and example projects: demonstration code with no API stability guarantee
        if (segment == "samples" || segment == "sample") return true;
        if (segment == "examples" || segment == "example") return true;
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
        // API surface manifest files emitted by the .NET SDK:
        //   net8.0.cs, net10.0.cs (numeric TFMs) and netstandard2.0.cs, netstandard2.1.cs, etc.
        // These enumerate every public member and are never hand-authored.
        if (Regex.IsMatch(
                fileName, @"\.(net\d+\.\d+|netstandard\d+\.\d+)\.cs$",
                RegexOptions.IgnoreCase)) return true;

        return false;
    }

    /// <summary>
    /// Returns <c>true</c> when the file path indicates infrastructure/configuration code where DI setup occurs.
    /// Service locator patterns and direct instantiation are acceptable in Program.cs, Startup.cs, etc.
    /// </summary>
    public static bool IsInfrastructureFile(string path)
    {
        var fileName = System.IO.Path.GetFileName(path);
        return string.Equals(fileName, "Program.cs", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "Startup.cs", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith("Extensions.cs", StringComparison.OrdinalIgnoreCase)
            || path.Contains("ServiceCollection", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// File path components indicating security-critical code sections.
    /// Used by GCI0003 for behavioral change context analysis (confidential boost for security changes).
    /// </summary>
    public static readonly string[] SecurityCriticalPaths =
    [
        "Http2", "Kestrel", "TLS", "SSL", "Crypto", "Auth",
        "Uri", "Parsing", "Validation", "Security", "Hmac", "Hash",
        "Decrypt", "Encrypt", "Certificate", "Token", "Key"
    ];

    /// <summary>
    /// Returns <c>true</c> if the given path contains security-critical component names.
    /// Used by GCI0003 for identifying security-related code changes.
    /// </summary>
    public static bool IsSecurityCriticalPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        return SecurityCriticalPaths.Any(p =>
            path.Contains(p, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns <c>true</c> if the line is a comment (starts with //, *, or #).
    /// Used across rules to skip comment-only lines from analysis.
    /// </summary>
    public static bool IsCommentLine(string trimmed) =>
        trimmed.StartsWith("//", StringComparison.Ordinal) ||
        trimmed.StartsWith("*", StringComparison.Ordinal) ||
        trimmed.StartsWith("#", StringComparison.Ordinal);
}

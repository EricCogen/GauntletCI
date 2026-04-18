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

        // Directory segments: "test" anywhere in a directory name is a strong signal
        var lastSlash = norm.LastIndexOf('/');
        if (lastSlash > 0 && norm[..lastSlash].Contains("test")) return true;

        // File name: "test" must appear as a word-boundary prefix or suffix, not embedded
        // mid-word (avoids false positives such as LatestOrderService.cs, ContestController.cs)
        var fileName  = lastSlash >= 0 ? norm[(lastSlash + 1)..] : norm;
        var nameNoExt = fileName.Contains('.') ? fileName[..fileName.LastIndexOf('.')] : fileName;
        return nameNoExt.StartsWith("test")
            || nameNoExt.EndsWith("test")
            || nameNoExt.EndsWith("tests");
    }
}

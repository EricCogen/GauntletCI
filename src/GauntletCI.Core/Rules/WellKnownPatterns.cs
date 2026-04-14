// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Core.Rules;

internal static class WellKnownPatterns
{
    /// <summary>Variable and field name fragments used to detect hardcoded secrets by name (GCI0010, GCI0012).</summary>
    public static readonly string[] SecretNamePatterns = [ "password", "passwd", "secret", "apikey", "api_key", "token", "credential", "private_key", "privatekey", "access_key", "auth_key" ];

    /// <summary>Log-level keywords indicating high-severity log calls that warrant review (GCI0007, GCI0013).</summary>
    public static readonly string[] HighSeverityLogKeywords = [ "error", "exception", "critical", "fatal", "warn", "warning" ];

    /// <summary>Directory and file name fragments that indicate security-sensitive code paths reviewed by multiple rules.</summary>
    public static readonly string[] CriticalPathKeywords = [ "auth", "security", "payment", "billing", "crypto", "encrypt", "secret", "credential", "token", "migration", "schema" ];

    /// <summary>
    /// Returns <c>true</c> when the given path belongs to a test or spec file.
    /// Used across rules to avoid false positives in test code.
    /// </summary>
    /// <param name="path">The file path to inspect.</param>
    public static bool IsTestFile(string path)
    {
        var lower = path.ToLowerInvariant();
        return lower.Contains("test") || lower.Contains("spec") || lower.Contains(".tests/") || lower.EndsWith("tests.cs");
    }
}

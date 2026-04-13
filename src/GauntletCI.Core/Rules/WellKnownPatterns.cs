// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Core.Rules;

internal static class WellKnownPatterns
{
    // Secret-like field/variable name fragments (used by GCI0010, GCI0012)
    public static readonly string[] SecretNamePatterns = [ "password", "passwd", "secret", "apikey", "api_key", "token", "credential", "private_key", "privatekey", "access_key", "auth_key" ];

    // Log-level keywords indicating severity (used by GCI0007, GCI0013)
    public static readonly string[] HighSeverityLogKeywords = [ "error", "exception", "critical", "fatal", "warn", "warning" ];

    // Critical-path directory/file name fragments (used by several rules)
    public static readonly string[] CriticalPathKeywords = [ "auth", "security", "payment", "billing", "crypto", "encrypt", "secret", "credential", "token", "migration", "schema" ];

    // Canonical test-file detection — returns true for test/spec files
    public static bool IsTestFile(string path)
    {
        var lower = path.ToLowerInvariant();
        return lower.Contains("test") || lower.Contains("spec") || lower.Contains(".tests/") || lower.EndsWith("tests.cs");
    }
}

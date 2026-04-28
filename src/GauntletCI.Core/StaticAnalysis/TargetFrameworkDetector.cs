// SPDX-License-Identifier: Elastic-2.0
using System.Text.RegularExpressions;

namespace GauntletCI.Core.StaticAnalysis;

/// <summary>
/// Reads the first <c>&lt;TargetFramework&gt;</c> or <c>&lt;TargetFrameworks&gt;</c> value
/// found in any <c>.csproj</c> file under the repository root.
/// Used to tailor rule messages to the actual target TFM rather than always assuming net8.
/// </summary>
public static class TargetFrameworkDetector
{
    private static readonly Regex TfmPattern =
        new(@"<TargetFrameworks?>([^<]+)</TargetFrameworks?>", RegexOptions.IgnoreCase);

    /// <summary>
    /// Scans <paramref name="repoPath"/> for the first <c>.csproj</c> that declares a target
    /// framework and returns the primary TFM (the first entry when multiple are listed).
    /// Returns <c>null</c> when no project file is found or no TFM can be parsed.
    /// </summary>
    public static string? Detect(string repoPath)
    {
        if (string.IsNullOrEmpty(repoPath) || !Directory.Exists(repoPath))
            return null;

        try
        {
            foreach (var csproj in Directory.EnumerateFiles(repoPath, "*.csproj", SearchOption.AllDirectories))
            {
                string xml;
                try { xml = File.ReadAllText(csproj); }
                catch (IOException) { continue; }

                var match = TfmPattern.Match(xml);
                if (!match.Success) continue;

                // <TargetFrameworks> may contain semicolon-separated values; take the first
                var primary = match.Groups[1].Value.Trim().Split(';')[0].Trim();
                if (!string.IsNullOrEmpty(primary))
                    return primary;
            }
        }
        catch (Exception)
        {
            // File system errors are non-fatal: TFM detection is best-effort
        }

        return null;
    }

    /// <summary>
    /// Returns true when <paramref name="tfm"/> targets .NET 8 or later
    /// (e.g. <c>net8.0</c>, <c>net9.0</c>).
    /// Framework monikers like <c>netstandard</c>, <c>netcoreapp</c>, and <c>net4x</c> return false.
    /// </summary>
    public static bool IsNet8OrLater(string? tfm)
    {
        if (string.IsNullOrEmpty(tfm)) return false;

        // net8.0, net9.0, net10.0, … : match netN.M where N >= 8
        var m = Regex.Match(tfm, @"^net(\d+)\.\d+$", RegexOptions.IgnoreCase);
        return m.Success && int.TryParse(m.Groups[1].Value, out var major) && major >= 8;
    }
}

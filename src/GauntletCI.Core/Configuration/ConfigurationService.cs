// SPDX-License-Identifier: Elastic-2.0
using System.Text.RegularExpressions;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Configuration;

/// <summary>
/// Resolves the effective <see cref="RuleSeverity"/> for any rule ID using a three-tier priority chain:
/// <list type="number">
///   <item><description><c>.gauntletci.json</c> rule overrides (highest priority)</description></item>
///   <item><description><c>.editorconfig</c> <c>dotnet_diagnostic.GCI####.severity</c> entries</description></item>
///   <item><description>Built-in defaults from <see cref="DefaultSeverities"/> (lowest priority)</description></item>
/// </list>
/// Results are cached for the lifetime of the instance (one per analysis run).
/// </summary>
public sealed class ConfigurationService
{
    private readonly GauntletConfig _config;
    private readonly IReadOnlyDictionary<string, RuleSeverity> _editorConfigOverrides;
    private readonly Dictionary<string, RuleSeverity> _cache = new(StringComparer.OrdinalIgnoreCase);

    // Matches: dotnet_diagnostic.GCI0001.severity = error
    private static readonly Regex DiagnosticLineRegex = new(
        @"^\s*dotnet_diagnostic\.(GCI\d+)\.severity\s*=\s*(\w+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public ConfigurationService(GauntletConfig config, string? repoPath = null)
    {
        _config = config;
        _editorConfigOverrides = repoPath is not null
            ? ParseEditorConfig(repoPath)
            : new Dictionary<string, RuleSeverity>();
    }

    /// <summary>
    /// Returns the effective severity for <paramref name="ruleId"/>, applying the full priority chain.
    /// Results are cached so repeated calls are O(1).
    /// </summary>
    public RuleSeverity GetEffectiveSeverity(string ruleId)
    {
        if (_cache.TryGetValue(ruleId, out var cached)) return cached;

        RuleSeverity resolved;

        // 1. .gauntletci.json override
        if (_config.Rules.TryGetValue(ruleId, out var rc) && rc.Severity is not null
            && TryParseSeverity(rc.Severity, out var jsonSev))
        {
            resolved = jsonSev;
        }
        // 2. .editorconfig
        else if (_editorConfigOverrides.TryGetValue(ruleId, out var edSev))
        {
            resolved = edSev;
        }
        // 3. Built-in default
        else
        {
            resolved = DefaultSeverities.Get(ruleId);
        }

        _cache[ruleId] = resolved;
        return resolved;
    }

    private static IReadOnlyDictionary<string, RuleSeverity> ParseEditorConfig(string repoPath)
    {
        var result = new Dictionary<string, RuleSeverity>(StringComparer.OrdinalIgnoreCase);
        var path = Path.Combine(repoPath, ".editorconfig");
        if (!File.Exists(path)) return result;

        try
        {
            foreach (var line in File.ReadAllLines(path))
            {
                var m = DiagnosticLineRegex.Match(line);
                if (!m.Success) continue;

                var ruleId = m.Groups[1].Value;
                var level  = m.Groups[2].Value;
                result[ruleId] = MapEditorConfigLevel(level);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Silently skip unreadable .editorconfig
        }

        return result;
    }

    private static RuleSeverity MapEditorConfigLevel(string level) =>
        level.ToLowerInvariant() switch
        {
            "error"      => RuleSeverity.Block,
            "warning"    => RuleSeverity.Warn,
            "suggestion" => RuleSeverity.Info,
            "none"       => RuleSeverity.None,
            _            => RuleSeverity.Info,
        };

    /// <summary>
    /// Parses a severity string from <c>.gauntletci.json</c>.
    /// Accepts new-style values (Block/Warn/Info/None) and legacy Confidence values (High/Medium/Low).
    /// </summary>
    private static bool TryParseSeverity(string value, out RuleSeverity severity)
    {
        severity = value.ToLowerInvariant() switch
        {
            "block" or "high"            => RuleSeverity.Block,
            "warn"  or "warning" or "medium" => RuleSeverity.Warn,
            "info"  or "suggestion" or "low" => RuleSeverity.Info,
            "none"                       => RuleSeverity.None,
            _                            => RuleSeverity.Info,
        };
        return true;
    }
}

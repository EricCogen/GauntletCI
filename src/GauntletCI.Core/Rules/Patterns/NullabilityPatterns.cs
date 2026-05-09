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
/// Null-safety, NRT (Nullable Reference Type), and nullable reference type detection patterns.
/// Used by GCI0006, GCI0043, and other nullability-aware rules to reduce false positives.
/// </summary>
internal static class NullabilityPatterns
{
    /// <summary>
    /// Returns <c>true</c> when NRT (Nullable Reference Type) is enabled for the given file.
    /// NRT is enabled via: #nullable enable directive, project-wide settings, or modern .NET versions.
    /// Used by GCI0006 and GCI0043 to determine if 'string' parameters are non-nullable by default.
    /// </summary>
    public static bool IsNullableReferenceTypeEnabled(string fileContent)
    {
        // Explicit NRT directive: #nullable enable or #nullable restore
        if (fileContent.Contains("#nullable enable", StringComparison.OrdinalIgnoreCase) ||
            fileContent.Contains("#nullable restore", StringComparison.OrdinalIgnoreCase))
            return true;

        // Explicit NRT disable: #nullable disable indicates NRT is not active
        if (fileContent.Contains("#nullable disable", StringComparison.OrdinalIgnoreCase))
            return false;

        // Heuristic: Modern .NET projects (net5+) typically have NRT enabled
        // Look for patterns that indicate modern C# (nullable annotations, record types, init accessors, required members)
        if (fileContent.Contains(" record ", StringComparison.Ordinal) ||
            fileContent.Contains("{ init; }", StringComparison.Ordinal) ||
            fileContent.Contains("{ get; init; }", StringComparison.Ordinal) ||
            fileContent.Contains("required ", StringComparison.Ordinal))
            return true;

        // Look for the pattern: non-nullable string used in method signatures
        // This is stronger evidence of NRT enablement than just presence of 'string'
        // Pattern: public/protected method with 'string' param not followed by '?'
        if (Regex.IsMatch(
                fileContent, @"(public|protected)\s+\w+\s+\w+\s*\(\s*string\s+\w+"))
            return true;

        // Default: assume NRT disabled (conservative approach - will validate parameters)
        return false;
    }

    /// <summary>
    /// Returns <c>true</c> when the parameter section contains explicitly non-nullable parameters
    /// (e.g., 'string param' without '?'). In NRT-enabled context, these don't need validation.
    /// </summary>
    public static bool HasNonNullableParams(string paramSection)
    {
        // Look for 'string' not followed by '?' (indicating non-nullable in NRT context)
        int angleDepth = 0;
        for (int i = 0; i < paramSection.Length; i++)
        {
            char c = paramSection[i];
            if (c == '<') { angleDepth++; continue; }
            if (c == '>') { angleDepth = Math.Max(0, angleDepth - 1); continue; }
            if (angleDepth > 0) continue;

            // Match "string" not followed by "?"
            if (i + 6 <= paramSection.Length && paramSection.AsSpan(i, 6).SequenceEqual("string"))
            {
                if (i + 6 >= paramSection.Length || paramSection[i + 6] != '?')
                {
                    // Check boundary
                    bool leadOk = i == 0 || paramSection[i - 1] is ' ' or '(' or ',' or '<';
                    bool trailOk = i + 6 >= paramSection.Length || paramSection[i + 6] is ' ' or '[' or ',' or ')';
                    if (leadOk && trailOk) return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Returns <c>true</c> when the parameter list contains nullable parameters (e.g., 'string?' or 'object?').
    /// Used by GCI0006 to detect when public methods have nullable reference type parameters.
    /// </summary>
    public static bool HasNullableReferenceParam(string paramSection)
    {
        // Walk character by character, tracking generic depth so we skip type arguments
        // like Dictionary<string?, int> and only match top-level parameters.
        int angleDepth = 0;
        for (int i = 0; i < paramSection.Length; i++)
        {
            char c = paramSection[i];
            if (c == '<') { angleDepth++; continue; }
            if (c == '>') { angleDepth = Math.Max(0, angleDepth - 1); continue; }
            if (angleDepth > 0) continue;

            foreach (var keyword in new[] { "string?", "object?" })
            {
                if (i + keyword.Length > paramSection.Length) continue;
                if (!paramSection.AsSpan(i).StartsWith(keyword, StringComparison.Ordinal)) continue;

                // Leading boundary: must be preceded by a non-identifier char
                bool leadOk = i == 0 || paramSection[i - 1] is ' ' or '(' or ',' or '<';
                if (!leadOk) continue;

                // Trailing boundary: must be followed by a non-identifier char
                int after = i + keyword.Length;
                bool trailOk = after >= paramSection.Length ||
                               paramSection[after] is ' ' or '[' or ',' or ')' or '<';
                if (trailOk) return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Returns <c>true</c> when the content contains Nullable&lt;T&gt; where T is a value type.
    /// In NRT context, Nullable&lt;int&gt;, Nullable&lt;string&gt;, etc. always have a value.
    /// </summary>
    public static bool IsNullableOfNonNullableType(string content)
    {
        // Look for Nullable<T> or Nullable<...> patterns
        var match = Regex.Match(content, @"Nullable<(\w+(?:<[^>]+>)?)>");
        if (!match.Success) return false;

        var typeParam = match.Groups[1].Value;
        
        // If T is a value type (int, bool, DateTime, etc.), Nullable<T> always has a value in NRT context
        var valueTypes = new[] 
        { 
            "int", "long", "short", "byte", "double", "float", "decimal", "bool", 
            "uint", "ulong", "ushort", "ubyte", "char",
            "DateTime", "TimeSpan", "DateOnly", "TimeOnly", "Guid",
            "DateTimeOffset", "DateTimeKind"
        };

        // Also check for custom structs (heuristic: if it's PascalCase and not a built-in type)
        bool isValueType = valueTypes.Contains(typeParam);
        bool isCustomStruct = typeParam.Length > 0 && char.IsUpper(typeParam[0]) && !valueTypes.Contains(typeParam);

        return isValueType || isCustomStruct;
    }

    /// <summary>
    /// Returns <c>true</c> when the content contains a #pragma warning disable with nullable-related codes.
    /// Detects suppression of nullable reference type warnings (CS8600, CS8603, etc.).
    /// Used by GCI0043 to flag deliberate nullable warning suppression.
    /// </summary>
    public static bool IsPragmaNullableDisable(string content)
    {
        if (!content.Contains("#pragma warning disable", StringComparison.OrdinalIgnoreCase))
            return false;
        
        var nullableCodes = new[] { "nullable", "CS8600", "CS8601", "CS8602", "CS8603", "CS8604" };
        return nullableCodes.Any(code =>
            content.Contains(code, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns <c>true</c> when the content contains a LINQ expression where .Value is intentionally mapped.
    /// Patterns: .Select(x => x.Value), .Where(x => x.Value != null), etc.
    /// Used by GCI0006 to avoid flagging safe LINQ projections as unsafe dereferences.
    /// </summary>
    public static bool IsLinqValueProjection(string content)
    {
        var linqMethods = new[] { "Select", "SelectMany", "Where", "OrderBy", "OrderByDescending", 
                                  "GroupBy", "All", "Any", "First", "FirstOrDefault", 
                                  "Last", "LastOrDefault", "Single", "SingleOrDefault" };

        foreach (var method in linqMethods)
        {
            // Pattern: .MethodName(... => ....Value...)
            var pattern = @"\." + method + @"\s*\([^)]*=>.*\.Value";
            if (Regex.IsMatch(content, pattern, RegexOptions.IgnoreCase))
                return true;
        }

        return false;
    }
}

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
/// Patterns used to detect dependency injection anti-patterns and safety issues.
/// </summary>
internal static class DependencyInjectionPatterns
{
    /// <summary>
    /// Service locator anti-patterns (Service.Current, ServiceLocator.GetInstance, etc.).
    /// Service locator hides dependencies and makes testing harder.
    /// Used by GCI0038 to detect service locator usage in non-infrastructure code.
    /// </summary>
    public static readonly string[] ServiceLocatorPatterns =
    [
        "Service.Current", "ServiceLocator.GetInstance", "ServiceProvider.GetService",
        "Container.Resolve", "ObjectFactory.GetInstance", "ObjectFactory.Create",
        "Globals.ThisAddIn", "Globals.Ribbon",
        ".GetRequiredService<", ".GetService<"
    ];

    /// <summary>
    /// Patterns to exclude from direct instantiation detection (factories, singletons, registrations, etc.).
    /// These are legitimate uses of 'new' that should not trigger GCI0038 false positives.
    /// </summary>
    public static readonly string[] DirectInstantiationExclusions =
    [
        "new ServiceCollection", "AddScoped<", "AddSingleton<", "AddTransient<",
        "RegisterService", "RegisterSingleton", "RegisterScoped", "RegisterTransient",
        "new object()", "new List<", "new Dictionary<", "new HashSet<", "new []", "new [",
        "factory", "Factory", "builder", "Builder", "provider", "Provider",
        "EventHandler", "Delegate", "Action", "Func"
    ];

    /// <summary>
    /// Regex: matches "new TypeName(...)" patterns to detect direct instantiation of service types.
    /// Used by GCI0038 to identify services being directly instantiated instead of injected.
    /// </summary>
    public static readonly Regex DirectInstantiationRegex =
        new(@"new\s+([A-Z][A-Za-z0-9_]*)\s*\(", RegexOptions.Compiled);

    /// <summary>
    /// Returns <c>true</c> if the given path is an infrastructure or DI setup file.
    /// Infrastructure files (Startup, ServiceCollectionExtensions, DI containers) use direct
    /// instantiation and service locator patterns as part of their job and should be excluded.
    /// </summary>
    public static bool IsInfrastructureFile(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        
        var lowerPath = path.Replace('\\', '/').ToLowerInvariant();
        
        // DI container and startup files
        return lowerPath.Contains("startup") ||
               lowerPath.Contains("servicecollection") ||
               lowerPath.Contains("dependencyinjection") ||
               lowerPath.Contains("dicontainer") ||
               lowerPath.Contains("extensions.cs") || // ServiceExtensions, AuthExtensions, etc.
               lowerPath.Contains("/infrastructure/", StringComparison.OrdinalIgnoreCase) ||
               lowerPath.Contains("/configuration/", StringComparison.OrdinalIgnoreCase) ||
               lowerPath.Contains("program.cs") ||
               lowerPath.Contains("composition");
    }
}

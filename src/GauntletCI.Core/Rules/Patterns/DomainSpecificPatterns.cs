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
/// Facade providing backward-compatible access to domain-specific patterns.
/// Individual pattern domains are now organized in dedicated files.
/// These nested classes delegate to maintain backward compatibility with WellKnownPatterns.
/// </summary>
internal static class DomainSpecificPatterns
{
    // ===== Module-level properties delegating to ResourceTimeoutPatterns =====
    
    public static string[] TimeoutPatterns => ResourceTimeoutPatterns.TimeoutPatterns;
    public static string[] IterationLimitPatterns => ResourceTimeoutPatterns.IterationLimitPatterns;
    public static string[] ResourceLimitPatterns => ResourceTimeoutPatterns.ResourceLimitPatterns;
    public static string[] ResourceCleanupPatterns => ResourceTimeoutPatterns.ResourceCleanupPatterns;
    public static string[] AsyncPatterns => ResourceTimeoutPatterns.AsyncPatterns;

    // ===== Module-level properties delegating to TestSilencePatterns =====

    public static string[] TestSilencePatterns => global::GauntletCI.Core.Rules.Patterns.TestSilencePatterns.Silence;
    public static string[] TestAttributeMarkers => global::GauntletCI.Core.Rules.Patterns.TestSilencePatterns.AttributeMarkers;
    public static string[] TestAssertionKeywords => global::GauntletCI.Core.Rules.Patterns.TestSilencePatterns.AssertionKeywords;

    // ===== Module-level helper methods (delegate to CodePatterns and other domains) =====

    public static bool HasMvvmPattern(string content) =>
        !string.IsNullOrEmpty(content) &&
        FrameworkPatterns.MvvmPatterns.Any(p => content.Contains(p, StringComparison.OrdinalIgnoreCase));

    public static bool HasDevOnlyMarker(string content) =>
        !string.IsNullOrEmpty(content) &&
        CodePatterns.DevOnlyMarkers.Any(p => content.Contains(p, StringComparison.OrdinalIgnoreCase));

    public static bool HasMappingPattern(string content) =>
        !string.IsNullOrEmpty(content) &&
        (CodePatterns.OrmAutoMapPatterns.Any(p => content.Contains(p, StringComparison.OrdinalIgnoreCase)) ||
         CodePatterns.DtoMapperPatterns.Any(p => content.Contains(p, StringComparison.OrdinalIgnoreCase)));

    public static bool HasMockPattern(string content) =>
        !string.IsNullOrEmpty(content) &&
        TestPatterns.MockObjectPatterns.Any(p => content.Contains(p, StringComparison.OrdinalIgnoreCase));

    public static bool HasInternalMarker(string content) =>
        !string.IsNullOrEmpty(content) &&
        CodePatterns.InternalMarkers.Any(p => content.Contains(p, StringComparison.OrdinalIgnoreCase));

    public static bool IsDiCompositionRoot(string content) =>
        !string.IsNullOrEmpty(content) &&
        CodePatterns.DiCompositionRootMarkers.Any(p => content.Contains(p, StringComparison.OrdinalIgnoreCase));

    // ===== Nested Classes - Delegations for backward compatibility =====

    public static class DataIntegrityPatterns
    {
        public static string[] HttpContextSignals => global::GauntletCI.Core.Rules.Patterns.DataIntegrityPatterns.HttpContextSignals;
        public static string[] SqlIgnorePatterns => global::GauntletCI.Core.Rules.Patterns.DataIntegrityPatterns.SqlIgnorePatterns;
        public static string[] UncheckedCastPatterns => global::GauntletCI.Core.Rules.Patterns.DataIntegrityPatterns.UncheckedCastPatterns;
        public static bool HasHttpContextSignal(string content) => global::GauntletCI.Core.Rules.Patterns.DataIntegrityPatterns.HasHttpContextSignal(content);
    }

    public static class PiiDetectionPatterns
    {
        public static string[] PiiTerms => global::GauntletCI.Core.Rules.Patterns.PiiDetectionPatterns.PiiTerms;
        public static string[] LogPrefixes => global::GauntletCI.Core.Rules.Patterns.PiiDetectionPatterns.LogPrefixes;
        public static string[] TransformationPatterns => global::GauntletCI.Core.Rules.Patterns.PiiDetectionPatterns.TransformationPatterns;
        public static string[] ReflectionGuards => global::GauntletCI.Core.Rules.Patterns.PiiDetectionPatterns.ReflectionGuards;
        public static bool IsDataTransformed(string content) => global::GauntletCI.Core.Rules.Patterns.PiiDetectionPatterns.IsDataTransformed(content);
    }

    public static class IdempotencyPatterns
    {
        public static string[] IdempotencySignals => global::GauntletCI.Core.Rules.Patterns.IdempotencyPatterns.IdempotencySignals;
        public static string[] UpsertPatterns => global::GauntletCI.Core.Rules.Patterns.IdempotencyPatterns.UpsertPatterns;
    }

    public static class ResourcePatterns
    {
        public static string[] DisposableTypes => global::GauntletCI.Core.Rules.Patterns.ResourcePatterns.DisposableTypes;
        public static string[] DisposableSuffixes => global::GauntletCI.Core.Rules.Patterns.ResourcePatterns.DisposableSuffixes;
        public static HashSet<string> OwnedByOtherRules => global::GauntletCI.Core.Rules.Patterns.ResourcePatterns.OwnedByOtherRules;
        public static HashSet<string> KnownNonDisposableTypes => global::GauntletCI.Core.Rules.Patterns.ResourcePatterns.KnownNonDisposableTypes;
        public static Regex NewTypeRegex => global::GauntletCI.Core.Rules.Patterns.ResourcePatterns.NewTypeRegex;
    }

    public static class ExternalServicePatterns
    {
        public static string[] HttpCallMethods => global::GauntletCI.Core.Rules.Patterns.ExternalServicePatterns.HttpCallMethods;
        public static string[] CtCheckHttpMethods => global::GauntletCI.Core.Rules.Patterns.ExternalServicePatterns.CtCheckHttpMethods;
        public static string[] PollyPatterns => global::GauntletCI.Core.Rules.Patterns.ExternalServicePatterns.PollyPatterns;
        public static string[] FactoryConfigPatterns => global::GauntletCI.Core.Rules.Patterns.ExternalServicePatterns.FactoryConfigPatterns;
        public static string[] FireAndForgetPatterns => global::GauntletCI.Core.Rules.Patterns.ExternalServicePatterns.FireAndForgetPatterns;
    }

    public static class PerformancePatterns
    {
        public static string[] LinqMethods => global::GauntletCI.Core.Rules.Patterns.PerformancePatterns.LinqMethods;
        public static string[] LoopKeywords => global::GauntletCI.Core.Rules.Patterns.PerformancePatterns.LoopKeywords;
        public static string[] UnboundedLoopKeywords => global::GauntletCI.Core.Rules.Patterns.PerformancePatterns.UnboundedLoopKeywords;
        public static bool HasLinqCall(string content) => global::GauntletCI.Core.Rules.Patterns.PerformancePatterns.HasLinqCall(content);
        public static bool HasLoopConstruct(string content) => global::GauntletCI.Core.Rules.Patterns.PerformancePatterns.HasLoopConstruct(content);
        public static bool IsRuleImplementationFile(string path) => global::GauntletCI.Core.Rules.Patterns.PerformancePatterns.IsRuleImplementationFile(path);
    }

    public static class FloatingPointPatterns
    {
        public static Regex FloatLiteralOnRightRegex => global::GauntletCI.Core.Rules.Patterns.FloatingPointPatterns.FloatLiteralOnRightRegex;
        public static Regex FloatLiteralOnLeftRegex => global::GauntletCI.Core.Rules.Patterns.FloatingPointPatterns.FloatLiteralOnLeftRegex;
        public static Regex FloatCastWithEqualityRegex => global::GauntletCI.Core.Rules.Patterns.FloatingPointPatterns.FloatCastWithEqualityRegex;
        public static Regex FloatTypeWithEqualityRegex => global::GauntletCI.Core.Rules.Patterns.FloatingPointPatterns.FloatTypeWithEqualityRegex;
        public static Regex IntegerZeroGuardRegex => global::GauntletCI.Core.Rules.Patterns.FloatingPointPatterns.IntegerZeroGuardRegex;
        public static bool IsGuardedIntegerZeroCheck(string content) => global::GauntletCI.Core.Rules.Patterns.FloatingPointPatterns.IsGuardedIntegerZeroCheck(content);
    }

    public static class DataSchemaPatterns
    {
        public static string[] SerializationAttributes => global::GauntletCI.Core.Rules.Patterns.DataSchemaPatterns.SerializationAttributes;
    }

    public static class ExceptionPatterns
    {
        public static string[] ThrowAssertions => global::GauntletCI.Core.Rules.Patterns.ExceptionPatterns.ThrowAssertions;
        public static string[] GuardClauseThrows => global::GauntletCI.Core.Rules.Patterns.ExceptionPatterns.GuardClauseThrows;
    }

    public static class DependencyInjectionPatterns
    {
        public static string[] ServiceLocatorPatterns => global::GauntletCI.Core.Rules.Patterns.DependencyInjectionPatterns.ServiceLocatorPatterns;
        public static string[] DirectInstantiationExclusions => global::GauntletCI.Core.Rules.Patterns.DependencyInjectionPatterns.DirectInstantiationExclusions;
        public static Regex DirectInstantiationRegex => global::GauntletCI.Core.Rules.Patterns.DependencyInjectionPatterns.DirectInstantiationRegex;
        public static bool IsInfrastructureFile(string path) => global::GauntletCI.Core.Rules.Patterns.DependencyInjectionPatterns.IsInfrastructureFile(path);
    }

    public static class StubDetectionPatterns
    {
        public static string[] StubKeywords => global::GauntletCI.Core.Rules.Patterns.StubDetectionPatterns.StubKeywords;
    }

    public static class ArchitecturePatterns
    {
        public static Regex UsingRegex => global::GauntletCI.Core.Rules.Patterns.ArchitecturePatterns.UsingRegex;
    }

    public static class FrameworkPatterns
    {
        public static string[] MvvmPatterns => global::GauntletCI.Core.Rules.Patterns.FrameworkPatterns.MvvmPatterns;
        public static string[] DiContainerPatterns => global::GauntletCI.Core.Rules.Patterns.FrameworkPatterns.DiContainerPatterns;
        public static string[] HttpBindingPatterns => global::GauntletCI.Core.Rules.Patterns.FrameworkPatterns.HttpBindingPatterns;
        public static string[] ExceptionHandlingPatterns => global::GauntletCI.Core.Rules.Patterns.FrameworkPatterns.ExceptionHandlingPatterns;
    }

    public static class TestPatterns
    {
        public static string[] SetupTeardownAttributes => global::GauntletCI.Core.Rules.Patterns.TestPatterns.SetupTeardownAttributes;
        public static string[] MockObjectPatterns => global::GauntletCI.Core.Rules.Patterns.TestPatterns.MockObjectPatterns;
        public static string[] TestFixturePatterns => global::GauntletCI.Core.Rules.Patterns.TestPatterns.TestFixturePatterns;
        public static string[] AssertionLibraryPatterns => global::GauntletCI.Core.Rules.Patterns.TestPatterns.AssertionLibraryPatterns;
    }

    public static class CodePatterns
    {
        public static string[] DevOnlyMarkers => global::GauntletCI.Core.Rules.Patterns.CodePatterns.DevOnlyMarkers;
        public static string[] FrameworkInitializationPatterns => global::GauntletCI.Core.Rules.Patterns.CodePatterns.FrameworkInitializationPatterns;
        public static string[] OrmAutoMapPatterns => global::GauntletCI.Core.Rules.Patterns.CodePatterns.OrmAutoMapPatterns;
        public static string[] DtoMapperPatterns => global::GauntletCI.Core.Rules.Patterns.CodePatterns.DtoMapperPatterns;
        public static string[] RetryPolicyPatterns => global::GauntletCI.Core.Rules.Patterns.CodePatterns.RetryPolicyPatterns;
        public static string[] BuilderPatterns => global::GauntletCI.Core.Rules.Patterns.CodePatterns.BuilderPatterns;
        public static string[] InterfaceImplementationPatterns => global::GauntletCI.Core.Rules.Patterns.CodePatterns.InterfaceImplementationPatterns;
        public static string[] InfrastructureMarkers => global::GauntletCI.Core.Rules.Patterns.CodePatterns.InfrastructureMarkers;
        public static string[] InternalMarkers => global::GauntletCI.Core.Rules.Patterns.CodePatterns.InternalMarkers;
        public static string[] DiCompositionRootMarkers => global::GauntletCI.Core.Rules.Patterns.CodePatterns.DiCompositionRootMarkers;
        public static string[] OrmAsyncPatterns => global::GauntletCI.Core.Rules.Patterns.CodePatterns.OrmAsyncPatterns;
        public static string[] FireAndForgetBackgroundPatterns => global::GauntletCI.Core.Rules.Patterns.CodePatterns.FireAndForgetBackgroundPatterns;
        public static string[] BoundedSynchronizationPatterns => global::GauntletCI.Core.Rules.Patterns.CodePatterns.BoundedSynchronizationPatterns;
        public static string[] InstanceScopedCachePatterns => global::GauntletCI.Core.Rules.Patterns.CodePatterns.InstanceScopedCachePatterns;
    }

    // ===== Helper Method Delegations =====

    public static bool HasHttpContextSignal(string content) => 
        global::GauntletCI.Core.Rules.Patterns.DataIntegrityPatterns.HasHttpContextSignal(content);
}




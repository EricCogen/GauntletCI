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

    public static string[] TestSilencePatterns => Patterns.TestSilencePatterns.Silence;
    public static string[] TestAttributeMarkers => Patterns.TestSilencePatterns.AttributeMarkers;
    public static string[] TestAssertionKeywords => Patterns.TestSilencePatterns.AssertionKeywords;

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
        public static string[] HttpContextSignals => Patterns.DataIntegrityPatterns.HttpContextSignals;
        public static string[] SqlIgnorePatterns => Patterns.DataIntegrityPatterns.SqlIgnorePatterns;
        public static string[] UncheckedCastPatterns => Patterns.DataIntegrityPatterns.UncheckedCastPatterns;
        public static bool HasHttpContextSignal(string content) => Patterns.DataIntegrityPatterns.HasHttpContextSignal(content);
    }

    public static class PiiDetectionPatterns
    {
        public static string[] PiiTerms => Patterns.PiiDetectionPatterns.PiiTerms;
        public static string[] LogPrefixes => Patterns.PiiDetectionPatterns.LogPrefixes;
        public static string[] TransformationPatterns => Patterns.PiiDetectionPatterns.TransformationPatterns;
        public static string[] ReflectionGuards => Patterns.PiiDetectionPatterns.ReflectionGuards;
        public static bool IsDataTransformed(string content) => Patterns.PiiDetectionPatterns.IsDataTransformed(content);
    }

    public static class IdempotencyPatterns
    {
        public static string[] IdempotencySignals => Patterns.IdempotencyPatterns.IdempotencySignals;
        public static string[] UpsertPatterns => Patterns.IdempotencyPatterns.UpsertPatterns;
    }

    public static class ResourcePatterns
    {
        public static string[] DisposableTypes => Patterns.ResourcePatterns.DisposableTypes;
        public static string[] DisposableSuffixes => Patterns.ResourcePatterns.DisposableSuffixes;
        public static HashSet<string> OwnedByOtherRules => Patterns.ResourcePatterns.OwnedByOtherRules;
        public static HashSet<string> KnownNonDisposableTypes => Patterns.ResourcePatterns.KnownNonDisposableTypes;
        public static Regex NewTypeRegex => Patterns.ResourcePatterns.NewTypeRegex;
    }

    public static class ExternalServicePatterns
    {
        public static string[] HttpCallMethods => Patterns.ExternalServicePatterns.HttpCallMethods;
        public static string[] CtCheckHttpMethods => Patterns.ExternalServicePatterns.CtCheckHttpMethods;
        public static string[] PollyPatterns => Patterns.ExternalServicePatterns.PollyPatterns;
        public static string[] FactoryConfigPatterns => Patterns.ExternalServicePatterns.FactoryConfigPatterns;
        public static string[] FireAndForgetPatterns => Patterns.ExternalServicePatterns.FireAndForgetPatterns;
    }

    public static class PerformancePatterns
    {
        public static string[] LinqMethods => Patterns.PerformancePatterns.LinqMethods;
        public static string[] LoopKeywords => Patterns.PerformancePatterns.LoopKeywords;
        public static string[] UnboundedLoopKeywords => Patterns.PerformancePatterns.UnboundedLoopKeywords;
        public static bool HasLinqCall(string content) => Patterns.PerformancePatterns.HasLinqCall(content);
        public static bool HasLoopConstruct(string content) => Patterns.PerformancePatterns.HasLoopConstruct(content);
        public static bool IsRuleImplementationFile(string path) => Patterns.PerformancePatterns.IsRuleImplementationFile(path);
    }

    public static class FloatingPointPatterns
    {
        public static Regex FloatLiteralOnRightRegex => Patterns.FloatingPointPatterns.FloatLiteralOnRightRegex;
        public static Regex FloatLiteralOnLeftRegex => Patterns.FloatingPointPatterns.FloatLiteralOnLeftRegex;
        public static Regex FloatCastWithEqualityRegex => Patterns.FloatingPointPatterns.FloatCastWithEqualityRegex;
        public static Regex FloatTypeWithEqualityRegex => Patterns.FloatingPointPatterns.FloatTypeWithEqualityRegex;
        public static Regex IntegerZeroGuardRegex => Patterns.FloatingPointPatterns.IntegerZeroGuardRegex;
        public static bool IsGuardedIntegerZeroCheck(string content) => Patterns.FloatingPointPatterns.IsGuardedIntegerZeroCheck(content);
    }

    public static class DataSchemaPatterns
    {
        public static string[] SerializationAttributes => Patterns.DataSchemaPatterns.SerializationAttributes;
    }

    public static class ExceptionPatterns
    {
        public static string[] ThrowAssertions => Patterns.ExceptionPatterns.ThrowAssertions;
        public static string[] GuardClauseThrows => Patterns.ExceptionPatterns.GuardClauseThrows;
    }

    public static class DependencyInjectionPatterns
    {
        public static string[] ServiceLocatorPatterns => Patterns.DependencyInjectionPatterns.ServiceLocatorPatterns;
        public static string[] DirectInstantiationExclusions => Patterns.DependencyInjectionPatterns.DirectInstantiationExclusions;
        public static Regex DirectInstantiationRegex => Patterns.DependencyInjectionPatterns.DirectInstantiationRegex;
        public static bool IsInfrastructureFile(string path) => Patterns.DependencyInjectionPatterns.IsInfrastructureFile(path);
    }

    public static class StubDetectionPatterns
    {
        public static string[] StubKeywords => Patterns.StubDetectionPatterns.StubKeywords;
    }

    public static class ArchitecturePatterns
    {
        public static Regex UsingRegex => Patterns.ArchitecturePatterns.UsingRegex;
    }

    public static class FrameworkPatterns
    {
        public static string[] MvvmPatterns => Patterns.FrameworkPatterns.MvvmPatterns;
        public static string[] DiContainerPatterns => Patterns.FrameworkPatterns.DiContainerPatterns;
        public static string[] HttpBindingPatterns => Patterns.FrameworkPatterns.HttpBindingPatterns;
        public static string[] ExceptionHandlingPatterns => Patterns.FrameworkPatterns.ExceptionHandlingPatterns;
    }

    public static class TestPatterns
    {
        public static string[] SetupTeardownAttributes => Patterns.TestPatterns.SetupTeardownAttributes;
        public static string[] MockObjectPatterns => Patterns.TestPatterns.MockObjectPatterns;
        public static string[] TestFixturePatterns => Patterns.TestPatterns.TestFixturePatterns;
        public static string[] AssertionLibraryPatterns => Patterns.TestPatterns.AssertionLibraryPatterns;
    }

    public static class CodePatterns
    {
        public static string[] DevOnlyMarkers => Patterns.CodePatterns.DevOnlyMarkers;
        public static string[] FrameworkInitializationPatterns => Patterns.CodePatterns.FrameworkInitializationPatterns;
        public static string[] OrmAutoMapPatterns => Patterns.CodePatterns.OrmAutoMapPatterns;
        public static string[] DtoMapperPatterns => Patterns.CodePatterns.DtoMapperPatterns;
        public static string[] RetryPolicyPatterns => Patterns.CodePatterns.RetryPolicyPatterns;
        public static string[] BuilderPatterns => Patterns.CodePatterns.BuilderPatterns;
        public static string[] InterfaceImplementationPatterns => Patterns.CodePatterns.InterfaceImplementationPatterns;
        public static string[] InfrastructureMarkers => Patterns.CodePatterns.InfrastructureMarkers;
        public static string[] InternalMarkers => Patterns.CodePatterns.InternalMarkers;
        public static string[] DiCompositionRootMarkers => Patterns.CodePatterns.DiCompositionRootMarkers;
        public static string[] OrmAsyncPatterns => Patterns.CodePatterns.OrmAsyncPatterns;
        public static string[] FireAndForgetBackgroundPatterns => Patterns.CodePatterns.FireAndForgetBackgroundPatterns;
        public static string[] BoundedSynchronizationPatterns => Patterns.CodePatterns.BoundedSynchronizationPatterns;
        public static string[] InstanceScopedCachePatterns => Patterns.CodePatterns.InstanceScopedCachePatterns;
    }

    // ===== Helper Method Delegations =====

    public static bool HasHttpContextSignal(string content) =>
        Patterns.DataIntegrityPatterns.HasHttpContextSignal(content);
}




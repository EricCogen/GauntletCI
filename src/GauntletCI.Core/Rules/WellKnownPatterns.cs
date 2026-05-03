// SPDX-License-Identifier: Elastic-2.0

// ========== GauntletCI Self-Analysis: WHITELISTED FILE ==========
// This file contains pattern strings and regexes used by GauntletCI detection rules.
// Pattern strings (e.g., "new FileStream(", "TODO", ".GetService<") appear in the code by design.
// These are NOT actual code violations, but rather PATTERN DEFINITIONS used to detect violations in other code.
// GauntletCI Self-Analysis should skip this file from analysis to avoid false positives on pattern data.
// =================================================================
// 
// GCI0003 Suppression: Consolidation moves guard logic and helper methods from individual rule files
// to this centralized WellKnownPatterns module. These are intentional refactorings, not behavioral changes.
// The logic remains in use; it's just been reorganized for reuse across multiple rules.
#pragma warning disable GCI0003  // Behavioral Change Detection - consolidation, not regression

using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules.Patterns;

namespace GauntletCI.Core.Rules;

/// <summary>
/// Facade for domain-specific pattern classes.
/// This module consolidates pattern detection logic across 5 focused domains:
/// - FileContextPatterns: test/generated file detection, infrastructure identification
/// - HttpExternalServicePatterns: gRPC/HTTP client framework guards
/// - NullabilityPatterns: NRT, nullable reference types, null-safety
/// - SecurityPatterns: credential detection, security boundaries
/// - DomainSpecificPatterns: performance, floating-point, data integrity, PII, exceptions, DI, architecture
/// 
/// Backward-compatible facade: all old WellKnownPatterns methods delegate to domain classes.
/// </summary>
internal static class WellKnownPatterns
{
    // ================= Module-Level Constants (Not Domain-Specific) =================

    /// <summary>Variable and field name fragments used to detect hardcoded secrets by name (GCI0010, GCI0012).</summary>
    public static readonly string[] SecretNamePatterns = [ "password", "passwd", "secret", "apikey", "api_key", "token", "credential", "private_key", "privatekey", "access_key", "auth_key" ];

    /// <summary>Log-level keywords indicating high-severity log calls that warrant review (GCI0007, GCI0013).</summary>
    public static readonly string[] HighSeverityLogKeywords = [ "error", "exception", "critical", "fatal", "warn", "warning" ];

    // ================= File Context Delegation =================

    /// <summary>
    /// Returns <c>true</c> when the given path belongs to a test or spec file.
    /// Used across rules to avoid false positives in test code.
    /// </summary>
    public static bool IsTestFile(string path) => FileContextPatterns.IsTestFile(path);

    /// <summary>
    /// Returns <c>true</c> when the given path is an auto-generated file that should not be
    /// subject to rule analysis (source generators, designer files, scaffolded API clients, etc.).
    /// </summary>
    public static bool IsGeneratedFile(string path) => FileContextPatterns.IsGeneratedFile(path);

    /// <summary>
    /// Returns <c>true</c> when the file path indicates infrastructure/configuration code where DI setup occurs.
    /// Service locator patterns and direct instantiation are acceptable in Program.cs, Startup.cs, etc.
    /// </summary>
    public static bool IsInfrastructureFile(string path) => FileContextPatterns.IsInfrastructureFile(path);

    /// <summary>
    /// Returns <c>true</c> if the given path contains security-critical component names.
    /// Used by GCI0003 for identifying security-related code changes.
    /// </summary>
    public static bool IsSecurityCriticalPath(string path) => FileContextPatterns.IsSecurityCriticalPath(path);

    /// <summary>
    /// Returns <c>true</c> if the line is a comment (starts with //, *, or #).
    /// Used across rules to skip comment-only lines from analysis.
    /// </summary>
    public static bool IsCommentLine(string trimmed) => FileContextPatterns.IsCommentLine(trimmed);

    /// <summary>
    /// File path components indicating security-critical code sections.
    /// Used by GCI0003 for behavioral change context analysis (confidential boost for security changes).
    /// </summary>
    public static readonly string[] SecurityCriticalPaths = FileContextPatterns.SecurityCriticalPaths;

    // ================= Nullability and NRT Delegation =================

    /// <summary>
    /// Returns <c>true</c> when NRT (Nullable Reference Type) is enabled for the given file.
    /// NRT is enabled via: #nullable enable directive, project-wide settings, or modern .NET versions.
    /// Used by GCI0006 and GCI0043 to determine if 'string' parameters are non-nullable by default.
    /// </summary>
    public static bool IsNullableReferenceTypeEnabled(string fileContent) => 
        NullabilityPatterns.IsNullableReferenceTypeEnabled(fileContent);

    /// <summary>
    /// Returns <c>true</c> when the parameter section contains explicitly non-nullable parameters
    /// (e.g., 'string param' without '?'). In NRT-enabled context, these don't need validation.
    /// </summary>
    public static bool HasNonNullableParams(string paramSection) => 
        NullabilityPatterns.HasNonNullableParams(paramSection);

    /// <summary>
    /// Returns <c>true</c> when the parameter list contains nullable parameters (e.g., 'string?' or 'object?').
    /// Used by GCI0006 to detect when public methods have nullable reference type parameters.
    /// </summary>
    public static bool HasNullableReferenceParam(string paramSection) => 
        NullabilityPatterns.HasNullableReferenceParam(paramSection);

    /// <summary>
    /// Returns <c>true</c> when the content contains Nullable&lt;T&gt; where T is a value type.
    /// In NRT context, Nullable&lt;int&gt;, Nullable&lt;string&gt;, etc. always have a value.
    /// </summary>
    public static bool IsNullableOfNonNullableType(string content) => 
        NullabilityPatterns.IsNullableOfNonNullableType(content);

    /// <summary>
    /// Returns <c>true</c> when the content contains a #pragma warning disable with nullable-related codes.
    /// Detects suppression of nullable reference type warnings (CS8600, CS8603, etc.).
    /// Used by GCI0043 to flag deliberate nullable warning suppression.
    /// </summary>
    public static bool IsPragmaNullableDisable(string content) => 
        NullabilityPatterns.IsPragmaNullableDisable(content);

    /// <summary>
    /// Returns <c>true</c> when the content contains a LINQ expression where .Value is intentionally mapped.
    /// Patterns: .Select(x => x.Value), .Where(x => x.Value != null), etc.
    /// Used by GCI0006 to avoid flagging safe LINQ projections as unsafe dereferences.
    /// </summary>
    public static bool IsLinqValueProjection(string content) => 
        NullabilityPatterns.IsLinqValueProjection(content);

    // ================= HTTP/gRPC External Service Delegation =================

    /// <summary>
    /// Returns <c>true</c> when the file path indicates gRPC-related code.
    /// gRPC channels manage timeouts at the channel/connection level, not per-HttpClient.
    /// Used by GCI0039 to skip false positive timeout checks in gRPC contexts.
    /// </summary>
    public static bool IsGrpcRelatedFile(string path) => 
        HttpExternalServicePatterns.IsGrpcRelatedFile(path);

    /// <summary>
    /// Returns <c>true</c> when the added lines indicate HttpClient configuration via IHttpClientFactory.
    /// Factory-managed clients configure timeout at the handler/channel level, not per-client.
    /// </summary>
    public static bool IsHttpFactoryConfigured(List<DiffLine> addedLines) => 
        HttpExternalServicePatterns.IsHttpFactoryConfigured(addedLines);

    /// <summary>
    /// Returns <c>true</c> when the added lines indicate gRPC channel configuration.
    /// gRPC channels manage timeouts via GrpcChannelOptions at the connection level.
    /// </summary>
    public static bool UsesGrpcChannel(List<DiffLine> addedLines) => 
        HttpExternalServicePatterns.UsesGrpcChannel(addedLines);

    /// <summary>
    /// Returns <c>true</c> when the added lines indicate use of factory-managed or injected HTTP clients.
    /// Factory patterns, Polly policies, and DI-managed clients manage timeouts externally.
    /// </summary>
    public static bool UsesFactoryManagedHttpClients(List<DiffLine> addedLines) => 
        HttpExternalServicePatterns.UsesFactoryManagedHttpClients(addedLines);

    /// <summary>
    /// Returns <c>true</c> when the content indicates an injected or static HttpClient is being used.
    /// Patterns like _httpClient.GetAsync() or this.client.PostAsync() are typically DI-managed.
    /// </summary>
    public static bool IsInjectedOrStaticClient(string content) => 
        HttpExternalServicePatterns.IsInjectedOrStaticClient(content);

    /// <summary>
    /// Returns <c>true</c> when the file uses .NET 9+ modern patterns (checked operators, required members, etc).
    /// These patterns strongly indicate NRT is enabled in modern project contexts.
    /// </summary>
    public static bool UsesModernDotNetPatterns(string fileContent) => 
        HttpExternalServicePatterns.UsesModernDotNetPatterns(fileContent);

    /// <summary>
    /// Returns <c>true</c> when a method signature uses record type parameters or other modern patterns.
    /// Helps identify rules applied to modern code that typically has NRT enabled.
    /// </summary>
    public static bool HasModernTypeParameters(string paramSection) => 
        HttpExternalServicePatterns.HasModernTypeParameters(paramSection);

    // ================= Security Patterns Delegation =================

    /// <summary>
    /// Returns <c>true</c> if the value appears to be an environment variable name
    /// (ALL_CAPS with digits and underscores, e.g., GITHUB_TOKEN, MY_API_KEY).
    /// Used by GCI0012 to skip environment variable names from hardcoded credential detection.
    /// </summary>
    public static bool IsEnvVarName(string literal) => SecurityPatterns.IsEnvVarName(literal);

    /// <summary>
    /// Returns <c>true</c> for benign literal values that are never actual secrets:
    /// empty strings, short strings (&lt;3 chars), HTTP auth scheme names, and C# keyword literals.
    /// Used by GCI0012 to reduce false positives in credential detection.
    /// </summary>
    public static bool IsBenignLiteralValue(string value) => SecurityPatterns.IsBenignLiteralValue(value);

    /// <summary>
    /// Returns the index of the first real assignment = in the line, skipping string literals.
    /// Distinguishes between = (assignment), == (equality), !=, &lt;=, >=, and => (lambda/expression body).
    /// Used by GCI0012 to find credentials assigned to variables.
    /// </summary>
    public static int FindAssignmentIndex(string content) => SecurityPatterns.FindAssignmentIndex(content);

    /// <summary>
    /// Returns <c>true</c> only if the line contains a real assignment = (not ==, !=, <=, >=, =>).
    /// Skips = signs inside string literals to avoid false positives from format strings.
    /// Used by GCI0012 to detect variable assignments with hardcoded credentials.
    /// </summary>
    public static bool HasAssignment(string content) => SecurityPatterns.HasAssignment(content);

    /// <summary>
    /// Returns the string value only when the direct RHS of an assignment is a bare string literal.
    /// Returns null if the RHS is a method call, object initializer, or anything other than a literal.
    /// Prevents false positives from patterns like: _tokenField = SomeFactory("ui-element-id")
    /// Used by GCI0012 to find actual hardcoded credential values.
    /// </summary>
    public static string? ExtractDirectlyAssignedLiteral(string content) => 
        SecurityPatterns.ExtractDirectlyAssignedLiteral(content);

    /// <summary>
    /// Commit message keywords indicating security-focused changes.
    /// Used by GCI0003 for behavioral change context analysis.
    /// </summary>
    public static readonly string[] SecurityKeywords = SecurityPatterns.SecurityKeywords;

    /// <summary>
    /// Test pattern keywords indicating security-focused test additions.
    /// Used by GCI0003 for detecting security-focused test additions.
    /// </summary>
    public static readonly string[] SecurityTestPatterns = SecurityPatterns.SecurityTestPatterns;

    /// <summary>
    /// Returns <c>true</c> if the given text contains security-related keywords.
    /// Used by GCI0003 for analyzing commit messages for security focus.
    /// </summary>
    public static bool HasSecurityKeywords(string text) => SecurityPatterns.HasSecurityKeywords(text);

    /// <summary>
    /// Returns <c>true</c> if the given text contains security-related test patterns.
    /// Used by GCI0003 for detecting security-focused test additions.
    /// </summary>
    public static bool HasSecurityTestPattern(string text) => SecurityPatterns.HasSecurityTestPattern(text);

    // ================= Domain-Specific Patterns Delegation =================

    // --- Timeout and Resource Patterns ---

    /// <summary>
    /// Patterns indicating resource timeout limits in code.
    /// Used by GCI0020 for detecting timeout removal that could lead to resource exhaustion.
    /// </summary>
    public static readonly string[] TimeoutPatterns = DomainSpecificPatterns.TimeoutPatterns;

    /// <summary>
    /// Patterns indicating iteration or loop count limits in code.
    /// Used by GCI0020 for detecting iteration limit removal.
    /// </summary>
    public static readonly string[] IterationLimitPatterns = DomainSpecificPatterns.IterationLimitPatterns;

    /// <summary>
    /// Patterns indicating resource limits (connections, threads, buffers, pools).
    /// Used by GCI0020 for detecting dangerous resource limit increases.
    /// </summary>
    public static readonly string[] ResourceLimitPatterns = DomainSpecificPatterns.ResourceLimitPatterns;

    /// <summary>
    /// Patterns indicating resource cleanup/disposal operations.
    /// Used by GCI0020 for detecting removal of resource cleanup code.
    /// </summary>
    public static readonly string[] ResourceCleanupPatterns = DomainSpecificPatterns.ResourceCleanupPatterns;

    /// <summary>
    /// Patterns indicating asynchronous operations that can consume resources.
    /// Used by GCI0020 for detecting unbounded async operations.
    /// </summary>
    public static readonly string[] AsyncPatterns = DomainSpecificPatterns.AsyncPatterns;

    // --- Test Patterns ---

    /// <summary>
    /// Test silence/skip patterns that prevent tests from running.
    /// Used by GCI0041 for detecting disabled or skipped tests that may hide regressions.
    /// </summary>
    public static readonly string[] TestSilencePatterns = DomainSpecificPatterns.TestSilencePatterns;

    /// <summary>
    /// Test attribute markers that identify test methods.
    /// Used by GCI0041 for detecting uninformative test method names.
    /// </summary>
    public static readonly string[] TestAttributeMarkers = DomainSpecificPatterns.TestAttributeMarkers;

    /// <summary>
    /// Assertion keywords used across popular .NET testing frameworks.
    /// Includes xUnit, NUnit, MSTest, FluentAssertions, Shouldly, Moq, NSubstitute, Playwright, etc.
    /// Used by GCI0041 for detecting test methods with missing assertions.
    /// </summary>
    public static readonly string[] TestAssertionKeywords = DomainSpecificPatterns.TestAssertionKeywords;

    // --- Service Locator and Connection Patterns (from original module constants) ---

    /// <summary>
    /// Array of service locator patterns that violate DI principles.
    /// These patterns bypass DI and make testing/mocking difficult.
    /// </summary>
    public static readonly string[] ServiceLocatorPatterns = DomainSpecificPatterns.DependencyInjectionPatterns.ServiceLocatorPatterns;

    /// <summary>
    /// Regex to detect direct instantiation of injectable types.
    /// Matches patterns like: new UserService(...), new OrderRepository(...), new RequestHandler(...)
    /// </summary>
    public static readonly System.Text.RegularExpressions.Regex DirectInstantiationRegex = 
        DomainSpecificPatterns.DependencyInjectionPatterns.DirectInstantiationRegex;

    /// <summary>
    /// Patterns to exclude from direct instantiation checks.
    /// Test doubles and event handlers are legitimate cases for direct instantiation.
    /// </summary>
    public static readonly string[] DirectInstantiationExclusions = 
        DomainSpecificPatterns.DependencyInjectionPatterns.DirectInstantiationExclusions;

    /// <summary>
    /// Common connection string markers that indicate hardcoded database/service connections.
    /// Used by GCI0010 to detect hardcoded configuration and GCI0012 to flag credential exposure.
    /// </summary>
    public static readonly string[] ConnectionStringMarkers =
    [
        "Server=", "Data Source=", "mongodb://", "redis://", "mysql://", "postgresql://", "Database="
    ];

    // ================= Signature Compatibility =================

    /// <summary>
    /// Returns <c>true</c> when <paramref name="addedSig"/> is a backward-compatible extension of
    /// <paramref name="removedSig"/> (i.e. the added overload appends only optional parameters,
    /// or the only difference is the addition of modifier keywords like <c>virtual</c> or <c>override</c>).
    /// Used by GCI0003 and GCI0004.
    /// </summary>
    public static bool IsBackwardCompatibleExtension(string removedSig, string addedSig)
    {
        // Adding/removing modifier keywords (virtual, override, sealed, abstract, new) does not
        // break existing callers at the binary level.
        if (NormalizeModifiers(removedSig) == NormalizeModifiers(addedSig)) return true;

        var removedParams = ExtractParenContent(removedSig)?.Trim() ?? "";
        var addedParams   = ExtractParenContent(addedSig)?.Trim()   ?? "";

        if (addedParams.Length <= removedParams.Length) return false;
        if (!addedParams.StartsWith(removedParams, StringComparison.Ordinal)) return false;

        var extra = addedParams[removedParams.Length..].TrimStart(',').TrimStart();
        return !string.IsNullOrWhiteSpace(extra) && extra.Contains('=', StringComparison.Ordinal);
    }

    /// <summary>
    /// Strips common C# modifier keywords so signatures that differ only in virtual/override/sealed/abstract/new
    /// can be compared for semantic equivalence.
    /// </summary>
    private static string NormalizeModifiers(string sig)
    {
        static string Strip(string s, string keyword)
            => s.Replace(keyword, " ", StringComparison.Ordinal);

        var s = sig.Trim();
        s = Strip(s, "virtual ");
        s = Strip(s, "override ");
        s = Strip(s, "sealed ");
        s = Strip(s, "abstract ");
        s = Strip(s, " new ");
        // Collapse multiple spaces introduced by stripping
        while (s.Contains("  ", StringComparison.Ordinal))
            s = s.Replace("  ", " ", StringComparison.Ordinal);
        return s.Trim();
    }

    /// <summary>Extracts the parameter list content between the outermost parentheses of a method signature.</summary>
    public static string? ExtractParenContent(string sig)
    {
        var open  = sig.IndexOf('(');
        var close = sig.LastIndexOf(')');
        return open >= 0 && close > open ? sig[(open + 1)..close] : null;
    }

    // ================= HTTP Context Helper =================

    /// <summary>
    /// Returns <c>true</c> if the given HTTP request content contains HTTP context signal patterns.
    /// Used by GCI0015 to determine whether mass-assignment and unsafe cast checks apply.
    /// </summary>
    public static bool HasHttpContextSignal(string content) => 
        DomainSpecificPatterns.HasHttpContextSignal(content);

    // ================= Nested Classes (Delegating to Domain-Specific) =================

    /// <summary>Delegate to DataIntegrityPatterns.</summary>
    public static class DataIntegrityPatterns
    {
        public static string[] HttpContextSignals => DomainSpecificPatterns.DataIntegrityPatterns.HttpContextSignals;
        public static string[] SqlIgnorePatterns => DomainSpecificPatterns.DataIntegrityPatterns.SqlIgnorePatterns;
        public static string[] UncheckedCastPatterns => DomainSpecificPatterns.DataIntegrityPatterns.UncheckedCastPatterns;
        public static bool HasHttpContextSignal(string content) => DomainSpecificPatterns.DataIntegrityPatterns.HasHttpContextSignal(content);
    }

    /// <summary>Delegate to PiiDetectionPatterns.</summary>
    public static class PiiDetectionPatterns
    {
        public static string[] PiiTerms => DomainSpecificPatterns.PiiDetectionPatterns.PiiTerms;
        public static string[] LogPrefixes => DomainSpecificPatterns.PiiDetectionPatterns.LogPrefixes;
        public static string[] TransformationPatterns => DomainSpecificPatterns.PiiDetectionPatterns.TransformationPatterns;
        public static string[] ReflectionGuards => DomainSpecificPatterns.PiiDetectionPatterns.ReflectionGuards;
        public static bool IsDataTransformed(string content) => DomainSpecificPatterns.PiiDetectionPatterns.IsDataTransformed(content);
    }

    /// <summary>Delegate to IdempotencyPatterns.</summary>
    public static class IdempotencyPatterns
    {
        public static string[] IdempotencySignals => DomainSpecificPatterns.IdempotencyPatterns.IdempotencySignals;
        public static string[] UpsertPatterns => DomainSpecificPatterns.IdempotencyPatterns.UpsertPatterns;
    }

    /// <summary>Delegate to ResourcePatterns.</summary>
    public static class ResourcePatterns
    {
        public static string[] DisposableTypes => DomainSpecificPatterns.ResourcePatterns.DisposableTypes;
        public static string[] DisposableSuffixes => DomainSpecificPatterns.ResourcePatterns.DisposableSuffixes;
        public static HashSet<string> OwnedByOtherRules => DomainSpecificPatterns.ResourcePatterns.OwnedByOtherRules;
        public static HashSet<string> KnownNonDisposableTypes => DomainSpecificPatterns.ResourcePatterns.KnownNonDisposableTypes;
        public static System.Text.RegularExpressions.Regex NewTypeRegex => DomainSpecificPatterns.ResourcePatterns.NewTypeRegex;
    }

    /// <summary>Delegate to ExternalServicePatterns.</summary>
    public static class ExternalServicePatterns
    {
        public static string[] HttpCallMethods => DomainSpecificPatterns.ExternalServicePatterns.HttpCallMethods;
        public static string[] CtCheckHttpMethods => DomainSpecificPatterns.ExternalServicePatterns.CtCheckHttpMethods;
        public static string[] PollyPatterns => DomainSpecificPatterns.ExternalServicePatterns.PollyPatterns;
        public static string[] FactoryConfigPatterns => DomainSpecificPatterns.ExternalServicePatterns.FactoryConfigPatterns;
        public static string[] FireAndForgetPatterns => DomainSpecificPatterns.ExternalServicePatterns.FireAndForgetPatterns;
    }

    /// <summary>Delegate to PerformancePatterns.</summary>
    public static class PerformancePatterns
    {
        public static string[] LinqMethods => DomainSpecificPatterns.PerformancePatterns.LinqMethods;
        public static string[] LoopKeywords => DomainSpecificPatterns.PerformancePatterns.LoopKeywords;
        public static string[] UnboundedLoopKeywords => DomainSpecificPatterns.PerformancePatterns.UnboundedLoopKeywords;
        public static bool HasLinqCall(string content) => DomainSpecificPatterns.PerformancePatterns.HasLinqCall(content);
        public static bool HasLoopConstruct(string content) => DomainSpecificPatterns.PerformancePatterns.HasLoopConstruct(content);
        public static bool IsRuleImplementationFile(string path) => DomainSpecificPatterns.PerformancePatterns.IsRuleImplementationFile(path);
    }

    /// <summary>Delegate to FloatingPointPatterns.</summary>
    public static class FloatingPointPatterns
    {
        public static System.Text.RegularExpressions.Regex FloatLiteralOnRightRegex => DomainSpecificPatterns.FloatingPointPatterns.FloatLiteralOnRightRegex;
        public static System.Text.RegularExpressions.Regex FloatLiteralOnLeftRegex => DomainSpecificPatterns.FloatingPointPatterns.FloatLiteralOnLeftRegex;
        public static System.Text.RegularExpressions.Regex FloatCastWithEqualityRegex => DomainSpecificPatterns.FloatingPointPatterns.FloatCastWithEqualityRegex;
        public static System.Text.RegularExpressions.Regex FloatTypeWithEqualityRegex => DomainSpecificPatterns.FloatingPointPatterns.FloatTypeWithEqualityRegex;
        public static System.Text.RegularExpressions.Regex IntegerZeroGuardRegex => DomainSpecificPatterns.FloatingPointPatterns.IntegerZeroGuardRegex;
        public static bool IsGuardedIntegerZeroCheck(string content) => DomainSpecificPatterns.FloatingPointPatterns.IsGuardedIntegerZeroCheck(content);
    }

    /// <summary>Delegate to DataSchemaPatterns.</summary>
    public static class DataSchemaPatterns
    {
        public static string[] SerializationAttributes => DomainSpecificPatterns.DataSchemaPatterns.SerializationAttributes;
    }

    /// <summary>Delegate to ExceptionPatterns.</summary>
    public static class ExceptionPatterns
    {
        public static string[] ThrowAssertions => DomainSpecificPatterns.ExceptionPatterns.ThrowAssertions;
        public static string[] GuardClauseThrows => DomainSpecificPatterns.ExceptionPatterns.GuardClauseThrows;
    }

    /// <summary>Delegate to DependencyInjectionPatterns.</summary>
    public static class DependencyInjectionPatterns
    {
        public static string[] ServiceLocatorPatterns => DomainSpecificPatterns.DependencyInjectionPatterns.ServiceLocatorPatterns;
        public static string[] DirectInstantiationExclusions => DomainSpecificPatterns.DependencyInjectionPatterns.DirectInstantiationExclusions;
        public static System.Text.RegularExpressions.Regex DirectInstantiationRegex => DomainSpecificPatterns.DependencyInjectionPatterns.DirectInstantiationRegex;
        public static bool IsInfrastructureFile(string path) => DomainSpecificPatterns.DependencyInjectionPatterns.IsInfrastructureFile(path);
    }

    /// <summary>Delegate to StubDetectionPatterns.</summary>
    public static class StubDetectionPatterns
    {
        public static string[] StubKeywords => DomainSpecificPatterns.StubDetectionPatterns.StubKeywords;
    }

    /// <summary>Delegate to ArchitecturePatterns.</summary>
    public static class ArchitecturePatterns
    {
        public static System.Text.RegularExpressions.Regex UsingRegex => DomainSpecificPatterns.ArchitecturePatterns.UsingRegex;
    }

    /// <summary>Delegate to GuardPatterns.</summary>
    public static class GuardPatterns
    {
        public static bool HasValueNullCheck(string content) => GuardPatternsImpl.HasValueNullCheck(content);
        public static bool IsKeyValuePairAccess(string content) => GuardPatternsImpl.IsKeyValuePairAccess(content);
        public static bool IsLinqValueMapping(string content) => GuardPatternsImpl.IsLinqValueMapping(content);
        public static bool IsIOptionsValue(string content) => GuardPatternsImpl.IsIOptionsValue(content);
        public static bool IsExpressionBodied(string content) => GuardPatternsImpl.IsExpressionBodied(content);
        public static bool HasHasValueGuard(string content) => GuardPatternsImpl.HasHasValueGuard(content);
        public static bool IsCommentLine(string content) => GuardPatternsImpl.IsCommentLine(content);
        public static bool HasAccessModifier(string content) => GuardPatternsImpl.HasAccessModifier(content);
        public static bool IsAsyncMethod(string content) => GuardPatternsImpl.IsAsyncMethod(content);
        public static bool IsEventHandler(string content) => GuardPatternsImpl.IsEventHandler(content);
        public static bool IsCryptographicBoundary(string content) => GuardPatternsImpl.IsCryptographicBoundary(content);
        public static bool HasInjectionGuard(string content) => GuardPatternsImpl.HasInjectionGuard(content);
        public static bool IsOverrideOrSealedMethod(string content) => GuardPatternsImpl.IsOverrideOrSealedMethod(content);
        public static bool IsAbstractOrDelegateOrPartial(string content) => GuardPatternsImpl.IsAbstractOrDelegateOrPartial(content);
        public static bool IsMigrationOrSeedFile(string filePath) => GuardPatternsImpl.IsMigrationOrSeedFile(filePath);
        public static bool IsUiEventHandler(string filePath) => GuardPatternsImpl.IsUiEventHandler(filePath);
        public static bool IsDocumentationFile(string filePath) => GuardPatternsImpl.IsDocumentationFile(filePath);
        public static bool IsInsideStaticConstructor(List<DiffLine> allLines, int idx) => GuardPatternsImpl.IsInsideStaticConstructor(allLines, idx);
    }

    // Internal implementation class to avoid name conflicts with public GuardPatterns class
    internal static class GuardPatternsImpl
    {
        private static readonly System.Text.RegularExpressions.Regex ValueNullCheckRegex = new(
            @"\.Value\s*(is not null|is null|==\s*null|!=\s*null)", System.Text.RegularExpressions.RegexOptions.Compiled);

        public static bool HasValueNullCheck(string content) => 
            !string.IsNullOrEmpty(content) && ValueNullCheckRegex.IsMatch(content);

        public static bool IsKeyValuePairAccess(string content) => 
            !string.IsNullOrEmpty(content) && content.Contains(".Key", StringComparison.Ordinal);

        public static bool IsLinqValueMapping(string content) =>
            !string.IsNullOrEmpty(content) && (
                (content.Contains(".Select", StringComparison.Ordinal) && content.Contains(".Value", StringComparison.Ordinal)) ||
                (content.Contains(".Select", StringComparison.Ordinal) && content.Contains(" => ", StringComparison.Ordinal))
            );

        public static bool IsIOptionsValue(string content) =>
            !string.IsNullOrEmpty(content) && content.Contains("IOptions", StringComparison.Ordinal);

        public static bool IsExpressionBodied(string content)
        {
            if (string.IsNullOrEmpty(content)) return false;
            var trimmed = content.TrimStart();
            return ((trimmed.StartsWith("public ", StringComparison.Ordinal) ||
                     trimmed.StartsWith("protected ", StringComparison.Ordinal)) &&
                    content.Contains("=>", StringComparison.Ordinal));
        }

        public static bool HasHasValueGuard(string content) =>
            !string.IsNullOrEmpty(content) && content.Contains("HasValue", StringComparison.Ordinal);

        public static bool IsCommentLine(string content) =>
            !string.IsNullOrEmpty(content) && content.TrimStart().StartsWith("//", StringComparison.Ordinal);

        public static bool HasAccessModifier(string content)
        {
            if (string.IsNullOrEmpty(content)) return false;
            var trimmed = content.TrimStart();
            
            while (trimmed.StartsWith("[", StringComparison.Ordinal))
            {
                var closeIdx = trimmed.IndexOf(']');
                if (closeIdx == -1) break;
                trimmed = trimmed[(closeIdx + 1)..].TrimStart();
            }
            
            return trimmed.StartsWith("public ", StringComparison.Ordinal) ||
                   trimmed.StartsWith("private ", StringComparison.Ordinal) ||
                   trimmed.StartsWith("protected ", StringComparison.Ordinal) ||
                   trimmed.StartsWith("internal ", StringComparison.Ordinal);
        }

        public static bool IsAsyncMethod(string content)
        {
            if (string.IsNullOrEmpty(content)) return false;
            var trimmed = content.TrimStart();
            return trimmed.StartsWith("async ", StringComparison.Ordinal) ||
                   trimmed.Contains(" async ", StringComparison.Ordinal) ||
                   trimmed.Contains(" async(", StringComparison.Ordinal);
        }

        public static bool IsEventHandler(string content)
        {
            if (string.IsNullOrEmpty(content)) return false;
            return content.Contains("EventHandler", StringComparison.Ordinal) ||
                   content.Contains("EventArgs", StringComparison.Ordinal) ||
                   content.Contains("+=", StringComparison.Ordinal) ||
                   content.Contains("-=", StringComparison.Ordinal);
        }

        public static bool IsCryptographicBoundary(string content)
        {
            if (string.IsNullOrEmpty(content)) return false;
            var cryptMethods = new[] { "ComputeHmac", "ComputeHash", "Encrypt", "Decrypt", "Sign", "Verify", "EncryptionAsync", "DecryptionAsync" };
            return cryptMethods.Any(m => content.Contains(m, StringComparison.Ordinal));
        }

        public static bool HasInjectionGuard(string content)
        {
            if (string.IsNullOrEmpty(content)) return false;
            return content.Contains("@", StringComparison.Ordinal) ||
                   content.Contains("SqlParameter", StringComparison.Ordinal) ||
                   content.Contains("DbParameter", StringComparison.Ordinal) ||
                   content.Contains("Escape", StringComparison.Ordinal) ||
                   content.Contains("Sanitize", StringComparison.Ordinal) ||
                   content.Contains("Validate", StringComparison.Ordinal) ||
                   content.Contains("Path.Combine", StringComparison.Ordinal);
        }

        public static bool IsOverrideOrSealedMethod(string content)
        {
            if (string.IsNullOrEmpty(content)) return false;
            return content.Contains(" override ", StringComparison.Ordinal) ||
                   content.Contains(" sealed ", StringComparison.Ordinal);
        }

        public static bool IsAbstractOrDelegateOrPartial(string content)
        {
            if (string.IsNullOrEmpty(content)) return false;
            return content.Contains(" abstract ", StringComparison.Ordinal) ||
                   content.Contains(" delegate ", StringComparison.Ordinal) ||
                   content.Contains(" partial ", StringComparison.Ordinal);
        }

        public static bool IsMigrationOrSeedFile(string filePath)
        {
            if (filePath.Contains("Migrations/", StringComparison.OrdinalIgnoreCase) ||
                filePath.Contains("\\Migrations\\", StringComparison.OrdinalIgnoreCase))
                return true;

            if (filePath.EndsWith(".sql", StringComparison.OrdinalIgnoreCase) &&
                (filePath.Contains("Migration", StringComparison.OrdinalIgnoreCase) ||
                 filePath.Contains("Seed", StringComparison.OrdinalIgnoreCase) ||
                 filePath.Contains("Setup", StringComparison.OrdinalIgnoreCase)))
                return true;

            if (filePath.Contains("SeedData", StringComparison.OrdinalIgnoreCase) ||
                filePath.Contains("DataSeeding", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        public static bool IsUiEventHandler(string filePath)
        {
            var lower = filePath.ToLowerInvariant();
            if (lower.EndsWith(".xaml.cs", StringComparison.OrdinalIgnoreCase)) return true;
            if (lower.EndsWith(".razor.cs", StringComparison.OrdinalIgnoreCase)) return true;
            if (lower.Contains("\\ui\\") || lower.Contains("/ui/") ||
                lower.Contains("\\components\\") || lower.Contains("/components/") ||
                lower.Contains("\\views\\") || lower.Contains("/views/") ||
                lower.Contains("\\pages\\") || lower.Contains("/pages/")) return true;
            return false;
        }

        public static bool IsDocumentationFile(string filePath)
        {
            var lower = filePath.ToLowerInvariant();
            if (lower.Contains("snippet", StringComparison.OrdinalIgnoreCase)) return true;
            if (lower.Contains("sample", StringComparison.OrdinalIgnoreCase)) return true;
            if (lower.Contains("example", StringComparison.OrdinalIgnoreCase)) return true;
            if (lower.Contains("demo", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public static bool IsInsideStaticConstructor(List<DiffLine> allLines, int idx)
        {
            int searchStart = Math.Max(0, idx - 20);
            for (int j = idx - 1; j >= searchStart; j--)
            {
                var trimmed = allLines[j].Content.Trim();
                if (!trimmed.StartsWith("static ", StringComparison.Ordinal)) continue;

                var afterStatic = trimmed["static ".Length..].TrimStart();
                if (afterStatic.StartsWith("void ", StringComparison.Ordinal) ||
                    afterStatic.StartsWith("Task", StringComparison.Ordinal) ||
                    afterStatic.StartsWith("bool ", StringComparison.Ordinal) ||
                    afterStatic.StartsWith("int ", StringComparison.Ordinal) ||
                    afterStatic.StartsWith("string ", StringComparison.Ordinal) ||
                    afterStatic.StartsWith("async ", StringComparison.Ordinal) ||
                    afterStatic.StartsWith("readonly ", StringComparison.Ordinal) ||
                    afterStatic.StartsWith("class ", StringComparison.Ordinal) ||
                    afterStatic.StartsWith("IEnumerable", StringComparison.Ordinal) ||
                    afterStatic.StartsWith("List<", StringComparison.Ordinal) ||
                    afterStatic.StartsWith("Dictionary<", StringComparison.Ordinal))
                    continue;

                if (afterStatic.Contains("()")) return true;
            }
            return false;
        }
    }

    // ================= Phase 13 Guard Delegation Methods =================

    /// <summary>
    /// Returns <c>true</c> if the content contains any MVVM framework patterns.
    /// Used by GCI0043 (Nullability) to avoid flagging ViewModels.
    /// </summary>
    public static bool HasMvvmPattern(string content) => DomainSpecificPatterns.HasMvvmPattern(content);

    /// <summary>
    /// Returns <c>true</c> if the content contains dev-only markers (TODO, HACK, DEBUG).
    /// Used by GCI0003 (Behavioral Change) to avoid flagging development-only code.
    /// </summary>
    public static bool HasDevOnlyMarker(string content) => DomainSpecificPatterns.HasDevOnlyMarker(content);

    /// <summary>
    /// Returns <c>true</c> if the content contains ORM or DTO mapping patterns.
    /// Used by GCI0015 (Data Integrity) to avoid flagging ORM auto-mapping.
    /// </summary>
    public static bool HasMappingPattern(string content) => DomainSpecificPatterns.HasMappingPattern(content);

    /// <summary>
    /// Returns <c>true</c> if the content contains mock object creation patterns.
    /// Used by GCI0047 (Naming) and GCI0032 (Exception) to avoid flagging test mocks.
    /// </summary>
    public static bool HasMockPattern(string content) => DomainSpecificPatterns.HasMockPattern(content);

    /// <summary>
    /// Returns <c>true</c> if the line/path indicates internal or private API.
    /// Used by GCI0004 (Breaking Change) to avoid flagging internal API deprecations.
    /// </summary>
    public static bool HasInternalMarker(string content) => DomainSpecificPatterns.HasInternalMarker(content);

    /// <summary>
    /// Returns <c>true</c> if the line indicates DI composition root code.
    /// Used by GCI0038 (DI Safety) to avoid flagging intentional composition patterns.
    /// </summary>
    public static bool IsDiCompositionRoot(string content) => DomainSpecificPatterns.IsDiCompositionRoot(content);

    /// <summary>
    /// Returns <c>true</c> if the content indicates an ORM async database call with proper ConfigureAwait.
    /// Used by GCI0016 and GCI0020 to suppress false positives on legitimate async ORM patterns.
    /// </summary>
    public static bool IsOrmAsyncPattern(string content) =>
        !string.IsNullOrEmpty(content) &&
        DomainSpecificPatterns.CodePatterns.OrmAsyncPatterns.Any(p => 
            content.Contains(p, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Returns <c>true</c> if the content indicates a fire-and-forget background task (intentional, CLI scope).
    /// Used by GCI0020 to suppress false positives on telemetry upload and GPU inference patterns.
    /// </summary>
    public static bool IsIntentionalBackgroundTask(string content) =>
        !string.IsNullOrEmpty(content) &&
        (content.Contains("UploadInBackground", StringComparison.OrdinalIgnoreCase) ||
         content.Contains("UploadAsync", StringComparison.OrdinalIgnoreCase) ||
         (content.Contains("Task.Run", StringComparison.OrdinalIgnoreCase) && 
          content.Contains("ContinueWith", StringComparison.OrdinalIgnoreCase)) ||
         (content.Contains("Telemetry", StringComparison.OrdinalIgnoreCase) && 
          content.Contains("Task.Run", StringComparison.OrdinalIgnoreCase)));

    /// <summary>
    /// Returns <c>true</c> if the content indicates bounded synchronization (semaphore/mutex/lock with bounds).
    /// Used by GCI0016 to suppress false positives on intentional synchronization for bounded resources.
    /// </summary>
    public static bool IsBoundedSynchronization(string content) =>
        !string.IsNullOrEmpty(content) &&
        DomainSpecificPatterns.CodePatterns.BoundedSynchronizationPatterns.Any(p => 
            content.Contains(p, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Returns <c>true</c> if the content indicates an instance-scoped cache (non-static Dictionary field).
    /// Used by GCI0020 to suppress false positives on intentional per-instance caches with bounded growth.
    /// </summary>
    public static bool IsInstanceScopedCache(string content) =>
        !string.IsNullOrEmpty(content) &&
        DomainSpecificPatterns.CodePatterns.InstanceScopedCachePatterns.Any(p => 
            content.Contains(p, StringComparison.OrdinalIgnoreCase));
}

#pragma warning restore GCI0003  // End of WellKnownPatterns consolidation module

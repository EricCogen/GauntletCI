// SPDX-License-Identifier: Elastic-2.0

using System.Text.RegularExpressions;
using GauntletCI.Core.Diff;

namespace GauntletCI.Core.Rules;

/// <summary>
/// Service providing pattern detection logic for all rule implementations.
/// Enables dependency injection and per-rule pattern overrides (future extension point).
/// 
/// This interface abstracts away the static WellKnownPatterns facade, allowing rules to:
/// - Inject pattern detection as a dependency
/// - Mock patterns in unit tests
/// - Override patterns per-rule or per-suite in future phases
/// </summary>
public interface IPatternProvider
{
    // ================= File Context Patterns =================

    /// <summary>Returns true when the given path belongs to a test or spec file.</summary>
    bool IsTestFile(string path);

    /// <summary>Returns true when the given path is an auto-generated file.</summary>
    bool IsGeneratedFile(string path);

    /// <summary>Returns true when the file path indicates infrastructure/configuration code.</summary>
    bool IsInfrastructureFile(string path);

    /// <summary>Returns true if the given path contains security-critical component names.</summary>
    bool IsSecurityCriticalPath(string path);

    /// <summary>Returns true if the line is a comment.</summary>
    bool IsCommentLine(string trimmed);

    /// <summary>File path components indicating security-critical code sections.</summary>
    IReadOnlyList<string> SecurityCriticalPaths { get; }

    // ================= Nullability and NRT Patterns =================

    /// <summary>Returns true when NRT (Nullable Reference Type) is enabled for the given file.</summary>
    bool IsNullableReferenceTypeEnabled(string fileContent);

    /// <summary>Returns true when the parameter section contains explicitly non-nullable parameters.</summary>
    bool HasNonNullableParams(string paramSection);

    /// <summary>Returns true when the parameter list contains nullable parameters.</summary>
    bool HasNullableReferenceParam(string paramSection);

    /// <summary>Returns true when the content contains Nullable&lt;T&gt; where T is a value type.</summary>
    bool IsNullableOfNonNullableType(string content);

    /// <summary>Returns true when the content contains a #pragma warning disable with nullable-related codes.</summary>
    bool IsPragmaNullableDisable(string content);

    /// <summary>Returns true when the content contains a LINQ expression where .Value is intentionally mapped.</summary>
    bool IsLinqValueProjection(string content);

    // ================= HTTP/gRPC External Service Patterns =================

    /// <summary>Returns true when the file path indicates gRPC-related code.</summary>
    bool IsGrpcRelatedFile(string path);

    /// <summary>Returns true when the added lines indicate HttpClient configuration via IHttpClientFactory.</summary>
    bool IsHttpFactoryConfigured(List<DiffLine> addedLines);

    /// <summary>Returns true when the added lines indicate gRPC channel configuration.</summary>
    bool UsesGrpcChannel(List<DiffLine> addedLines);

    /// <summary>Returns true when the added lines indicate use of factory-managed or injected HTTP clients.</summary>
    bool UsesFactoryManagedHttpClients(List<DiffLine> addedLines);

    /// <summary>Returns true when the content indicates an injected or static HttpClient is being used.</summary>
    bool IsInjectedOrStaticClient(string content);

    /// <summary>Returns true when the file uses .NET 9+ modern patterns.</summary>
    bool UsesModernDotNetPatterns(string fileContent);

    /// <summary>Returns true when a method signature uses record type parameters or other modern patterns.</summary>
    bool HasModernTypeParameters(string paramSection);

    // ================= Security Patterns =================

    /// <summary>Returns true if the value appears to be an environment variable name.</summary>
    bool IsEnvVarName(string literal);

    /// <summary>Returns true for benign literal values that are never actual secrets.</summary>
    bool IsBenignLiteralValue(string value);

    /// <summary>Returns the index of the first real assignment = in the line, skipping string literals.</summary>
    int FindAssignmentIndex(string content);

    /// <summary>Returns true only if the line contains a real assignment = (not ==, !=, <=, >=, =>).</summary>
    bool HasAssignment(string content);

    /// <summary>Returns the string value only when the direct RHS of an assignment is a bare string literal.</summary>
    string? ExtractDirectlyAssignedLiteral(string content);

    /// <summary>Commit message keywords indicating security-focused changes.</summary>
    IReadOnlyList<string> SecurityKeywords { get; }

    /// <summary>Test pattern keywords indicating security-focused test additions.</summary>
    IReadOnlyList<string> SecurityTestPatterns { get; }

    /// <summary>Returns true if the given text contains security-related keywords.</summary>
    bool HasSecurityKeywords(string text);

    /// <summary>Returns true if the given text contains security-related test patterns.</summary>
    bool HasSecurityTestPattern(string text);

    // ================= Domain-Specific Pattern Collections =================

    /// <summary>Variable and field name fragments used to detect hardcoded secrets by name.</summary>
    IReadOnlyList<string> SecretNamePatterns { get; }

    /// <summary>Log-level keywords indicating high-severity log calls.</summary>
    IReadOnlyList<string> HighSeverityLogKeywords { get; }

    /// <summary>Patterns indicating resource timeout limits in code.</summary>
    IReadOnlyList<string> TimeoutPatterns { get; }

    /// <summary>Patterns indicating iteration or loop count limits in code.</summary>
    IReadOnlyList<string> IterationLimitPatterns { get; }

    /// <summary>Patterns indicating resource limits (connections, threads, buffers, pools).</summary>
    IReadOnlyList<string> ResourceLimitPatterns { get; }

    /// <summary>Patterns indicating resource cleanup/disposal operations.</summary>
    IReadOnlyList<string> ResourceCleanupPatterns { get; }

    /// <summary>Patterns indicating asynchronous operations that can consume resources.</summary>
    IReadOnlyList<string> AsyncPatterns { get; }

    /// <summary>Test silence/skip patterns that prevent tests from running.</summary>
    IReadOnlyList<string> TestSilencePatterns { get; }

    /// <summary>Test attribute markers that identify test methods.</summary>
    IReadOnlyList<string> TestAttributeMarkers { get; }

    /// <summary>Assertion keywords used across popular .NET testing frameworks.</summary>
    IReadOnlyList<string> TestAssertionKeywords { get; }

    /// <summary>Array of service locator patterns that violate DI principles.</summary>
    IReadOnlyList<string> ServiceLocatorPatterns { get; }

    /// <summary>Regex to detect direct instantiation of injectable types.</summary>
    Regex DirectInstantiationRegex { get; }

    /// <summary>Patterns to exclude from direct instantiation checks.</summary>
    IReadOnlyList<string> DirectInstantiationExclusions { get; }

    /// <summary>Common connection string markers that indicate hardcoded database/service connections.</summary>
    IReadOnlyList<string> ConnectionStringMarkers { get; }

    // ================= Signature Compatibility =================

    /// <summary>Returns true when the added signature is a backward-compatible extension of the removed signature.</summary>
    bool IsBackwardCompatibleExtension(string removedSig, string addedSig);

    /// <summary>Extracts the parameter list content between the outermost parentheses of a method signature.</summary>
    string? ExtractParenContent(string sig);

    // ================= HTTP Context Helper =================

    /// <summary>Returns true if the given HTTP request content contains HTTP context signal patterns.</summary>
    bool HasHttpContextSignal(string content);

    // ================= Nested Pattern Groups (Complex Type Aggregates) =================

    /// <summary>Data integrity patterns (HTTP context, SQL injection, unchecked casts).</summary>
    IDataIntegrityPatterns DataIntegrityPatterns { get; }

    /// <summary>PII detection patterns (terms, log prefixes, transformations, reflection guards).</summary>
    IPiiDetectionPatterns PiiDetectionPatterns { get; }

    /// <summary>Idempotency patterns (signals, upsert operations).</summary>
    IIdempotencyPatterns IdempotencyPatterns { get; }

    /// <summary>Resource patterns (disposable types, owned/non-disposable registries, regex).</summary>
    IResourcePatterns ResourcePatterns { get; }

    /// <summary>External service patterns (HTTP methods, cancellation token checks).</summary>
    IExternalServicePatterns ExternalServicePatterns { get; }

    /// <summary>Performance patterns (LINQ methods, loop keywords).</summary>
    IPerformancePatterns PerformancePatterns { get; }

    /// <summary>Floating-point patterns (float literals, equality checks, integer zero guards).</summary>
    IFloatingPointPatterns FloatingPointPatterns { get; }

    /// <summary>Data schema patterns (serialization attributes).</summary>
    IDataSchemaPatterns DataSchemaPatterns { get; }

    /// <summary>Exception patterns (throw assertions, guard clause throws).</summary>
    IExceptionPatterns ExceptionPatterns { get; }

    /// <summary>Dependency injection patterns (service locators, direct instantiation, infrastructure files).</summary>
    IDependencyInjectionPatterns DependencyInjectionPatterns { get; }

    /// <summary>Stub detection patterns (keywords).</summary>
    IStubDetectionPatterns StubDetectionPatterns { get; }

    /// <summary>Architecture patterns (using statements).</summary>
    IArchitecturePatterns ArchitecturePatterns { get; }

    /// <summary>Guard patterns (null checks, value mappings, event handlers, cryptographic boundaries, etc.).</summary>
    IGuardPatterns GuardPatterns { get; }
}

// ================= Nested Interface Definitions =================

public interface IDataIntegrityPatterns
{
    IReadOnlyList<string> HttpContextSignals { get; }
    IReadOnlyList<string> SqlIgnorePatterns { get; }
    IReadOnlyList<string> UncheckedCastPatterns { get; }
    bool HasHttpContextSignal(string content);
}

public interface IPiiDetectionPatterns
{
    IReadOnlyList<string> PiiTerms { get; }
    IReadOnlyList<string> LogPrefixes { get; }
    IReadOnlyList<string> TransformationPatterns { get; }
    IReadOnlyList<string> ReflectionGuards { get; }
    bool IsDataTransformed(string content);
}

public interface IIdempotencyPatterns
{
    IReadOnlyList<string> IdempotencySignals { get; }
    IReadOnlyList<string> UpsertPatterns { get; }
}

public interface IResourcePatterns
{
    IReadOnlyList<string> DisposableTypes { get; }
    IReadOnlyList<string> DisposableSuffixes { get; }
    HashSet<string> OwnedByOtherRules { get; }
    HashSet<string> KnownNonDisposableTypes { get; }
    Regex NewTypeRegex { get; }
}

public interface IExternalServicePatterns
{
    IReadOnlyList<string> HttpCallMethods { get; }
    IReadOnlyList<string> CtCheckHttpMethods { get; }
}

public interface IPerformancePatterns
{
    IReadOnlyList<string> LinqMethods { get; }
    IReadOnlyList<string> LoopKeywords { get; }
    IReadOnlyList<string> UnboundedLoopKeywords { get; }
    bool HasLinqCall(string content);
    bool HasLoopConstruct(string content);
    bool IsRuleImplementationFile(string path);
}

public interface IFloatingPointPatterns
{
    Regex FloatLiteralOnRightRegex { get; }
    Regex FloatLiteralOnLeftRegex { get; }
    Regex FloatCastWithEqualityRegex { get; }
    Regex FloatTypeWithEqualityRegex { get; }
    Regex IntegerZeroGuardRegex { get; }
    bool IsGuardedIntegerZeroCheck(string content);
}

public interface IDataSchemaPatterns
{
    IReadOnlyList<string> SerializationAttributes { get; }
}

public interface IExceptionPatterns
{
    IReadOnlyList<string> ThrowAssertions { get; }
    IReadOnlyList<string> GuardClauseThrows { get; }
}

public interface IDependencyInjectionPatterns
{
    IReadOnlyList<string> ServiceLocatorPatterns { get; }
    IReadOnlyList<string> DirectInstantiationExclusions { get; }
    Regex DirectInstantiationRegex { get; }
    bool IsInfrastructureFile(string path);
}

public interface IStubDetectionPatterns
{
    IReadOnlyList<string> StubKeywords { get; }
}

public interface IArchitecturePatterns
{
    Regex UsingRegex { get; }
}

public interface IGuardPatterns
{
    bool HasValueNullCheck(string content);
    bool IsKeyValuePairAccess(string content);
    bool IsLinqValueMapping(string content);
    bool IsIOptionsValue(string content);
    bool IsExpressionBodied(string content);
    bool HasHasValueGuard(string content);
    bool IsCommentLine(string content);
    bool HasAccessModifier(string content);
    bool IsAsyncMethod(string content);
    bool IsEventHandler(string content);
    bool IsCryptographicBoundary(string content);
    bool HasInjectionGuard(string content);
    bool IsOverrideOrSealedMethod(string content);
    bool IsAbstractOrDelegateOrPartial(string content);
    bool IsMigrationOrSeedFile(string filePath);
    bool IsUiEventHandler(string filePath);
    bool IsDocumentationFile(string filePath);
    bool IsInsideStaticConstructor(List<DiffLine> allLines, int idx);
}

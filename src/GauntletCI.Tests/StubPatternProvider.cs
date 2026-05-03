// SPDX-License-Identifier: Elastic-2.0
using System.Text.RegularExpressions;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules;

namespace GauntletCI.Tests;

/// <summary>
/// Stub implementation of IPatternProvider for use in unit tests.
/// Returns empty/default values for all patterns and false for all checks.
/// Suitable for tests that don't depend on specific pattern matching.
/// </summary>
internal sealed class StubPatternProvider : IPatternProvider
{
    // Scalar property stubs
    public IReadOnlyList<string> SecurityCriticalPaths => [];
    public IReadOnlyList<string> SecurityKeywords => [];
    public IReadOnlyList<string> SecurityTestPatterns => [];
    public IReadOnlyList<string> SecretNamePatterns => [];
    public IReadOnlyList<string> HighSeverityLogKeywords => [];
    public IReadOnlyList<string> TimeoutPatterns => [];
    public IReadOnlyList<string> IterationLimitPatterns => [];
    public IReadOnlyList<string> ResourceLimitPatterns => [];
    public IReadOnlyList<string> ResourceCleanupPatterns => [];
    public IReadOnlyList<string> AsyncPatterns => [];
    public IReadOnlyList<string> TestSilencePatterns => [];
    public IReadOnlyList<string> TestAttributeMarkers => [];
    public IReadOnlyList<string> TestAssertionKeywords => [];
    public IReadOnlyList<string> ServiceLocatorPatterns => [];
    public Regex DirectInstantiationRegex => new("(?!)"); // Regex that never matches
    public IReadOnlyList<string> DirectInstantiationExclusions => [];
    public IReadOnlyList<string> ConnectionStringMarkers => [];

    // Nested pattern stubs
    public IDataIntegrityPatterns DataIntegrityPatterns => new StubDataIntegrityPatterns();
    public IPiiDetectionPatterns PiiDetectionPatterns => new StubPiiDetectionPatterns();
    public IIdempotencyPatterns IdempotencyPatterns => new StubIdempotencyPatterns();
    public IResourcePatterns ResourcePatterns => new StubResourcePatterns();
    public IExternalServicePatterns ExternalServicePatterns => new StubExternalServicePatterns();
    public IPerformancePatterns PerformancePatterns => new StubPerformancePatterns();
    public IFloatingPointPatterns FloatingPointPatterns => new StubFloatingPointPatterns();
    public IDataSchemaPatterns DataSchemaPatterns => new StubDataSchemaPatterns();
    public IExceptionPatterns ExceptionPatterns => new StubExceptionPatterns();
    public IDependencyInjectionPatterns DependencyInjectionPatterns => new StubDependencyInjectionPatterns();
    public IStubDetectionPatterns StubDetectionPatterns => new StubStubDetectionPatterns();
    public IArchitecturePatterns ArchitecturePatterns => new StubArchitecturePatterns();
    public IGuardPatterns GuardPatterns => new StubGuardPatterns();

    // Method stubs - return default/false
    public bool IsTestFile(string path) => false;
    public bool IsGeneratedFile(string path) => false;
    public bool IsInfrastructureFile(string path) => false;
    public bool IsSecurityCriticalPath(string path) => false;
    public bool IsCommentLine(string trimmed) => false;
    public bool IsNullableReferenceTypeEnabled(string fileContent) => false;
    public bool HasNonNullableParams(string paramSection) => false;
    public bool HasNullableReferenceParam(string paramSection) => false;
    public bool IsNullableOfNonNullableType(string content) => false;
    public bool IsPragmaNullableDisable(string content) => false;
    public bool IsLinqValueProjection(string content) => false;
    public bool IsGrpcRelatedFile(string path) => false;
    public bool IsHttpFactoryConfigured(List<DiffLine> addedLines) => false;
    public bool UsesGrpcChannel(List<DiffLine> addedLines) => false;
    public bool UsesFactoryManagedHttpClients(List<DiffLine> addedLines) => false;
    public bool IsInjectedOrStaticClient(string content) => false;
    public bool UsesModernDotNetPatterns(string fileContent) => false;
    public bool HasModernTypeParameters(string paramSection) => false;
    public bool IsEnvVarName(string literal) => false;
    public bool IsBenignLiteralValue(string value) => false;
    public int FindAssignmentIndex(string content) => -1;
    public bool HasAssignment(string content) => false;
    public string? ExtractDirectlyAssignedLiteral(string content) => null;
    public bool HasSecurityKeywords(string text) => false;
    public bool HasSecurityTestPattern(string text) => false;
    public bool IsBackwardCompatibleExtension(string removedSig, string addedSig) => false;
    public string? ExtractParenContent(string sig) => null;
    public bool HasHttpContextSignal(string content) => false;

    // Nested pattern stub implementations
    private sealed class StubDataIntegrityPatterns : IDataIntegrityPatterns
    {
        public IReadOnlyList<string> HttpContextSignals => [];
        public IReadOnlyList<string> SqlIgnorePatterns => [];
        public IReadOnlyList<string> UncheckedCastPatterns => [];
        public bool HasHttpContextSignal(string content) => false;
    }

    private sealed class StubPiiDetectionPatterns : IPiiDetectionPatterns
    {
        public IReadOnlyList<string> PiiTerms => [];
        public IReadOnlyList<string> LogPrefixes => [];
        public IReadOnlyList<string> TransformationPatterns => [];
        public IReadOnlyList<string> ReflectionGuards => [];
        public bool IsDataTransformed(string content) => false;
    }

    private sealed class StubIdempotencyPatterns : IIdempotencyPatterns
    {
        public IReadOnlyList<string> IdempotencySignals => [];
        public IReadOnlyList<string> UpsertPatterns => [];
    }

    private sealed class StubResourcePatterns : IResourcePatterns
    {
        public IReadOnlyList<string> DisposableTypes => [];
        public IReadOnlyList<string> DisposableSuffixes => [];
        public HashSet<string> OwnedByOtherRules => [];
        public HashSet<string> KnownNonDisposableTypes => [];
        public Regex NewTypeRegex => new("(?!)");
    }

    private sealed class StubExternalServicePatterns : IExternalServicePatterns
    {
        public IReadOnlyList<string> HttpCallMethods => [];
        public IReadOnlyList<string> CtCheckHttpMethods => [];
    }

    private sealed class StubPerformancePatterns : IPerformancePatterns
    {
        public IReadOnlyList<string> LinqMethods => [];
        public IReadOnlyList<string> LoopKeywords => [];
        public IReadOnlyList<string> UnboundedLoopKeywords => [];
        public bool HasLinqCall(string content) => false;
        public bool HasLoopConstruct(string content) => false;
        public bool IsRuleImplementationFile(string path) => false;
    }

    private sealed class StubFloatingPointPatterns : IFloatingPointPatterns
    {
        public Regex FloatLiteralOnRightRegex => new("(?!)");
        public Regex FloatLiteralOnLeftRegex => new("(?!)");
        public Regex FloatCastWithEqualityRegex => new("(?!)");
        public Regex FloatTypeWithEqualityRegex => new("(?!)");
        public Regex IntegerZeroGuardRegex => new("(?!)");
        public bool IsGuardedIntegerZeroCheck(string content) => false;
    }

    private sealed class StubDataSchemaPatterns : IDataSchemaPatterns
    {
        public IReadOnlyList<string> SerializationAttributes => [];
    }

    private sealed class StubExceptionPatterns : IExceptionPatterns
    {
        public IReadOnlyList<string> ThrowAssertions => [];
        public IReadOnlyList<string> GuardClauseThrows => [];
    }

    private sealed class StubDependencyInjectionPatterns : IDependencyInjectionPatterns
    {
        public IReadOnlyList<string> ServiceLocatorPatterns => [];
        public IReadOnlyList<string> DirectInstantiationExclusions => [];
        public Regex DirectInstantiationRegex => new("(?!)");
        public bool IsInfrastructureFile(string path) => false;
    }

    private sealed class StubStubDetectionPatterns : IStubDetectionPatterns
    {
        public IReadOnlyList<string> StubKeywords => [];
    }

    private sealed class StubArchitecturePatterns : IArchitecturePatterns
    {
        public Regex UsingRegex => new("(?!)");
    }

    private sealed class StubGuardPatterns : IGuardPatterns
    {
        public bool HasValueNullCheck(string content) => false;
        public bool IsKeyValuePairAccess(string content) => false;
        public bool IsLinqValueMapping(string content) => false;
        public bool IsIOptionsValue(string content) => false;
        public bool IsExpressionBodied(string content) => false;
        public bool HasHasValueGuard(string content) => false;
        public bool IsCommentLine(string content) => false;
        public bool HasAccessModifier(string content) => false;
        public bool IsAsyncMethod(string content) => false;
        public bool IsEventHandler(string content) => false;
        public bool IsCryptographicBoundary(string content) => false;
        public bool HasInjectionGuard(string content) => false;
        public bool IsOverrideOrSealedMethod(string content) => false;
        public bool IsAbstractOrDelegateOrPartial(string content) => false;
        public bool IsMigrationOrSeedFile(string filePath) => false;
        public bool IsUiEventHandler(string filePath) => false;
        public bool IsDocumentationFile(string filePath) => false;
        public bool IsInsideStaticConstructor(List<DiffLine> allLines, int idx) => false;
    }
}

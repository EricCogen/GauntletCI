// SPDX-License-Identifier: Elastic-2.0

using System.Text.RegularExpressions;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules.Patterns;

namespace GauntletCI.Core.Rules;

/// <summary>
/// Default implementation of IPatternProvider that delegates to WellKnownPatterns.
/// Provides backward-compatible access to the pattern registry for all rule implementations.
/// </summary>
public sealed class DefaultPatternProvider : IPatternProvider
{
    // Implement scalar properties by delegating to WellKnownPatterns static members
    public IReadOnlyList<string> SecurityCriticalPaths => WellKnownPatterns.SecurityCriticalPaths;
    public IReadOnlyList<string> SecurityKeywords => WellKnownPatterns.SecurityKeywords;
    public IReadOnlyList<string> SecurityTestPatterns => WellKnownPatterns.SecurityTestPatterns;
    public IReadOnlyList<string> SecretNamePatterns => WellKnownPatterns.SecretNamePatterns;
    public IReadOnlyList<string> HighSeverityLogKeywords => WellKnownPatterns.HighSeverityLogKeywords;
    public IReadOnlyList<string> TimeoutPatterns => WellKnownPatterns.TimeoutPatterns;
    public IReadOnlyList<string> IterationLimitPatterns => WellKnownPatterns.IterationLimitPatterns;
    public IReadOnlyList<string> ResourceLimitPatterns => WellKnownPatterns.ResourceLimitPatterns;
    public IReadOnlyList<string> ResourceCleanupPatterns => WellKnownPatterns.ResourceCleanupPatterns;
    public IReadOnlyList<string> AsyncPatterns => WellKnownPatterns.AsyncPatterns;
    public IReadOnlyList<string> TestSilencePatterns => WellKnownPatterns.TestSilencePatterns;
    public IReadOnlyList<string> TestAttributeMarkers => WellKnownPatterns.TestAttributeMarkers;
    public IReadOnlyList<string> TestAssertionKeywords => WellKnownPatterns.TestAssertionKeywords;
    public IReadOnlyList<string> ServiceLocatorPatterns => WellKnownPatterns.ServiceLocatorPatterns;
    public Regex DirectInstantiationRegex => WellKnownPatterns.DirectInstantiationRegex;
    public IReadOnlyList<string> DirectInstantiationExclusions => WellKnownPatterns.DirectInstantiationExclusions;
    public IReadOnlyList<string> ConnectionStringMarkers => WellKnownPatterns.ConnectionStringMarkers;

    // Implement methods by delegating to WellKnownPatterns static methods
    public bool IsTestFile(string path) => WellKnownPatterns.IsTestFile(path);
    public bool IsGeneratedFile(string path) => WellKnownPatterns.IsGeneratedFile(path);
    public bool IsInfrastructureFile(string path) => WellKnownPatterns.IsInfrastructureFile(path);
    public bool IsSecurityCriticalPath(string path) => WellKnownPatterns.IsSecurityCriticalPath(path);
    public bool IsCommentLine(string trimmed) => WellKnownPatterns.IsCommentLine(trimmed);
    public bool IsNullableReferenceTypeEnabled(string fileContent) => WellKnownPatterns.IsNullableReferenceTypeEnabled(fileContent);
    public bool HasNonNullableParams(string paramSection) => WellKnownPatterns.HasNonNullableParams(paramSection);
    public bool HasNullableReferenceParam(string paramSection) => WellKnownPatterns.HasNullableReferenceParam(paramSection);
    public bool IsNullableOfNonNullableType(string content) => WellKnownPatterns.IsNullableOfNonNullableType(content);
    public bool IsPragmaNullableDisable(string content) => WellKnownPatterns.IsPragmaNullableDisable(content);
    public bool IsLinqValueProjection(string content) => WellKnownPatterns.IsLinqValueProjection(content);
    public bool IsGrpcRelatedFile(string path) => WellKnownPatterns.IsGrpcRelatedFile(path);
    public bool IsHttpFactoryConfigured(List<DiffLine> addedLines) => WellKnownPatterns.IsHttpFactoryConfigured(addedLines);
    public bool UsesGrpcChannel(List<DiffLine> addedLines) => WellKnownPatterns.UsesGrpcChannel(addedLines);
    public bool UsesFactoryManagedHttpClients(List<DiffLine> addedLines) => WellKnownPatterns.UsesFactoryManagedHttpClients(addedLines);
    public bool IsInjectedOrStaticClient(string content) => WellKnownPatterns.IsInjectedOrStaticClient(content);
    public bool UsesModernDotNetPatterns(string fileContent) => WellKnownPatterns.UsesModernDotNetPatterns(fileContent);
    public bool HasModernTypeParameters(string paramSection) => WellKnownPatterns.HasModernTypeParameters(paramSection);
    public bool IsEnvVarName(string literal) => WellKnownPatterns.IsEnvVarName(literal);
    public bool IsBenignLiteralValue(string value) => WellKnownPatterns.IsBenignLiteralValue(value);
    public int FindAssignmentIndex(string content) => WellKnownPatterns.FindAssignmentIndex(content);
    public bool HasAssignment(string content) => WellKnownPatterns.HasAssignment(content);
    public string? ExtractDirectlyAssignedLiteral(string content) => WellKnownPatterns.ExtractDirectlyAssignedLiteral(content);
    public bool HasSecurityKeywords(string text) => WellKnownPatterns.HasSecurityKeywords(text);
    public bool HasSecurityTestPattern(string text) => WellKnownPatterns.HasSecurityTestPattern(text);
    public bool IsBackwardCompatibleExtension(string removedSig, string addedSig) => WellKnownPatterns.IsBackwardCompatibleExtension(removedSig, addedSig);
    public string? ExtractParenContent(string sig) => WellKnownPatterns.ExtractParenContent(sig);
    public bool HasHttpContextSignal(string content) => WellKnownPatterns.HasHttpContextSignal(content);

    // Delegate nested pattern groups
    public IDataIntegrityPatterns DataIntegrityPatterns => new DataIntegrityPatternsBridge();
    public IPiiDetectionPatterns PiiDetectionPatterns => new PiiDetectionPatternsBridge();
    public IIdempotencyPatterns IdempotencyPatterns => new IdempotencyPatternsBridge();
    public IResourcePatterns ResourcePatterns => new ResourcePatternsBridge();
    public IExternalServicePatterns ExternalServicePatterns => new ExternalServicePatternsBridge();
    public IPerformancePatterns PerformancePatterns => new PerformancePatternsBridge();
    public IFloatingPointPatterns FloatingPointPatterns => new FloatingPointPatternsBridge();
    public IDataSchemaPatterns DataSchemaPatterns => new DataSchemaPatternsBridge();
    public IExceptionPatterns ExceptionPatterns => new ExceptionPatternsBridge();
    public IDependencyInjectionPatterns DependencyInjectionPatterns => new DependencyInjectionPatternsBridge();
    public IStubDetectionPatterns StubDetectionPatterns => new StubDetectionPatternsBridge();
    public IArchitecturePatterns ArchitecturePatterns => new ArchitecturePatternsBridge();
    public IGuardPatterns GuardPatterns => new GuardPatternsBridge();

    // ============ Nested Bridge Classes for Pattern Groups ============

    private sealed class DataIntegrityPatternsBridge : IDataIntegrityPatterns
    {
        public IReadOnlyList<string> HttpContextSignals => WellKnownPatterns.DataIntegrityPatterns.HttpContextSignals;
        public IReadOnlyList<string> SqlIgnorePatterns => WellKnownPatterns.DataIntegrityPatterns.SqlIgnorePatterns;
        public IReadOnlyList<string> UncheckedCastPatterns => WellKnownPatterns.DataIntegrityPatterns.UncheckedCastPatterns;
        public bool HasHttpContextSignal(string content) => WellKnownPatterns.DataIntegrityPatterns.HasHttpContextSignal(content);
    }

    private sealed class PiiDetectionPatternsBridge : IPiiDetectionPatterns
    {
        public IReadOnlyList<string> PiiTerms => WellKnownPatterns.PiiDetectionPatterns.PiiTerms;
        public IReadOnlyList<string> LogPrefixes => WellKnownPatterns.PiiDetectionPatterns.LogPrefixes;
        public IReadOnlyList<string> TransformationPatterns => WellKnownPatterns.PiiDetectionPatterns.TransformationPatterns;
        public IReadOnlyList<string> ReflectionGuards => WellKnownPatterns.PiiDetectionPatterns.ReflectionGuards;
        public bool IsDataTransformed(string content) => WellKnownPatterns.PiiDetectionPatterns.IsDataTransformed(content);
    }

    private sealed class IdempotencyPatternsBridge : IIdempotencyPatterns
    {
        public IReadOnlyList<string> IdempotencySignals => WellKnownPatterns.IdempotencyPatterns.IdempotencySignals;
        public IReadOnlyList<string> UpsertPatterns => WellKnownPatterns.IdempotencyPatterns.UpsertPatterns;
    }

    private sealed class ResourcePatternsBridge : IResourcePatterns
    {
        public IReadOnlyList<string> DisposableTypes => WellKnownPatterns.ResourcePatterns.DisposableTypes;
        public IReadOnlyList<string> DisposableSuffixes => WellKnownPatterns.ResourcePatterns.DisposableSuffixes;
        public HashSet<string> OwnedByOtherRules => WellKnownPatterns.ResourcePatterns.OwnedByOtherRules;
        public HashSet<string> KnownNonDisposableTypes => WellKnownPatterns.ResourcePatterns.KnownNonDisposableTypes;
        public Regex NewTypeRegex => WellKnownPatterns.ResourcePatterns.NewTypeRegex;
    }

    private sealed class ExternalServicePatternsBridge : IExternalServicePatterns
    {
        public IReadOnlyList<string> HttpCallMethods => WellKnownPatterns.ExternalServicePatterns.HttpCallMethods;
        public IReadOnlyList<string> CtCheckHttpMethods => WellKnownPatterns.ExternalServicePatterns.CtCheckHttpMethods;
    }

    private sealed class PerformancePatternsBridge : IPerformancePatterns
    {
        public IReadOnlyList<string> LinqMethods => WellKnownPatterns.PerformancePatterns.LinqMethods;
        public IReadOnlyList<string> LoopKeywords => WellKnownPatterns.PerformancePatterns.LoopKeywords;
        public IReadOnlyList<string> UnboundedLoopKeywords => WellKnownPatterns.PerformancePatterns.UnboundedLoopKeywords;
        public bool HasLinqCall(string content) => WellKnownPatterns.PerformancePatterns.HasLinqCall(content);
        public bool HasLoopConstruct(string content) => WellKnownPatterns.PerformancePatterns.HasLoopConstruct(content);
        public bool IsRuleImplementationFile(string path) => WellKnownPatterns.PerformancePatterns.IsRuleImplementationFile(path);
    }

    private sealed class FloatingPointPatternsBridge : IFloatingPointPatterns
    {
        public Regex FloatLiteralOnRightRegex => WellKnownPatterns.FloatingPointPatterns.FloatLiteralOnRightRegex;
        public Regex FloatLiteralOnLeftRegex => WellKnownPatterns.FloatingPointPatterns.FloatLiteralOnLeftRegex;
        public Regex FloatCastWithEqualityRegex => WellKnownPatterns.FloatingPointPatterns.FloatCastWithEqualityRegex;
        public Regex FloatTypeWithEqualityRegex => WellKnownPatterns.FloatingPointPatterns.FloatTypeWithEqualityRegex;
        public Regex IntegerZeroGuardRegex => WellKnownPatterns.FloatingPointPatterns.IntegerZeroGuardRegex;
        public bool IsGuardedIntegerZeroCheck(string content) => WellKnownPatterns.FloatingPointPatterns.IsGuardedIntegerZeroCheck(content);
    }

    private sealed class DataSchemaPatternsBridge : IDataSchemaPatterns
    {
        public IReadOnlyList<string> SerializationAttributes => WellKnownPatterns.DataSchemaPatterns.SerializationAttributes;
    }

    private sealed class ExceptionPatternsBridge : IExceptionPatterns
    {
        public IReadOnlyList<string> ThrowAssertions => WellKnownPatterns.ExceptionPatterns.ThrowAssertions;
        public IReadOnlyList<string> GuardClauseThrows => WellKnownPatterns.ExceptionPatterns.GuardClauseThrows;
    }

    private sealed class DependencyInjectionPatternsBridge : IDependencyInjectionPatterns
    {
        public IReadOnlyList<string> ServiceLocatorPatterns => WellKnownPatterns.DependencyInjectionPatterns.ServiceLocatorPatterns;
        public IReadOnlyList<string> DirectInstantiationExclusions => WellKnownPatterns.DependencyInjectionPatterns.DirectInstantiationExclusions;
        public Regex DirectInstantiationRegex => WellKnownPatterns.DependencyInjectionPatterns.DirectInstantiationRegex;
        public bool IsInfrastructureFile(string path) => WellKnownPatterns.DependencyInjectionPatterns.IsInfrastructureFile(path);
    }

    private sealed class StubDetectionPatternsBridge : IStubDetectionPatterns
    {
        public IReadOnlyList<string> StubKeywords => WellKnownPatterns.StubDetectionPatterns.StubKeywords;
    }

    private sealed class ArchitecturePatternsBridge : IArchitecturePatterns
    {
        public Regex UsingRegex => WellKnownPatterns.ArchitecturePatterns.UsingRegex;
    }

    private sealed class GuardPatternsBridge : IGuardPatterns
    {
        public bool HasValueNullCheck(string content) => WellKnownPatterns.GuardPatterns.HasValueNullCheck(content);
        public bool IsKeyValuePairAccess(string content) => WellKnownPatterns.GuardPatterns.IsKeyValuePairAccess(content);
        public bool IsLinqValueMapping(string content) => WellKnownPatterns.GuardPatterns.IsLinqValueMapping(content);
        public bool IsIOptionsValue(string content) => WellKnownPatterns.GuardPatterns.IsIOptionsValue(content);
        public bool IsExpressionBodied(string content) => WellKnownPatterns.GuardPatterns.IsExpressionBodied(content);
        public bool HasHasValueGuard(string content) => WellKnownPatterns.GuardPatterns.HasHasValueGuard(content);
        public bool IsCommentLine(string content) => WellKnownPatterns.GuardPatterns.IsCommentLine(content);
        public bool HasAccessModifier(string content) => WellKnownPatterns.GuardPatterns.HasAccessModifier(content);
        public bool IsAsyncMethod(string content) => WellKnownPatterns.GuardPatterns.IsAsyncMethod(content);
        public bool IsEventHandler(string content) => WellKnownPatterns.GuardPatterns.IsEventHandler(content);
        public bool IsCryptographicBoundary(string content) => WellKnownPatterns.GuardPatterns.IsCryptographicBoundary(content);
        public bool HasInjectionGuard(string content) => WellKnownPatterns.GuardPatterns.HasInjectionGuard(content);
        public bool IsOverrideOrSealedMethod(string content) => WellKnownPatterns.GuardPatterns.IsOverrideOrSealedMethod(content);
        public bool IsAbstractOrDelegateOrPartial(string content) => WellKnownPatterns.GuardPatterns.IsAbstractOrDelegateOrPartial(content);
        public bool IsMigrationOrSeedFile(string filePath) => WellKnownPatterns.GuardPatterns.IsMigrationOrSeedFile(filePath);
        public bool IsUiEventHandler(string filePath) => WellKnownPatterns.GuardPatterns.IsUiEventHandler(filePath);
        public bool IsDocumentationFile(string filePath) => WellKnownPatterns.GuardPatterns.IsDocumentationFile(filePath);
        public bool IsInsideStaticConstructor(List<DiffLine> allLines, int idx) => WellKnownPatterns.GuardPatterns.IsInsideStaticConstructor(allLines, idx);
    }
}

// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Core.Semantics;

/// <summary>
/// Classifies the semantic category of a patch transformation.
/// Transformations represent higher-level semantic patterns: refactorings, rewrites, structural changes.
/// </summary>
public enum PatchTransformationKind
{
    // Refactoring patterns
    /// <summary>Extract method/function refactoring: code extracted into new function.</summary>
    ExtractMethod,

    /// <summary>Inline method/function refactoring: function inlined into call sites.</summary>
    InlineMethod,

    /// <summary>Move method: function moved to different class/module.</summary>
    MoveMethod,

    /// <summary>Rename method/function refactoring.</summary>
    RenameMethod,

    /// <summary>Extract variable/field: repeated expression extracted into named variable.</summary>
    ExtractVariable,

    /// <summary>Inline variable: variable inlined into uses.</summary>
    InlineVariable,

    /// <summary>Extract class: code extracted into new class.</summary>
    ExtractClass,

    /// <summary>Extract interface: interface extracted from class implementation.</summary>
    ExtractInterface,

    /// <summary>Move class: class moved to different namespace/module.</summary>
    MoveClass,

    /// <summary>Rename class: class name changed.</summary>
    RenameClass,

    // Structural changes
    /// <summary>Class inheritance changed (e.g., extends X → extends Y).</summary>
    InheritanceChanged,

    /// <summary>Interface implementation added.</summary>
    InterfaceImplementationAdded,

    /// <summary>Interface implementation removed.</summary>
    InterfaceImplementationRemoved,

    /// <summary>Visibility/access modifier changed (private → public, etc.).</summary>
    AccessModifierChanged,

    // Dependency changes
    /// <summary>New import or using statement added.</summary>
    ImportAdded,

    /// <summary>Import or using statement removed.</summary>
    ImportRemoved,

    /// <summary>Namespace or module structure changed.</summary>
    NamespaceModified,

    // Logic/flow changes
    /// <summary>Conditional logic simplified or refactored.</summary>
    LogicSimplified,

    /// <summary>Conditional logic made more complex.</summary>
    LogicComplexified,

    /// <summary>Loop structure changed (while ↔ for, added/removed iteration).</summary>
    LoopModified,

    /// <summary>Exception handling changed.</summary>
    ExceptionHandlingChanged,

    // Data structure changes
    /// <summary>Data structure changed (array → list, object → tuple, etc.).</summary>
    DataStructureChanged,

    /// <summary>Type hierarchy modified (generics, type parameters changed).</summary>
    TypeHierarchyChanged,

    // Performance-related
    /// <summary>Code optimized for performance (e.g., caching added).</summary>
    PerformanceOptimization,

    /// <summary>Code de-optimized or trade-off made (e.g., clarity over speed).</summary>
    PerformanceRegression,

    // Testing/quality
    /// <summary>Test coverage added or improved.</summary>
    TestCoverageAdded,

    /// <summary>Test coverage removed or reduced.</summary>
    TestCoverageRemoved,

    /// <summary>Assertion or validation added.</summary>
    ValidationAdded,

    /// <summary>Assertion or validation removed.</summary>
    ValidationRemoved,

    // Documentation
    /// <summary>Documentation or comments added.</summary>
    DocumentationAdded,

    /// <summary>Documentation or comments removed.</summary>
    DocumentationRemoved,

    /// <summary>Documentation or comments updated.</summary>
    DocumentationUpdated,

    // Configuration
    /// <summary>Configuration or constant changed.</summary>
    ConfigurationChanged,

    /// <summary>Feature flag or conditional compilation added/modified.</summary>
    FeatureFlagChanged,

    // Build/dependency system
    /// <summary>Dependency version updated.</summary>
    DependencyVersionChanged,

    /// <summary>Dependency added.</summary>
    DependencyAdded,

    /// <summary>Dependency removed.</summary>
    DependencyRemoved,

    // Other/unknown
    /// <summary>A transformation that doesn't fit other categories.</summary>
    Other,

    /// <summary>A transformation whose kind could not be determined.</summary>
    Unknown
}

/// <summary>
/// Represents a high-level semantic transformation: a refactoring, structural change, or pattern.
/// Transformations are built from operations and represent intent.
/// </summary>
public sealed class PatchTransformation
{
    /// <summary>The semantic kind of this transformation.</summary>
    public PatchTransformationKind Kind { get; init; }

    /// <summary>A human-readable name or title for this transformation.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>A detailed description of the transformation and its intent.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>The file paths affected by this transformation.</summary>
    public IReadOnlyList<string> AffectedFiles { get; init; } = [];

    /// <summary>The operations that compose this transformation.</summary>
    public IReadOnlyList<PatchOperation> Operations { get; init; } = [];

    /// <summary>Optional symbol (class name, method name, etc.) this transformation targets.</summary>
    public string? TargetSymbol { get; init; }

    /// <summary>Optional source symbol (for rename/move operations).</summary>
    public string? SourceSymbol { get; init; }

    /// <summary>The line range (old file) where this transformation starts and ends.</summary>
    public (int Start, int End)? OldLineRange { get; init; }

    /// <summary>The line range (new file) where this transformation starts and ends.</summary>
    public (int Start, int End)? NewLineRange { get; init; }

    /// <summary>
    /// Risk level (0.0-1.0) indicating the potential severity of this transformation.
    /// Calculated from constituent operations.
    /// </summary>
    public double RiskLevel { get; init; }

    /// <summary>
    /// Confidence score (0.0-1.0) indicating analyzer certainty about this transformation.
    /// Calculated from constituent operations.
    /// </summary>
    public double Confidence { get; init; } = 1.0;

    /// <summary>
    /// Behavioral impact classification: None, Logic, Control, Performance, Io, Security, Compatibility.
    /// Used for risk prioritization.
    /// </summary>
    public string BehavioralImpact { get; init; } = "None";

    /// <summary>
    /// Whether this transformation is a known-safe pattern (e.g., idiomatic refactoring).
    /// Safe transformations may be prioritized lower in review.
    /// </summary>
    public bool IsSafePattern { get; init; }
}

/// <summary>
/// Collects and indexes patch transformations for efficient querying and analysis.
/// Provides views into transformations by kind, risk level, affected files, and target symbols.
/// </summary>
public sealed class PatchTransformationCollection
{
    private readonly List<PatchTransformation> _transformations = [];

    /// <summary>All transformations in this collection.</summary>
    public IReadOnlyList<PatchTransformation> All => _transformations.AsReadOnly();

    /// <summary>Adds a transformation to the collection.</summary>
    public void Add(PatchTransformation transformation)
    {
        ArgumentNullException.ThrowIfNull(transformation, nameof(transformation));
        _transformations.Add(transformation);
    }

    /// <summary>Adds multiple transformations at once.</summary>
    public void AddRange(IEnumerable<PatchTransformation> transformations)
    {
        ArgumentNullException.ThrowIfNull(transformations, nameof(transformations));
        _transformations.AddRange(transformations);
    }

    /// <summary>Gets all transformations of a specific kind.</summary>
    public IEnumerable<PatchTransformation> ByKind(PatchTransformationKind kind)
    {
        return _transformations.Where(t => t.Kind == kind);
    }

    /// <summary>Gets all transformations affecting a specific file.</summary>
    public IEnumerable<PatchTransformation> ByFile(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath, nameof(filePath));
        return _transformations.Where(t => t.AffectedFiles.Contains(filePath));
    }

    /// <summary>Gets all transformations targeting a specific symbol.</summary>
    public IEnumerable<PatchTransformation> ByTargetSymbol(string symbol)
    {
        ArgumentNullException.ThrowIfNull(symbol, nameof(symbol));
        return _transformations.Where(t => t.TargetSymbol == symbol);
    }

    /// <summary>Gets all transformations with risk level >= threshold.</summary>
    public IEnumerable<PatchTransformation> ByMinRisk(double threshold)
    {
        return _transformations.Where(t => t.RiskLevel >= threshold);
    }

    /// <summary>Gets all transformations with confidence >= threshold.</summary>
    public IEnumerable<PatchTransformation> ByMinConfidence(double threshold)
    {
        return _transformations.Where(t => t.Confidence >= threshold);
    }

    /// <summary>Gets all transformations with specific behavioral impact.</summary>
    public IEnumerable<PatchTransformation> ByBehavioralImpact(string impact)
    {
        ArgumentNullException.ThrowIfNull(impact, nameof(impact));
        return _transformations.Where(t => t.BehavioralImpact == impact);
    }

    /// <summary>Gets all transformations marked as safe patterns.</summary>
    public IEnumerable<PatchTransformation> SafePatterns()
    {
        return _transformations.Where(t => t.IsSafePattern);
    }

    /// <summary>Gets all risky transformations (not safe patterns).</summary>
    public IEnumerable<PatchTransformation> RiskyTransformations()
    {
        return _transformations.Where(t => !t.IsSafePattern);
    }

    /// <summary>Counts transformations by kind.</summary>
    public Dictionary<PatchTransformationKind, int> CountByKind()
    {
        return _transformations
            .GroupBy(t => t.Kind)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    /// <summary>Counts transformations by behavioral impact.</summary>
    public Dictionary<string, int> CountByBehavioralImpact()
    {
        return _transformations
            .GroupBy(t => t.BehavioralImpact)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    /// <summary>Calculates aggregate risk across all transformations.</summary>
    public double AggregateRisk(string metric = "max")
    {
        return metric switch
        {
            "max" => _transformations.MaxBy(t => t.RiskLevel)?.RiskLevel ?? 0,
            "mean" => _transformations.Count > 0 ? _transformations.Average(t => t.RiskLevel) : 0,
            "p95" => Percentile(_transformations.Select(t => t.RiskLevel), 0.95),
            _ => 0
        };
    }

    /// <summary>Gets the total count of transformations.</summary>
    public int Count => _transformations.Count;

    /// <summary>Clears all transformations.</summary>
    public void Clear()
    {
        _transformations.Clear();
    }

    private static double Percentile(IEnumerable<double> values, double percentile)
    {
        var sorted = values.OrderBy(v => v).ToList();
        if (sorted.Count == 0)
            return 0;

        var index = (int)Math.Ceiling(percentile * sorted.Count) - 1;
        return sorted[Math.Max(0, Math.Min(index, sorted.Count - 1))];
    }
}

/// <summary>
/// Helper factory for creating common PatchTransformation instances.
/// Reduces boilerplate when building transformations programmatically.
/// </summary>
public static class PatchTransformationFactory
{
    /// <summary>Creates an extract-method refactoring transformation.</summary>
    public static PatchTransformation ExtractMethod(
        string methodName,
        string? sourceFile,
        IEnumerable<PatchOperation> operations,
        double risk = 0.4)
    {
        return new PatchTransformation
        {
            Kind = PatchTransformationKind.ExtractMethod,
            Name = $"Extract method: {methodName}",
            Description = $"Code extracted into new method '{methodName}'",
            TargetSymbol = methodName,
            AffectedFiles = sourceFile != null ? [sourceFile] : [],
            Operations = operations.ToList(),
            RiskLevel = risk,
            IsSafePattern = true,
            BehavioralImpact = "None"
        };
    }

    /// <summary>Creates a rename refactoring transformation.</summary>
    public static PatchTransformation Rename(
        PatchTransformationKind kind,
        string oldName,
        string newName,
        string? sourceFile,
        IEnumerable<PatchOperation> operations,
        double risk = 0.3)
    {
        return new PatchTransformation
        {
            Kind = kind,
            Name = $"Rename: {oldName} → {newName}",
            Description = $"Renamed '{oldName}' to '{newName}'",
            SourceSymbol = oldName,
            TargetSymbol = newName,
            AffectedFiles = sourceFile != null ? [sourceFile] : [],
            Operations = operations.ToList(),
            RiskLevel = risk,
            IsSafePattern = true,
            BehavioralImpact = "None"
        };
    }

    /// <summary>Creates a logic-change transformation.</summary>
    public static PatchTransformation LogicChanged(
        string description,
        IEnumerable<string> affectedFiles,
        IEnumerable<PatchOperation> operations,
        double risk = 0.8)
    {
        return new PatchTransformation
        {
            Kind = PatchTransformationKind.LogicSimplified,
            Name = "Logic changed",
            Description = description,
            AffectedFiles = affectedFiles.ToList(),
            Operations = operations.ToList(),
            RiskLevel = risk,
            IsSafePattern = false,
            BehavioralImpact = "Logic"
        };
    }

    /// <summary>Creates an access-modifier-changed transformation.</summary>
    public static PatchTransformation AccessModifierChanged(
        string symbolName,
        string oldModifier,
        string newModifier,
        string? sourceFile,
        IEnumerable<PatchOperation> operations,
        double risk = 0.6)
    {
        return new PatchTransformation
        {
            Kind = PatchTransformationKind.AccessModifierChanged,
            Name = $"Access modifier changed: {symbolName}",
            Description = $"'{symbolName}' visibility changed from '{oldModifier}' to '{newModifier}'",
            TargetSymbol = symbolName,
            AffectedFiles = sourceFile != null ? [sourceFile] : [],
            Operations = operations.ToList(),
            RiskLevel = risk,
            IsSafePattern = false,
            BehavioralImpact = "Compatibility"
        };
    }

    /// <summary>Creates a dependency-changed transformation.</summary>
    public static PatchTransformation DependencyChanged(
        string packageName,
        string? oldVersion,
        string? newVersion,
        IEnumerable<string> affectedFiles,
        IEnumerable<PatchOperation> operations,
        double risk = 0.7)
    {
        return new PatchTransformation
        {
            Kind = PatchTransformationKind.DependencyVersionChanged,
            Name = $"Dependency: {packageName} {oldVersion ?? "?"}→{newVersion ?? "?"}",
            Description = $"Updated {packageName} from {oldVersion} to {newVersion}",
            TargetSymbol = packageName,
            AffectedFiles = affectedFiles.ToList(),
            Operations = operations.ToList(),
            RiskLevel = risk,
            IsSafePattern = false,
            BehavioralImpact = "Compatibility"
        };
    }

    /// <summary>Creates a performance-optimization transformation.</summary>
    public static PatchTransformation PerformanceOptimization(
        string description,
        string? targetSymbol,
        IEnumerable<string> affectedFiles,
        IEnumerable<PatchOperation> operations,
        double risk = 0.4)
    {
        return new PatchTransformation
        {
            Kind = PatchTransformationKind.PerformanceOptimization,
            Name = "Performance optimization",
            Description = description,
            TargetSymbol = targetSymbol,
            AffectedFiles = affectedFiles.ToList(),
            Operations = operations.ToList(),
            RiskLevel = risk,
            IsSafePattern = true,
            BehavioralImpact = "Performance"
        };
    }
}

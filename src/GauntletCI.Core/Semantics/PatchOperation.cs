// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Core.Semantics;

/// <summary>
/// Classifies the semantic kind of a patch operation.
/// Operations represent low-level changes within added/removed code: syntax transformations, replacements, etc.
/// </summary>
public enum PatchOperationKind
{
    // Core line-level operations
    /// <summary>A line was completely added (inserted).</summary>
    LineAdded,

    /// <summary>A line was completely removed (deleted).</summary>
    LineRemoved,

    // Symbol/identifier operations
    /// <summary>An identifier or variable name was added.</summary>
    IdentifierAdded,

    /// <summary>An identifier or variable name was removed.</summary>
    IdentifierRemoved,

    /// <summary>An identifier or variable name was renamed.</summary>
    IdentifierRenamed,

    // Control flow operations
    /// <summary>A conditional (if, while, for, switch, etc.) was added.</summary>
    ConditionalAdded,

    /// <summary>A conditional was removed or simplified.</summary>
    ConditionalRemoved,

    /// <summary>A conditional was modified (condition changed).</summary>
    ConditionalModified,

    // Function/method operations
    /// <summary>A function or method definition was added.</summary>
    FunctionAdded,

    /// <summary>A function or method definition was removed.</summary>
    FunctionRemoved,

    /// <summary>A function signature was modified (parameters, return type, etc.).</summary>
    FunctionSignatureChanged,

    /// <summary>A function body was modified.</summary>
    FunctionBodyModified,

    // Parameter/argument operations
    /// <summary>A parameter was added to a function signature.</summary>
    ParameterAdded,

    /// <summary>A parameter was removed from a function signature.</summary>
    ParameterRemoved,

    /// <summary>A parameter's type or default value was changed.</summary>
    ParameterModified,

    /// <summary>An argument was added to a function call.</summary>
    ArgumentAdded,

    /// <summary>An argument was removed from a function call.</summary>
    ArgumentRemoved,

    // Type/declaration operations
    /// <summary>A type annotation or cast was added.</summary>
    TypeAnnotationAdded,

    /// <summary>A type annotation or cast was removed.</summary>
    TypeAnnotationRemoved,

    /// <summary>A type was changed (T1 → T2).</summary>
    TypeChanged,

    /// <summary>A class, interface, or struct was added.</summary>
    TypeDeclarationAdded,

    /// <summary>A class, interface, or struct was removed.</summary>
    TypeDeclarationRemoved,

    /// <summary>A class, interface, or struct was modified (e.g., inherits from new base).</summary>
    TypeDeclarationModified,

    // Operator/expression operations
    /// <summary>An operator was added (e.g., !, &&, ||, +, -, *, /).</summary>
    OperatorAdded,

    /// <summary>An operator was removed.</summary>
    OperatorRemoved,

    /// <summary>An operator was changed (+ → -, && → ||, etc.).</summary>
    OperatorChanged,

    /// <summary>A literal value was added (number, string, boolean, etc.).</summary>
    LiteralAdded,

    /// <summary>A literal value was removed.</summary>
    LiteralRemoved,

    /// <summary>A literal value was changed (1 → 2, "a" → "b", true → false, etc.).</summary>
    LiteralChanged,

    // Assignment and binding operations
    /// <summary>A variable assignment or binding was added.</summary>
    AssignmentAdded,

    /// <summary>A variable assignment or binding was removed.</summary>
    AssignmentRemoved,

    /// <summary>An assignment's right-hand side (value) was modified.</summary>
    AssignmentValueChanged,

    // Collection operations
    /// <summary>An array, list, or set literal was added.</summary>
    CollectionAdded,

    /// <summary>An array, list, or set literal was removed.</summary>
    CollectionRemoved,

    /// <summary>A collection element was added (e.g., new item in array literal).</summary>
    CollectionElementAdded,

    /// <summary>A collection element was removed.</summary>
    CollectionElementRemoved,

    // Error handling operations
    /// <summary>A try-catch or exception handler was added.</summary>
    ExceptionHandlerAdded,

    /// <summary>A try-catch or exception handler was removed.</summary>
    ExceptionHandlerRemoved,

    /// <summary>An exception type was changed (catch (X) → catch (Y)).</summary>
    ExceptionTypeChanged,

    /// <summary>A throw statement was added.</summary>
    ThrowAdded,

    /// <summary>A throw statement was removed.</summary>
    ThrowRemoved,

    // Whitespace and formatting (usually ignored by semantic analysis)
    /// <summary>Whitespace or formatting changed (usually semantically insignificant).</summary>
    FormattingChanged,

    // Other/unknown operations
    /// <summary>An operation that doesn't fit other categories.</summary>
    Other,

    /// <summary>An operation whose kind could not be determined.</summary>
    Unknown
}

/// <summary>
/// Represents a single semantic operation: a low-level, typed change within added or removed code.
/// Examples: variable renamed, operator changed, parameter added, function signature modified.
/// </summary>
public sealed class PatchOperation
{
    /// <summary>The semantic kind of this operation.</summary>
    public PatchOperationKind Kind { get; init; }

    /// <summary>The line where this operation occurs (old file line number, if applicable).</summary>
    public int? OldLineNumber { get; init; }

    /// <summary>The line where this operation occurs (new file line number, if applicable).</summary>
    public int? NewLineNumber { get; init; }

    /// <summary>The file path where this operation occurs.</summary>
    public string? FilePath { get; init; }

    /// <summary>
    /// A free-form description of the operation for human readability.
    /// Example: "Renamed variable 'count' to 'total'", "Changed operator '+' to '-'".
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Optional text snippet that was removed or replaced (for changed/removed operations).
    /// </summary>
    public string? Before { get; init; }

    /// <summary>
    /// Optional text snippet that was added or replaced (for added/changed operations).
    /// </summary>
    public string? After { get; init; }

    /// <summary>
    /// Optional symbol or identifier involved in this operation.
    /// Useful for tracking specific symbols across operations.
    /// </summary>
    public string? Symbol { get; init; }

    /// <summary>
    /// Confidence score (0.0-1.0) indicating how certain the analyzer is about this operation.
    /// Lower scores indicate heuristic-based detection; higher scores indicate syntax-based certainty.
    /// </summary>
    public double Confidence { get; init; } = 1.0;

    /// <summary>
    /// Risk level (0.0-1.0) indicating the potential severity of this change.
    /// Calculated during analysis; used for prioritizing review.
    /// 0.0 = negligible risk (formatting, whitespace)
    /// 1.0 = critical risk (logic change, control flow modification)
    /// </summary>
    public double RiskLevel { get; init; }

    /// <summary>
    /// Optional category or tag for grouping related operations.
    /// Examples: "logic", "io", "performance", "security".
    /// </summary>
    public string? Category { get; init; }
}

/// <summary>
/// Collects and indexes patch operations for efficient querying and analysis.
/// Provides views into operations by kind, risk level, file, and symbol.
/// </summary>
public sealed class PatchOperationCollection
{
    private readonly List<PatchOperation> _operations = [];

    /// <summary>All operations in this collection.</summary>
    public IReadOnlyList<PatchOperation> All => _operations.AsReadOnly();

    /// <summary>Adds an operation to the collection.</summary>
    public void Add(PatchOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation, nameof(operation));
        _operations.Add(operation);
    }

    /// <summary>Adds multiple operations at once.</summary>
    public void AddRange(IEnumerable<PatchOperation> operations)
    {
        ArgumentNullException.ThrowIfNull(operations, nameof(operations));
        _operations.AddRange(operations);
    }

    /// <summary>Gets all operations of a specific kind.</summary>
    public IEnumerable<PatchOperation> ByKind(PatchOperationKind kind)
    {
        return _operations.Where(op => op.Kind == kind);
    }

    /// <summary>Gets all operations in a specific file.</summary>
    public IEnumerable<PatchOperation> ByFile(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath, nameof(filePath));
        return _operations.Where(op => op.FilePath == filePath);
    }

    /// <summary>Gets all operations involving a specific symbol.</summary>
    public IEnumerable<PatchOperation> BySymbol(string symbol)
    {
        ArgumentNullException.ThrowIfNull(symbol, nameof(symbol));
        return _operations.Where(op => op.Symbol == symbol);
    }

    /// <summary>Gets all operations in a specific category.</summary>
    public IEnumerable<PatchOperation> ByCategory(string category)
    {
        ArgumentNullException.ThrowIfNull(category, nameof(category));
        return _operations.Where(op => op.Category == category);
    }

    /// <summary>Gets all operations with risk level >= threshold.</summary>
    public IEnumerable<PatchOperation> ByMinRisk(double threshold)
    {
        return _operations.Where(op => op.RiskLevel >= threshold);
    }

    /// <summary>Gets all operations with confidence >= threshold.</summary>
    public IEnumerable<PatchOperation> ByMinConfidence(double threshold)
    {
        return _operations.Where(op => op.Confidence >= threshold);
    }

    /// <summary>Counts operations by kind.</summary>
    public Dictionary<PatchOperationKind, int> CountByKind()
    {
        return _operations
            .GroupBy(op => op.Kind)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    /// <summary>Calculates aggregate risk across all operations (max, mean, or percentile).</summary>
    public double AggregateRisk(string metric = "max")
    {
        return metric switch
        {
            "max" => _operations.MaxBy(op => op.RiskLevel)?.RiskLevel ?? 0,
            "mean" => _operations.Count > 0 ? _operations.Average(op => op.RiskLevel) : 0,
            "p95" => Percentile(_operations.Select(op => op.RiskLevel), 0.95),
            _ => 0
        };
    }

    /// <summary>Gets the total count of operations.</summary>
    public int Count => _operations.Count;

    /// <summary>Clears all operations.</summary>
    public void Clear()
    {
        _operations.Clear();
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
/// Helper factory for creating common PatchOperation instances.
/// Reduces boilerplate when building operations programmatically.
/// </summary>
public static class PatchOperationFactory
{
    /// <summary>Creates a line-added operation.</summary>
    public static PatchOperation LineAdded(int newLineNumber, string? filePath, string content, double risk = 0.3)
    {
        return new PatchOperation
        {
            Kind = PatchOperationKind.LineAdded,
            NewLineNumber = newLineNumber,
            FilePath = filePath,
            After = content,
            Description = $"Line added at {newLineNumber}: {Truncate(content)}",
            RiskLevel = risk
        };
    }

    /// <summary>Creates a line-removed operation.</summary>
    public static PatchOperation LineRemoved(int oldLineNumber, string? filePath, string content, double risk = 0.3)
    {
        return new PatchOperation
        {
            Kind = PatchOperationKind.LineRemoved,
            OldLineNumber = oldLineNumber,
            FilePath = filePath,
            Before = content,
            Description = $"Line removed at {oldLineNumber}: {Truncate(content)}",
            RiskLevel = risk
        };
    }

    /// <summary>Creates an identifier-renamed operation.</summary>
    public static PatchOperation IdentifierRenamed(string oldName, string newName, int? lineNumber, string? filePath, double risk = 0.6)
    {
        return new PatchOperation
        {
            Kind = PatchOperationKind.IdentifierRenamed,
            NewLineNumber = lineNumber,
            FilePath = filePath,
            Before = oldName,
            After = newName,
            Symbol = newName,
            Description = $"Identifier renamed: '{oldName}' → '{newName}'",
            RiskLevel = risk
        };
    }

    /// <summary>Creates an operator-changed operation.</summary>
    public static PatchOperation OperatorChanged(string oldOp, string newOp, int? lineNumber, string? filePath, double risk = 0.8)
    {
        return new PatchOperation
        {
            Kind = PatchOperationKind.OperatorChanged,
            NewLineNumber = lineNumber,
            FilePath = filePath,
            Before = oldOp,
            After = newOp,
            Symbol = newOp,
            Description = $"Operator changed: '{oldOp}' → '{newOp}'",
            RiskLevel = risk
        };
    }

    /// <summary>Creates a literal-changed operation.</summary>
    public static PatchOperation LiteralChanged(string oldValue, string newValue, int? lineNumber, string? filePath, double risk = 0.7)
    {
        return new PatchOperation
        {
            Kind = PatchOperationKind.LiteralChanged,
            NewLineNumber = lineNumber,
            FilePath = filePath,
            Before = oldValue,
            After = newValue,
            Description = $"Literal changed: {oldValue} → {newValue}",
            RiskLevel = risk
        };
    }

    /// <summary>Creates a function-signature-changed operation.</summary>
    public static PatchOperation FunctionSignatureChanged(string functionName, string oldSig, string newSig, int? lineNumber, string? filePath, double risk = 0.9)
    {
        return new PatchOperation
        {
            Kind = PatchOperationKind.FunctionSignatureChanged,
            NewLineNumber = lineNumber,
            FilePath = filePath,
            Before = oldSig,
            After = newSig,
            Symbol = functionName,
            Description = $"Function signature changed: {functionName}",
            RiskLevel = risk
        };
    }

    /// <summary>Creates a conditional-modified operation.</summary>
    public static PatchOperation ConditionalModified(string conditionDescription, int? lineNumber, string? filePath, double risk = 0.85)
    {
        return new PatchOperation
        {
            Kind = PatchOperationKind.ConditionalModified,
            NewLineNumber = lineNumber,
            FilePath = filePath,
            Description = $"Conditional modified: {conditionDescription}",
            RiskLevel = risk
        };
    }

    /// <summary>Creates a parameter-added operation.</summary>
    public static PatchOperation ParameterAdded(string parameterName, string? parameterType, int? lineNumber, string? filePath, double risk = 0.75)
    {
        return new PatchOperation
        {
            Kind = PatchOperationKind.ParameterAdded,
            NewLineNumber = lineNumber,
            FilePath = filePath,
            Symbol = parameterName,
            Description = $"Parameter added: {parameterName} ({parameterType ?? "unknown"})",
            RiskLevel = risk
        };
    }

    /// <summary>Creates a parameter-removed operation.</summary>
    public static PatchOperation ParameterRemoved(string parameterName, string? parameterType, int? lineNumber, string? filePath, double risk = 0.75)
    {
        return new PatchOperation
        {
            Kind = PatchOperationKind.ParameterRemoved,
            OldLineNumber = lineNumber,
            FilePath = filePath,
            Symbol = parameterName,
            Description = $"Parameter removed: {parameterName} ({parameterType ?? "unknown"})",
            RiskLevel = risk
        };
    }

    private static string Truncate(string text, int maxLength = 60)
    {
        return text.Length > maxLength ? text[..maxLength] + "..." : text;
    }
}

// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Core.Semantics;

/// <summary>
/// Classifies the type of evidence that supports a patch finding or transformation.
/// Evidence is the "proof" that a transformation or operation occurred.
/// </summary>
public enum PatchEvidenceKind
{
    // Direct line-level evidence
    /// <summary>An added line in the patch is evidence of insertion.</summary>
    AddedLine,

    /// <summary>A removed line in the patch is evidence of deletion.</summary>
    RemovedLine,

    /// <summary>Context lines surrounding a change provide scope evidence.</summary>
    ContextLine,

    // Syntax/semantic evidence
    /// <summary>An operator symbol (+, -, &&, ||, >, <, ==, etc.) changed or was added.</summary>
    OperatorChange,

    /// <summary>A keyword or reserved word (if, while, return, throw, etc.) changed or was added.</summary>
    KeywordChange,

    /// <summary>An identifier or variable name changed, was added, or removed.</summary>
    IdentifierChange,

    /// <summary>A literal value (number, string, boolean) changed or was added.</summary>
    LiteralChange,

    /// <summary>A type annotation or cast changed or was added.</summary>
    TypeAnnotationChange,

    // Structure evidence
    /// <summary>Bracket/brace structure ({}, [], (), <>) changed, indicating scope or collection changes.</summary>
    BracketStructureChange,

    /// <summary>Indentation changed, often indicating scope/nesting changes.</summary>
    IndentationChange,

    /// <summary>Line count or hunk size changed significantly.</summary>
    HunkSizeChange,

    // Control flow evidence
    /// <summary>Conditional structure (if/else, switch, ternary) was added or modified.</summary>
    ConditionalStructure,

    /// <summary>Loop structure (for, while, do-while) was added or modified.</summary>
    LoopStructure,

    /// <summary>Exception handling (try/catch/finally, throw) was added or modified.</summary>
    ExceptionHandling,

    /// <summary>Return statement or break/continue was added or modified.</summary>
    ControlFlowStatement,

    // Function/method evidence
    /// <summary>Function or method signature (parameters, return type) changed.</summary>
    FunctionSignature,

    /// <summary>Function call with different arguments was added or modified.</summary>
    FunctionCall,

    /// <summary>Function body content changed without signature change.</summary>
    FunctionBodyChange,

    // Assignment/binding evidence
    /// <summary>Variable assignment or binding changed.</summary>
    AssignmentChange,

    /// <summary>Parameter or argument list changed.</summary>
    ParameterChange,

    // Annotation evidence
    /// <summary>Code annotation, attribute, or decorator was added or changed.</summary>
    AnnotationChange,

    /// <summary>Comment or documentation was added, removed, or changed.</summary>
    CommentChange,

    // Import/dependency evidence
    /// <summary>Import or using statement was added or removed.</summary>
    ImportChange,

    /// <summary>Namespace or module reference changed.</summary>
    NamespaceChange,

    // Data structure evidence
    /// <summary>Collection literal (array, list, set, dict) changed or was added.</summary>
    CollectionLiteral,

    /// <summary>Object or record literal changed or was added.</summary>
    ObjectLiteral,

    // Pattern evidence
    /// <summary>Code pattern recognized (e.g., builder pattern, visitor pattern).</summary>
    PatternOccurrence,

    /// <summary>Known refactoring pattern detected (e.g., extract method, rename).</summary>
    RefactoringPattern,

    /// <summary>Security-relevant pattern detected (validation, sanitization, crypto).</summary>
    SecurityPattern,

    /// <summary>Performance-relevant pattern detected (caching, optimization, deoptimization).</summary>
    PerformancePattern,

    /// <summary>Testing pattern detected (test setup, assertion, mock).</summary>
    TestingPattern,

    // Anomaly/heuristic evidence
    /// <summary>Unusual or suspicious pattern detected (may indicate bug or intentional change).</summary>
    AnomalyDetected,

    /// <summary>Heuristic match based on statistical/ML analysis of similar patches.</summary>
    HeuristicMatch,

    /// <summary>Other/unknown evidence type.</summary>
    Other,

    /// <summary>Evidence whose kind could not be determined.</summary>
    Unknown
}

/// <summary>
/// Classifies the level of knowledge certainty about a patch finding.
/// Used to prioritize review focus and distinguish "what we know" from "what we infer".
/// </summary>
public enum PatchKnowledgeLevel
{
    /// <summary>
    /// Known directly from patch syntax (e.g., operator changed from + to -).
    /// High certainty; no inference needed.
    /// </summary>
    KnownFromPatch,

    /// <summary>
    /// Strongly inferred from pattern + semantic analysis.
    /// High confidence inference based on syntax patterns and heuristics.
    /// </summary>
    StronglyInferred,

    /// <summary>
    /// Weakly inferred; based on heuristic or statistical analysis.
    /// May require additional context to confirm.
    /// </summary>
    WeaklyInferred,

    /// <summary>
    /// Unknown from patch alone; requires additional context (git history, tests, PR review).
    /// Cannot be reliably determined from patch syntax.
    /// </summary>
    UnknownFromPatchAlone,

    /// <summary>
    /// Speculative or hypothetical; added by user or external system during analysis.
    /// Should be verified before using in decisions.
    /// </summary>
    Speculative
}

/// <summary>
/// Represents a single piece of evidence supporting a patch finding or transformation.
/// Evidence is the concrete proof that something changed and how we know about it.
/// </summary>
public sealed class PatchEvidence
{
    /// <summary>The kind of evidence (added line, operator change, pattern, etc.).</summary>
    public PatchEvidenceKind Kind
    {
        get; init;
    }

    /// <summary>The level of certainty about this evidence.</summary>
    public PatchKnowledgeLevel KnowledgeLevel { get; init; } = PatchKnowledgeLevel.KnownFromPatch;

    /// <summary>A human-readable description of the evidence.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>The text content or value that is evidence.</summary>
    public string? Text
    {
        get; init;
    }

    /// <summary>The file path where this evidence occurs.</summary>
    public string? FilePath
    {
        get; init;
    }

    /// <summary>The line number (old file) where this evidence occurs, if applicable.</summary>
    public int? OldLineNumber
    {
        get; init;
    }

    /// <summary>The line number (new file) where this evidence occurs, if applicable.</summary>
    public int? NewLineNumber
    {
        get; init;
    }

    /// <summary>
    /// Confidence score (0.0-1.0) indicating certainty that this evidence is correctly identified.
    /// Used to weight evidence in aggregate analysis.
    /// </summary>
    public double Confidence { get; init; } = 1.0;

    /// <summary>
    /// Optional context or surrounding code snippet for reference.
    /// Helps reviewers understand the evidence in context.
    /// </summary>
    public string? Context
    {
        get; init;
    }

    /// <summary>
    /// Optional related evidence (e.g., if this is an OperatorChange, cite the OperatorChange evidence).
    /// Used for dependency tracking.
    /// </summary>
    public IReadOnlyList<string> RelatedEvidenceIds { get; init; } = [];
}

/// <summary>
/// Collects evidence to build a case for patch findings.
/// Provides efficient querying and aggregation of evidence by kind, certainty level, and location.
/// </summary>
public sealed class PatchEvidenceCollection
{
    private readonly List<PatchEvidence> _evidence = [];

    /// <summary>All evidence in this collection.</summary>
    public IReadOnlyList<PatchEvidence> All => _evidence.AsReadOnly();

    /// <summary>Adds a single piece of evidence.</summary>
    public void Add(PatchEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence, nameof(evidence));
        _evidence.Add(evidence);
    }

    /// <summary>Adds multiple pieces of evidence.</summary>
    public void AddRange(IEnumerable<PatchEvidence> evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence, nameof(evidence));
        _evidence.AddRange(evidence);
    }

    /// <summary>Gets all evidence of a specific kind.</summary>
    public IEnumerable<PatchEvidence> ByKind(PatchEvidenceKind kind)
    {
        return _evidence.Where(e => e.Kind == kind);
    }

    /// <summary>Gets all evidence at a specific knowledge level.</summary>
    public IEnumerable<PatchEvidence> ByKnowledgeLevel(PatchKnowledgeLevel level)
    {
        return _evidence.Where(e => e.KnowledgeLevel == level);
    }

    /// <summary>Gets all evidence in a specific file.</summary>
    public IEnumerable<PatchEvidence> ByFile(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath, nameof(filePath));
        return _evidence.Where(e => e.FilePath == filePath);
    }

    /// <summary>Gets all evidence with confidence >= threshold.</summary>
    public IEnumerable<PatchEvidence> ByMinConfidence(double threshold)
    {
        return _evidence.Where(e => e.Confidence >= threshold);
    }

    /// <summary>Gets all "known" evidence (high certainty).</summary>
    public IEnumerable<PatchEvidence> KnownEvidence()
    {
        return _evidence.Where(e => e.KnowledgeLevel == PatchKnowledgeLevel.KnownFromPatch ||
                                    e.KnowledgeLevel == PatchKnowledgeLevel.StronglyInferred);
    }

    /// <summary>Gets all "inferred" evidence (lower certainty).</summary>
    public IEnumerable<PatchEvidence> InferredEvidence()
    {
        return _evidence.Where(e => e.KnowledgeLevel == PatchKnowledgeLevel.WeaklyInferred ||
                                    e.KnowledgeLevel == PatchKnowledgeLevel.UnknownFromPatchAlone);
    }

    /// <summary>Gets all evidence at line number (old or new file).</summary>
    public IEnumerable<PatchEvidence> ByLineNumber(int lineNumber)
    {
        return _evidence.Where(e => e.OldLineNumber == lineNumber || e.NewLineNumber == lineNumber);
    }

    /// <summary>Counts evidence by kind.</summary>
    public Dictionary<PatchEvidenceKind, int> CountByKind()
    {
        return _evidence
            .GroupBy(e => e.Kind)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    /// <summary>Counts evidence by knowledge level.</summary>
    public Dictionary<PatchKnowledgeLevel, int> CountByKnowledgeLevel()
    {
        return _evidence
            .GroupBy(e => e.KnowledgeLevel)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    /// <summary>Calculates aggregate confidence across all evidence.</summary>
    public double AggregateConfidence(string metric = "mean")
    {
        if (_evidence.Count == 0)
        {
            return 0;
        }

        return metric switch
        {
            "mean" => _evidence.Average(e => e.Confidence),
            "min" => _evidence.Min(e => e.Confidence),
            "max" => _evidence.Max(e => e.Confidence),
            "p95" => Percentile(_evidence.Select(e => e.Confidence), 0.95),
            _ => 0
        };
    }

    /// <summary>Gets the total count of evidence.</summary>
    public int Count => _evidence.Count;

    /// <summary>Clears all evidence.</summary>
    public void Clear()
    {
        _evidence.Clear();
    }

    private static double Percentile(IEnumerable<double> values, double percentile)
    {
        var sorted = values.OrderBy(v => v).ToList();
        if (sorted.Count == 0)
        {
            return 0;
        }

        var index = (int)Math.Ceiling(percentile * sorted.Count) - 1;
        return sorted[Math.Max(0, Math.Min(index, sorted.Count - 1))];
    }
}

/// <summary>
/// Helper factory for creating common evidence instances.
/// Reduces boilerplate when building evidence programmatically.
/// </summary>
public static class PatchEvidenceFactory
{
    /// <summary>Creates evidence from an added line.</summary>
    public static PatchEvidence AddedLine(
        int newLineNumber,
        string? filePath,
        string lineContent,
        double confidence = 1.0)
    {
        return new PatchEvidence
        {
            Kind = PatchEvidenceKind.AddedLine,
            KnowledgeLevel = PatchKnowledgeLevel.KnownFromPatch,
            Description = $"Added line at {newLineNumber}",
            Text = lineContent,
            FilePath = filePath,
            NewLineNumber = newLineNumber,
            Confidence = confidence
        };
    }

    /// <summary>Creates evidence from a removed line.</summary>
    public static PatchEvidence RemovedLine(
        int oldLineNumber,
        string? filePath,
        string lineContent,
        double confidence = 1.0)
    {
        return new PatchEvidence
        {
            Kind = PatchEvidenceKind.RemovedLine,
            KnowledgeLevel = PatchKnowledgeLevel.KnownFromPatch,
            Description = $"Removed line at {oldLineNumber}",
            Text = lineContent,
            FilePath = filePath,
            OldLineNumber = oldLineNumber,
            Confidence = confidence
        };
    }

    /// <summary>Creates evidence from an operator change.</summary>
    public static PatchEvidence OperatorChange(
        string oldOp,
        string newOp,
        int? lineNumber,
        string? filePath,
        double confidence = 1.0)
    {
        return new PatchEvidence
        {
            Kind = PatchEvidenceKind.OperatorChange,
            KnowledgeLevel = PatchKnowledgeLevel.KnownFromPatch,
            Description = $"Operator changed: '{oldOp}' → '{newOp}'",
            Text = $"{oldOp} → {newOp}",
            FilePath = filePath,
            NewLineNumber = lineNumber,
            Confidence = confidence
        };
    }

    /// <summary>Creates evidence from a keyword change.</summary>
    public static PatchEvidence KeywordChange(
        string keyword,
        bool wasAdded,
        int? lineNumber,
        string? filePath,
        double confidence = 1.0)
    {
        return new PatchEvidence
        {
            Kind = PatchEvidenceKind.KeywordChange,
            KnowledgeLevel = PatchKnowledgeLevel.KnownFromPatch,
            Description = $"Keyword '{keyword}' {(wasAdded ? "added" : "removed")}",
            Text = keyword,
            FilePath = filePath,
            NewLineNumber = lineNumber,
            Confidence = confidence
        };
    }

    /// <summary>Creates evidence from an identifier change.</summary>
    public static PatchEvidence IdentifierChange(
        string oldName,
        string newName,
        int? lineNumber,
        string? filePath,
        double confidence = 0.95)
    {
        return new PatchEvidence
        {
            Kind = PatchEvidenceKind.IdentifierChange,
            KnowledgeLevel = PatchKnowledgeLevel.KnownFromPatch,
            Description = $"Identifier changed: '{oldName}' → '{newName}'",
            Text = $"{oldName} → {newName}",
            FilePath = filePath,
            NewLineNumber = lineNumber,
            Confidence = confidence
        };
    }

    /// <summary>Creates evidence from a pattern occurrence.</summary>
    public static PatchEvidence PatternOccurrence(
        string patternName,
        string description,
        string? filePath,
        double confidence = 0.8)
    {
        return new PatchEvidence
        {
            Kind = PatchEvidenceKind.PatternOccurrence,
            KnowledgeLevel = PatchKnowledgeLevel.StronglyInferred,
            Description = description,
            Text = patternName,
            FilePath = filePath,
            Confidence = confidence
        };
    }

    /// <summary>Creates evidence from a refactoring pattern.</summary>
    public static PatchEvidence RefactoringPattern(
        string refactoringName,
        string description,
        IEnumerable<string> affectedFiles,
        double confidence = 0.85)
    {
        return new PatchEvidence
        {
            Kind = PatchEvidenceKind.RefactoringPattern,
            KnowledgeLevel = PatchKnowledgeLevel.StronglyInferred,
            Description = description,
            Text = refactoringName,
            FilePath = affectedFiles.FirstOrDefault(),
            Confidence = confidence
        };
    }

    /// <summary>Creates evidence from a security pattern.</summary>
    public static PatchEvidence SecurityPattern(
        string patternName,
        string description,
        string? filePath,
        double confidence = 0.75)
    {
        return new PatchEvidence
        {
            Kind = PatchEvidenceKind.SecurityPattern,
            KnowledgeLevel = PatchKnowledgeLevel.StronglyInferred,
            Description = description,
            Text = patternName,
            FilePath = filePath,
            Confidence = confidence
        };
    }

    /// <summary>Creates evidence from a heuristic match.</summary>
    public static PatchEvidence HeuristicMatch(
        string heuristicName,
        string description,
        string? filePath,
        double confidence = 0.6)
    {
        return new PatchEvidence
        {
            Kind = PatchEvidenceKind.HeuristicMatch,
            KnowledgeLevel = PatchKnowledgeLevel.WeaklyInferred,
            Description = description,
            Text = heuristicName,
            FilePath = filePath,
            Confidence = confidence
        };
    }

    /// <summary>Creates evidence from an anomaly.</summary>
    public static PatchEvidence AnomalyDetected(
        string anomalyType,
        string description,
        string? filePath,
        double confidence = 0.7)
    {
        return new PatchEvidence
        {
            Kind = PatchEvidenceKind.AnomalyDetected,
            KnowledgeLevel = PatchKnowledgeLevel.WeaklyInferred,
            Description = description,
            Text = anomalyType,
            FilePath = filePath,
            Confidence = confidence
        };
    }
}

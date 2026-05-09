namespace GauntletCI.Core.Semantics;

/// <summary>
/// Describes the kind of counterfactual scenario that would expose a behavioral difference.
/// </summary>
public enum PatchCounterfactualKind
{
    /// <summary>
    /// Boundary value case (e.g., amount == limit for > vs >=).
    /// </summary>
    BoundaryValue = 0,

    /// <summary>
    /// Boolean truth table case (e.g., specific combination of flags).
    /// </summary>
    BooleanTruthTableCase = 1,

    /// <summary>
    /// Null reference case.
    /// </summary>
    NullCase = 2,

    /// <summary>
    /// Empty string case ("").
    /// </summary>
    EmptyStringCase = 3,

    /// <summary>
    /// Whitespace-only string case ("   ").
    /// </summary>
    WhitespaceStringCase = 4,

    /// <summary>
    /// Exception case (method throws specific exception).
    /// </summary>
    ExceptionCase = 5,

    /// <summary>
    /// Return value case (method returns specific value).
    /// </summary>
    ReturnValueCase = 6,

    /// <summary>
    /// Test assertion oracle case (e.g., what assertion changes).
    /// </summary>
    AssertionOracleCase = 7,

    /// <summary>
    /// Default/zero value case.
    /// </summary>
    DefaultValueCase = 8,

    /// <summary>
    /// Unknown counterfactual kind.
    /// </summary>
    Unknown = 9
}

/// <summary>
/// Represents a small scenario (counterfactual witness) that would expose a behavioral difference between old and new code.
/// </summary>
/// <remarks>
/// A counterfactual is patch-local reasoning: given a diff, what small input or state would make old and new behave differently?
/// This is not full symbolic execution, but it provides concrete scenarios for review.
/// </remarks>
public sealed class PatchCounterfactual
{
    /// <summary>
    /// Unique identifier for this counterfactual.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The kind of counterfactual scenario.
    /// </summary>
    public required PatchCounterfactualKind Kind { get; init; }

    /// <summary>
    /// Human-readable description of the scenario (e.g., "amount == limit").
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Optional file path where the change occurs.
    /// </summary>
    public string? FilePath { get; init; }

    /// <summary>
    /// Optional primary line number in the new code.
    /// </summary>
    public int? PrimaryLineNumber { get; init; }

    /// <summary>
    /// Optional secondary line number (e.g., for old code line being replaced).
    /// </summary>
    public int? SecondaryLineNumber { get; init; }

    /// <summary>
    /// How strongly the patch supports this scenario.
    /// </summary>
    public PatchKnowledgeLevel KnowledgeLevel { get; init; } = PatchKnowledgeLevel.StronglyInferred;

    /// <summary>
    /// Evidence supporting this counterfactual.
    /// </summary>
    public IReadOnlyList<PatchEvidence> Evidence { get; init; } = [];

    /// <summary>
    /// Optional rules explaining why this scenario applies (e.g., "boundary condition").
    /// </summary>
    public IReadOnlyList<string> Rules { get; init; } = [];

    /// <summary>
    /// Optional note on whether this witness is actually executable in the system.
    /// "Inferred from patch structure" indicates structural reasoning, not actual test execution.
    /// </summary>
    public string? ExecutabilityNote { get; init; }
}

/// <summary>
/// Collection for managing multiple counterfactuals with indexed queries.
/// </summary>
public sealed class PatchCounterfactualCollection
{
    private readonly List<PatchCounterfactual> _items = [];

    /// <summary>
    /// All counterfactuals in the collection.
    /// </summary>
    public IReadOnlyList<PatchCounterfactual> All => _items.AsReadOnly();

    /// <summary>
    /// Number of counterfactuals in the collection.
    /// </summary>
    public int Count => _items.Count;

    /// <summary>
    /// Add a counterfactual to the collection.
    /// </summary>
    public void Add(PatchCounterfactual counterfactual)
    {
        ArgumentNullException.ThrowIfNull(counterfactual);
        _items.Add(counterfactual);
    }

    /// <summary>
    /// Add multiple counterfactuals to the collection.
    /// </summary>
    public void AddRange(IEnumerable<PatchCounterfactual> counterfactuals)
    {
        ArgumentNullException.ThrowIfNull(counterfactuals);
        _items.AddRange(counterfactuals);
    }

    /// <summary>
    /// Clear all counterfactuals from the collection.
    /// </summary>
    public void Clear()
    {
        _items.Clear();
    }

    /// <summary>
    /// Get counterfactuals by kind.
    /// </summary>
    public IEnumerable<PatchCounterfactual> ByKind(PatchCounterfactualKind kind)
    {
        return _items.Where(c => c.Kind == kind);
    }

    /// <summary>
    /// Get counterfactuals by file path.
    /// </summary>
    public IEnumerable<PatchCounterfactual> ByFile(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        return _items.Where(c => c.FilePath == filePath);
    }

    /// <summary>
    /// Get counterfactuals by knowledge level.
    /// </summary>
    public IEnumerable<PatchCounterfactual> ByKnowledgeLevel(PatchKnowledgeLevel level)
    {
        return _items.Where(c => c.KnowledgeLevel == level);
    }

    /// <summary>
    /// Get counterfactuals with minimum knowledge level (inclusive).
    /// </summary>
    public IEnumerable<PatchCounterfactual> ByMinKnowledgeLevel(PatchKnowledgeLevel minLevel)
    {
        return _items.Where(c => c.KnowledgeLevel >= minLevel);
    }

    /// <summary>
    /// Count counterfactuals by kind.
    /// </summary>
    public int CountByKind(PatchCounterfactualKind kind)
    {
        return _items.Count(c => c.Kind == kind);
    }

    /// <summary>
    /// Count counterfactuals by knowledge level.
    /// </summary>
    public int CountByKnowledgeLevel(PatchKnowledgeLevel level)
    {
        return _items.Count(c => c.KnowledgeLevel == level);
    }
}

/// <summary>
/// Factory for creating counterfactuals with consistent defaults.
/// </summary>
public static class PatchCounterfactualFactory
{
    private static int _idCounter = 1;

    /// <summary>
    /// Create a boundary value counterfactual (e.g., amount == limit).
    /// </summary>
    public static PatchCounterfactual BoundaryValue(
        string description,
        string? filePath = null,
        int? lineNumber = null,
        PatchKnowledgeLevel knowledgeLevel = PatchKnowledgeLevel.StronglyInferred,
        IEnumerable<PatchEvidence>? evidence = null)
    {
        return new PatchCounterfactual
        {
            Id = $"cf-boundary-{_idCounter++}",
            Kind = PatchCounterfactualKind.BoundaryValue,
            Description = description,
            FilePath = filePath,
            PrimaryLineNumber = lineNumber,
            KnowledgeLevel = knowledgeLevel,
            Evidence = evidence?.ToList() ?? [],
            Rules = ["Boundary condition: test the exact boundary value between old and new behavior"],
            ExecutabilityNote = "Inferred from patch structure"
        };
    }

    /// <summary>
    /// Create a boolean truth table counterfactual (e.g., specific flag combination).
    /// </summary>
    public static PatchCounterfactual BooleanTruthTable(
        string description,
        string? filePath = null,
        int? lineNumber = null,
        PatchKnowledgeLevel knowledgeLevel = PatchKnowledgeLevel.StronglyInferred,
        IEnumerable<PatchEvidence>? evidence = null)
    {
        return new PatchCounterfactual
        {
            Id = $"cf-bool-{_idCounter++}",
            Kind = PatchCounterfactualKind.BooleanTruthTableCase,
            Description = description,
            FilePath = filePath,
            PrimaryLineNumber = lineNumber,
            KnowledgeLevel = knowledgeLevel,
            Evidence = evidence?.ToList() ?? [],
            Rules = ["Boolean operator changed: test specific truth value combination"],
            ExecutabilityNote = "Inferred from patch structure"
        };
    }

    /// <summary>
    /// Create a null reference counterfactual.
    /// </summary>
    public static PatchCounterfactual NullCase(
        string description,
        string? filePath = null,
        int? lineNumber = null,
        PatchKnowledgeLevel knowledgeLevel = PatchKnowledgeLevel.StronglyInferred,
        IEnumerable<PatchEvidence>? evidence = null)
    {
        return new PatchCounterfactual
        {
            Id = $"cf-null-{_idCounter++}",
            Kind = PatchCounterfactualKind.NullCase,
            Description = description,
            FilePath = filePath,
            PrimaryLineNumber = lineNumber,
            KnowledgeLevel = knowledgeLevel,
            Evidence = evidence?.ToList() ?? [],
            Rules = ["Null checking semantics changed: test null reference scenarios"],
            ExecutabilityNote = "Inferred from patch structure"
        };
    }

    /// <summary>
    /// Create an empty string counterfactual.
    /// </summary>
    public static PatchCounterfactual EmptyStringCase(
        string description,
        string? filePath = null,
        int? lineNumber = null,
        PatchKnowledgeLevel knowledgeLevel = PatchKnowledgeLevel.StronglyInferred,
        IEnumerable<PatchEvidence>? evidence = null)
    {
        return new PatchCounterfactual
        {
            Id = $"cf-empty-{_idCounter++}",
            Kind = PatchCounterfactualKind.EmptyStringCase,
            Description = description,
            FilePath = filePath,
            PrimaryLineNumber = lineNumber,
            KnowledgeLevel = knowledgeLevel,
            Evidence = evidence?.ToList() ?? [],
            Rules = ["String checking logic changed: test empty string edge case"],
            ExecutabilityNote = "Inferred from patch structure"
        };
    }

    /// <summary>
    /// Create a whitespace string counterfactual.
    /// </summary>
    public static PatchCounterfactual WhitespaceStringCase(
        string description,
        string? filePath = null,
        int? lineNumber = null,
        PatchKnowledgeLevel knowledgeLevel = PatchKnowledgeLevel.StronglyInferred,
        IEnumerable<PatchEvidence>? evidence = null)
    {
        return new PatchCounterfactual
        {
            Id = $"cf-whitespace-{_idCounter++}",
            Kind = PatchCounterfactualKind.WhitespaceStringCase,
            Description = description,
            FilePath = filePath,
            PrimaryLineNumber = lineNumber,
            KnowledgeLevel = knowledgeLevel,
            Evidence = evidence?.ToList() ?? [],
            Rules = ["String checking logic changed: test whitespace-only edge case"],
            ExecutabilityNote = "Inferred from patch structure"
        };
    }

    /// <summary>
    /// Create an exception counterfactual.
    /// </summary>
    public static PatchCounterfactual ExceptionCase(
        string description,
        string? filePath = null,
        int? lineNumber = null,
        PatchKnowledgeLevel knowledgeLevel = PatchKnowledgeLevel.StronglyInferred,
        IEnumerable<PatchEvidence>? evidence = null)
    {
        return new PatchCounterfactual
        {
            Id = $"cf-exc-{_idCounter++}",
            Kind = PatchCounterfactualKind.ExceptionCase,
            Description = description,
            FilePath = filePath,
            PrimaryLineNumber = lineNumber,
            KnowledgeLevel = knowledgeLevel,
            Evidence = evidence?.ToList() ?? [],
            Rules = ["Exception handling or validation changed: describe the scenario that would throw"],
            ExecutabilityNote = "Inferred from patch structure"
        };
    }

    /// <summary>
    /// Create a return value counterfactual.
    /// </summary>
    public static PatchCounterfactual ReturnValueCase(
        string description,
        string? filePath = null,
        int? lineNumber = null,
        PatchKnowledgeLevel knowledgeLevel = PatchKnowledgeLevel.StronglyInferred,
        IEnumerable<PatchEvidence>? evidence = null)
    {
        return new PatchCounterfactual
        {
            Id = $"cf-ret-{_idCounter++}",
            Kind = PatchCounterfactualKind.ReturnValueCase,
            Description = description,
            FilePath = filePath,
            PrimaryLineNumber = lineNumber,
            KnowledgeLevel = knowledgeLevel,
            Evidence = evidence?.ToList() ?? [],
            Rules = ["Return behavior changed: describe the scenario and expected return value"],
            ExecutabilityNote = "Inferred from patch structure"
        };
    }

    /// <summary>
    /// Create an assertion oracle counterfactual (test oracle change).
    /// </summary>
    public static PatchCounterfactual AssertionOracleCase(
        string description,
        string? filePath = null,
        int? lineNumber = null,
        PatchKnowledgeLevel knowledgeLevel = PatchKnowledgeLevel.StronglyInferred,
        IEnumerable<PatchEvidence>? evidence = null)
    {
        return new PatchCounterfactual
        {
            Id = $"cf-assert-{_idCounter++}",
            Kind = PatchCounterfactualKind.AssertionOracleCase,
            Description = description,
            FilePath = filePath,
            PrimaryLineNumber = lineNumber,
            KnowledgeLevel = knowledgeLevel,
            Evidence = evidence?.ToList() ?? [],
            Rules = ["Test assertion changed: describe what was being asserted and how it changed"],
            ExecutabilityNote = "Inferred from patch structure"
        };
    }

    /// <summary>
    /// Create a default/zero value counterfactual.
    /// </summary>
    public static PatchCounterfactual DefaultValueCase(
        string description,
        string? filePath = null,
        int? lineNumber = null,
        PatchKnowledgeLevel knowledgeLevel = PatchKnowledgeLevel.StronglyInferred,
        IEnumerable<PatchEvidence>? evidence = null)
    {
        return new PatchCounterfactual
        {
            Id = $"cf-default-{_idCounter++}",
            Kind = PatchCounterfactualKind.DefaultValueCase,
            Description = description,
            FilePath = filePath,
            PrimaryLineNumber = lineNumber,
            KnowledgeLevel = knowledgeLevel,
            Evidence = evidence?.ToList() ?? [],
            Rules = ["Default or zero value handling changed: test with default/zero scenarios"],
            ExecutabilityNote = "Inferred from patch structure"
        };
    }
}

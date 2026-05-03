using GauntletCI.Core.Domain;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Serialization;

/// <summary>
/// Extension methods for converting between Finding (string-based) and domain types.
/// Enables gradual adoption of domain types without requiring immediate refactoring of all consumers.
/// </summary>
public static class DomainTypeConversionExtensions
{
    /// <summary>
    /// Converts a Finding's rule ID string to a RuleIdentifier (if valid).
    /// Returns null if the rule ID is invalid or malformed.
    /// </summary>
    public static RuleIdentifier? ToRuleIdentifier(this Finding finding)
    {
        if (finding?.RuleId == null)
            return null;
        
        return RuleIdentifier.TryParse(finding.RuleId, out var result) ? result : null;
    }

    /// <summary>
    /// Converts a Finding's file path string to a CodeFilePath (if valid).
    /// Returns null if the file path is null or invalid.
    /// </summary>
    public static CodeFilePath? ToCodeFilePath(this Finding finding)
    {
        if (finding?.FilePath == null)
            return null;
        
        return CodeFilePath.TryParse(finding.FilePath, out var result) ? result : null;
    }

    /// <summary>
    /// Converts a Finding's evidence string to an LlmExplanation.
    /// Never returns null (uses empty explanation if evidence is null).
    /// </summary>
    public static LlmExplanation ToLlmExplanation(this Finding finding)
    {
        return LlmExplanation.Create(finding?.Evidence);
    }

    /// <summary>
    /// Applies domain types to a Finding, validating and storing results.
    /// Returns a result object indicating success/failure for each conversion.
    /// </summary>
    public static FindingDomainConversionResult ApplyDomainTypes(this Finding finding)
    {
        return new FindingDomainConversionResult
        {
            RuleId = finding.ToRuleIdentifier(),
            FilePath = finding.ToCodeFilePath(),
            Explanation = finding.ToLlmExplanation(),
            IsValidRuleId = RuleIdentifier.TryParse(finding.RuleId, out _),
            IsValidFilePath = CodeFilePath.TryParse(finding.FilePath ?? "", out _),
        };
    }
}

/// <summary>
/// Result of attempting to convert a Finding's string fields to domain types.
/// </summary>
public sealed record FindingDomainConversionResult
{
    /// <summary>Parsed rule identifier, or null if invalid.</summary>
    public RuleIdentifier? RuleId { get; init; }

    /// <summary>Parsed file path, or null if invalid.</summary>
    public CodeFilePath? FilePath { get; init; }

    /// <summary>Parsed LLM explanation.</summary>
    public LlmExplanation Explanation { get; init; }

    /// <summary>Whether the rule ID was successfully converted.</summary>
    public bool IsValidRuleId { get; init; }

    /// <summary>Whether the file path was successfully converted.</summary>
    public bool IsValidFilePath { get; init; }

    /// <summary>True if all conversions succeeded.</summary>
    public bool AllConversionsSucceeded => IsValidRuleId && IsValidFilePath;
}

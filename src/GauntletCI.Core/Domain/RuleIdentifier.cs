using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace GauntletCI.Core.Domain;

/// <summary>
/// Strongly-typed wrapper for GauntletCI rule identifiers (e.g., "GCI0001").
/// Ensures that rule IDs are validated at compile-time and provide semantic meaning.
/// </summary>
public readonly record struct RuleIdentifier : IEquatable<RuleIdentifier>, IComparable<RuleIdentifier>
{
    private static readonly Regex RuleIdPattern = new(@"^GCI\d{4}$", RegexOptions.Compiled);
    
    /// <summary>Gets the raw rule ID value (e.g., "GCI0001").</summary>
    public string Value { get; }

    /// <summary>Gets the numeric portion of the rule ID (e.g., 1 for "GCI0001").</summary>
    public int Number => int.Parse(Value.Substring(3));

    /// <summary>Initializes a new RuleIdentifier with the given value.</summary>
    /// <param name="value">The rule ID value (must match GCI#### format).</param>
    /// <exception cref="ArgumentException">Thrown if value doesn't match the expected format.</exception>
    private RuleIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Rule ID cannot be null or empty.", nameof(value));

        if (!RuleIdPattern.IsMatch(value))
            throw new ArgumentException(
                $"Rule ID must match format 'GCIXXXX' where X is a digit. Got: {value}",
                nameof(value));

        Value = value;
    }

    /// <summary>Attempts to parse a string into a RuleIdentifier.</summary>
    /// <param name="value">The value to parse.</param>
    /// <param name="result">The parsed RuleIdentifier if successful.</param>
    /// <returns>True if parsing succeeded; false otherwise.</returns>
    public static bool TryParse(string? value, out RuleIdentifier result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value) || !RuleIdPattern.IsMatch(value))
            return false;

        result = new RuleIdentifier(value);
        return true;
    }

    /// <summary>Parses a string into a RuleIdentifier.</summary>
    /// <param name="value">The value to parse.</param>
    /// <returns>The parsed RuleIdentifier.</returns>
    /// <exception cref="ArgumentException">Thrown if value is invalid.</exception>
    public static RuleIdentifier Parse(string value)
    {
        if (!TryParse(value, out var result))
            throw new ArgumentException(
                $"Invalid rule ID format: '{value}'. Expected format: GCIXXXX where X is a digit.",
                nameof(value));
        return result;
    }

    /// <summary>Creates a RuleIdentifier from a numeric ID (e.g., 1 → "GCI0001").</summary>
    /// <param name="number">The numeric rule ID (0-9999).</param>
    /// <returns>A RuleIdentifier for the given number.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if number is outside valid range.</exception>
    public static RuleIdentifier FromNumber(int number)
    {
        if (number < 0 || number > 9999)
            throw new ArgumentOutOfRangeException(nameof(number), "Rule number must be between 0 and 9999.");
        return new RuleIdentifier($"GCI{number:D4}");
    }

    /// <summary>Converts the RuleIdentifier to its string representation.</summary>
    public override string ToString() => Value;

    /// <summary>Compares two RuleIdentifiers by their numeric value.</summary>
    public int CompareTo(RuleIdentifier other) => Number.CompareTo(other.Number);

    /// <summary>Determines whether two RuleIdentifiers are equal.</summary>
    public bool Equals(RuleIdentifier other) => Value == other.Value;

    /// <summary>Gets the hash code for this RuleIdentifier.</summary>
    public override int GetHashCode() => Value.GetHashCode();

    /// <summary>Implicitly converts a string to a RuleIdentifier.</summary>
    public static implicit operator RuleIdentifier(string value) => Parse(value);

    /// <summary>Implicitly converts a RuleIdentifier to its string value.</summary>
    public static implicit operator string(RuleIdentifier id) => id.Value;

    /// <summary>Determines whether a string is a valid RuleIdentifier format.</summary>
    public static bool IsValid(string? value) => !string.IsNullOrWhiteSpace(value) && RuleIdPattern.IsMatch(value);
}

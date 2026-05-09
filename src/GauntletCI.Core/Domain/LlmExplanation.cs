namespace GauntletCI.Core.Domain;

/// <summary>
/// Strongly-typed wrapper for LLM-generated explanations.
/// Provides metadata about text length, format, and content validation.
/// </summary>
public readonly record struct LlmExplanation : IEquatable<LlmExplanation>
{
    /// <summary>Gets the explanation text content.</summary>
    public string Value { get; }

    /// <summary>Gets the number of words in the explanation.</summary>
    public int WordCount => string.IsNullOrWhiteSpace(Value) ? 0 : Value.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;

    /// <summary>Gets whether the explanation is empty or whitespace-only.</summary>
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

    /// <summary>Gets the character count of the explanation.</summary>
    public int CharCount => Value?.Length ?? 0;

    /// <summary>Gets the number of lines in the explanation.</summary>
    public int LineCount => string.IsNullOrWhiteSpace(Value) ? 0 : Value.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;

    /// <summary>Initializes a new LlmExplanation with the given text.</summary>
    /// <param name="value">The explanation text. Can be null or empty.</param>
    private LlmExplanation(string? value)
    {
        Value = value ?? string.Empty;
    }

    /// <summary>Attempts to create an LlmExplanation from text.</summary>
    /// <param name="value">The explanation text.</param>
    /// <param name="result">The created LlmExplanation if successful.</param>
    /// <returns>True if creation succeeded (always true for this type).</returns>
    public static bool TryCreate(string? value, out LlmExplanation result)
    {
        result = new LlmExplanation(value);
        return true;
    }

    /// <summary>Creates an LlmExplanation from text.</summary>
    /// <param name="value">The explanation text (can be null or empty).</param>
    /// <returns>The created LlmExplanation.</returns>
    public static LlmExplanation Create(string? value) => new(value);

    /// <summary>Creates an empty LlmExplanation.</summary>
    public static LlmExplanation Empty => new(string.Empty);

    /// <summary>Converts the explanation to its string representation.</summary>
    public override string ToString() => Value;

    /// <summary>Determines whether two LlmExplanations are equal.</summary>
    public bool Equals(LlmExplanation other) => Value == other.Value;

    /// <summary>Gets the hash code for this LlmExplanation.</summary>
    public override int GetHashCode() => Value.GetHashCode();

    /// <summary>Implicitly converts an LlmExplanation to its string value.</summary>
    public static implicit operator string(LlmExplanation explanation) => explanation.Value;

    /// <summary>Implicitly converts a string to an LlmExplanation.</summary>
    public static implicit operator LlmExplanation(string? value) => new(value);

    /// <summary>Gets the first N words of the explanation (useful for previews).</summary>
    /// <param name="wordCount">Number of words to extract.</param>
    /// <returns>A truncated preview with ellipsis if text was longer.</returns>
    public string Preview(int wordCount = 10)
    {
        if (IsEmpty) return string.Empty;
        var words = Value.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length <= wordCount)
            return Value;
        return string.Join(" ", words.Take(wordCount)) + "...";
    }

    /// <summary>Determines whether the explanation contains markdown formatting.</summary>
    public bool HasMarkdown => Value.Contains("##") || Value.Contains("**") || Value.Contains("`") || Value.Contains("[");
}

using System.Diagnostics.CodeAnalysis;

namespace GauntletCI.Core.Domain;

/// <summary>
/// Strongly-typed wrapper for code file paths.
/// Normalizes path separators and provides semantic information about file context.
/// </summary>
public readonly record struct CodeFilePath : IEquatable<CodeFilePath>, IComparable<CodeFilePath>
{
    /// <summary>Gets the normalized file path (using forward slashes).</summary>
    public string Value { get; }

    /// <summary>Gets the file name without directory (e.g., "file.cs").</summary>
    public string FileName => Path.GetFileName(Value);

    /// <summary>Gets the file extension (e.g., ".cs").</summary>
    public string Extension => Path.GetExtension(Value);

    /// <summary>Gets whether this path represents a test file.</summary>
    public bool IsTest => Value.Contains(".Test", StringComparison.OrdinalIgnoreCase) ||
                         Value.Contains("/test/", StringComparison.OrdinalIgnoreCase) ||
                         Value.Contains("/tests/", StringComparison.OrdinalIgnoreCase) ||
                         Value.StartsWith("test/", StringComparison.OrdinalIgnoreCase) ||
                         Value.StartsWith("tests/", StringComparison.OrdinalIgnoreCase) ||
                         Value.EndsWith(".tests.cs", StringComparison.OrdinalIgnoreCase) ||
                         Value.EndsWith(".test.cs", StringComparison.OrdinalIgnoreCase);

    /// <summary>Gets whether this path represents a benchmark file.</summary>
    public bool IsBenchmark => Value.Contains("benchmark", StringComparison.OrdinalIgnoreCase) ||
                              Value.Contains("perf", StringComparison.OrdinalIgnoreCase) ||
                              Value.EndsWith(".bench.cs", StringComparison.OrdinalIgnoreCase);

    /// <summary>Initializes a new CodeFilePath with the given value.</summary>
    /// <param name="value">The file path (relative or absolute).</param>
    /// <exception cref="ArgumentException">Thrown if value is null, empty, or whitespace.</exception>
    private CodeFilePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("File path cannot be null, empty, or whitespace.", nameof(value));

        // Normalize path separators to forward slashes
        Value = value.Replace('\\', '/');
    }

    /// <summary>Attempts to parse a string into a CodeFilePath.</summary>
    /// <param name="value">The value to parse.</param>
    /// <param name="result">The parsed CodeFilePath if successful.</param>
    /// <returns>True if parsing succeeded; false otherwise.</returns>
    public static bool TryParse(string? value, out CodeFilePath result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        result = new CodeFilePath(value);
        return true;
    }

    /// <summary>Parses a string into a CodeFilePath.</summary>
    /// <param name="value">The value to parse.</param>
    /// <returns>The parsed CodeFilePath.</returns>
    /// <exception cref="ArgumentException">Thrown if value is invalid.</exception>
    public static CodeFilePath Parse(string value)
    {
        if (!TryParse(value, out var result))
            throw new ArgumentException("File path cannot be null, empty, or whitespace.", nameof(value));
        return result;
    }

    /// <summary>Creates a CodeFilePath from a path, normalizing separators.</summary>
    /// <param name="path">The file path (absolute or relative).</param>
    /// <returns>A CodeFilePath with normalized separators.</returns>
    public static CodeFilePath FromPath(string path) => new(path);

    /// <summary>Converts the CodeFilePath to its string representation.</summary>
    public override string ToString() => Value;

    /// <summary>Compares two CodeFilePaths lexicographically.</summary>
    public int CompareTo(CodeFilePath other) => Value.CompareTo(other.Value);

    /// <summary>Determines whether two CodeFilePaths are equal.</summary>
    public bool Equals(CodeFilePath other) => Value.Equals(other.Value, StringComparison.Ordinal);

    /// <summary>Gets the hash code for this CodeFilePath.</summary>
    public override int GetHashCode() => Value.GetHashCode();

    /// <summary>Implicitly converts a CodeFilePath to its string value.</summary>
    public static implicit operator string(CodeFilePath path) => path.Value;

    /// <summary>Explicitly converts a string to a CodeFilePath.</summary>
    public static explicit operator CodeFilePath(string value) => Parse(value);

    /// <summary>Determines whether a string is a valid CodeFilePath (non-empty).</summary>
    public static bool IsValid(string? value) => !string.IsNullOrWhiteSpace(value);
}

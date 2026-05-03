using System.Text.Json;
using System.Text.Json.Serialization;
using GauntletCI.Core.Domain;

namespace GauntletCI.Core.Serialization;

/// <summary>
/// JSON converter for RuleIdentifier that serializes to/from string.
/// Enables seamless System.Text.Json support for the strongly-typed domain type.
/// </summary>
public class RuleIdentifierJsonConverter : JsonConverter<RuleIdentifier>
{
    /// <summary>Reads a RuleIdentifier from JSON (expects a string value).</summary>
    public override RuleIdentifier Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value))
            throw new JsonException("RuleIdentifier cannot be null or empty");
        
        if (!RuleIdentifier.TryParse(value, out var result))
            throw new JsonException($"Invalid RuleIdentifier format: {value}");
        
        return result;
    }

    /// <summary>Writes a RuleIdentifier to JSON (as a string).</summary>
    public override void Write(Utf8JsonWriter writer, RuleIdentifier value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

/// <summary>
/// JSON converter for CodeFilePath that serializes to/from string.
/// Enables seamless System.Text.Json support for the strongly-typed domain type.
/// </summary>
public class CodeFilePathJsonConverter : JsonConverter<CodeFilePath>
{
    /// <summary>Reads a CodeFilePath from JSON (expects a string value).</summary>
    public override CodeFilePath Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value))
            throw new JsonException("CodeFilePath cannot be null or empty");
        
        if (!CodeFilePath.TryParse(value, out var result))
            throw new JsonException($"Invalid CodeFilePath: {value}");
        
        return result;
    }

    /// <summary>Writes a CodeFilePath to JSON (as a string).</summary>
    public override void Write(Utf8JsonWriter writer, CodeFilePath value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

/// <summary>
/// JSON converter for CodeFilePath? (nullable) that serializes to/from string or null.
/// Enables seamless System.Text.Json support for optional file paths.
/// </summary>
public class NullableCodeFilePathJsonConverter : JsonConverter<CodeFilePath?>
{
    /// <summary>Reads an optional CodeFilePath from JSON (string or null).</summary>
    public override CodeFilePath? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;
        
        var value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value))
            return null;
        
        if (!CodeFilePath.TryParse(value, out var result))
            throw new JsonException($"Invalid CodeFilePath: {value}");
        
        return result;
    }

    /// <summary>Writes an optional CodeFilePath to JSON (string or null).</summary>
    public override void Write(Utf8JsonWriter writer, CodeFilePath? value, JsonSerializerOptions options)
    {
        if (value == null)
            writer.WriteNullValue();
        else
            writer.WriteStringValue(value.Value.ToString());
    }
}

/// <summary>
/// JSON converter for LlmExplanation that serializes to/from string.
/// Enables seamless System.Text.Json support for the strongly-typed domain type.
/// </summary>
public class LlmExplanationJsonConverter : JsonConverter<LlmExplanation>
{
    /// <summary>Reads an LlmExplanation from JSON (expects a string value).</summary>
    public override LlmExplanation Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return LlmExplanation.Create(value);
    }

    /// <summary>Writes an LlmExplanation to JSON (as a string).</summary>
    public override void Write(Utf8JsonWriter writer, LlmExplanation value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

/// <summary>
/// JSON converter for LlmExplanation? (nullable) that serializes to/from string or null.
/// Enables seamless System.Text.Json support for optional explanations.
/// </summary>
public class NullableLlmExplanationJsonConverter : JsonConverter<LlmExplanation?>
{
    /// <summary>Reads an optional LlmExplanation from JSON (string or null).</summary>
    public override LlmExplanation? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;
        
        var value = reader.GetString();
        return LlmExplanation.Create(value);
    }

    /// <summary>Writes an optional LlmExplanation to JSON (string or null).</summary>
    public override void Write(Utf8JsonWriter writer, LlmExplanation? value, JsonSerializerOptions options)
    {
        if (value == null)
            writer.WriteNullValue();
        else
            writer.WriteStringValue(value.Value.ToString());
    }
}

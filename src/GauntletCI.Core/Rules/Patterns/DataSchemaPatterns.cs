// SPDX-License-Identifier: Elastic-2.0

// ========== GauntletCI Self-Analysis: WHITELISTED FILE ==========
// This file contains pattern strings and regexes used by GauntletCI detection rules.
// Pattern strings (e.g., "new FileStream(", "TODO", ".GetService<") appear in the code by design.
// These are NOT actual code violations, but rather PATTERN DEFINITIONS used to detect violations in other code.
// GauntletCI Self-Analysis should skip this file from analysis to avoid false positives on pattern data.
// =================================================================

namespace GauntletCI.Core.Rules.Patterns;

/// <summary>
/// Patterns used to detect data schema and serialization compatibility issues.
/// </summary>
internal static class DataSchemaPatterns
{
    /// <summary>
    /// Serialization and schema-mapping attributes that indicate a field is part of a wire format
    /// or persistent storage contract. Removal breaks deserialization of existing data.
    /// Used by GCI0021 to detect removed serialization attributes.
    /// Covers: JSON serialization, ORM/EF Core mapping, data annotations, and serialization frameworks.
    /// </summary>
    public static readonly string[] SerializationAttributes =
    [
        // JSON serialization
        "[JsonProperty", "[JsonPropertyName", "[JsonIgnore", "[JsonRequired",
        // ORM/EF Core field mapping
        "[Column(", "[Table(", "[Index(", "[Unique(", "[Keyless]",
        "[ComplexType(", "[PrimaryKey(", "[NotMapped]",
        // Data annotations
        "[DataMember", "[DataContract", "[EnumMember",
        // Validation attributes (removal affects contracts)
        "[Required]", "[MaxLength", "[MinLength", "[StringLength",
        "[Key]", "[ForeignKey",
        // XML serialization
        "[XmlElement", "[XmlAttribute", "[XmlType", "[XmlRoot",
        // NoSQL/MongoDB
        "[BsonElement", "[BsonId", "[BsonRepresentation",
        // Precision and scale for decimals
        "[Precision(", "[Scale(",
        // gRPC ProtoContract from protobuf
        "[ProtoMember", "[ProtoContract",
    ];
}

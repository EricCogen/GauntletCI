// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0021 – Data &amp; Schema Compatibility
/// Detects removed serialization attributes and enum member removals that may break
/// existing stored data, caches, or wire formats.
/// </summary>
public class GCI0021_DataSchemaCompatibility : RuleBase
{
    public override string Id => "GCI0021";
    public override string Name => "Data & Schema Compatibility";

    private static readonly string[] SerializationAttributes =
    [
        "[JsonProperty", "[JsonPropertyName", "[Column(", "[DataMember",
        "[BsonElement", "[Key]", "[ForeignKey", "[Required]", "[MaxLength"
    ];

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var diff = context.Diff;
        var findings = new List<Finding>();

        foreach (var file in diff.Files)
        {
            CheckRemovedSerializationAttributes(file, findings);
            CheckRemovedEnumMembers(file, findings);
        }

        return Task.FromResult(findings);
    }

    private void CheckRemovedSerializationAttributes(DiffFile file, List<Finding> findings)
    {
        foreach (var line in file.RemovedLines)
        {
            var content = line.Content.Trim();
            foreach (var attr in SerializationAttributes)
            {
                if (!content.Contains(attr, StringComparison.OrdinalIgnoreCase)) continue;

                findings.Add(CreateFinding(
                    summary: $"Serialization attribute removed in {file.NewPath}: {content}",
                    evidence: $"Removed line ~{line.OldLineNumber}: {content}",
                    whyItMatters: "Removing or renaming serialized fields breaks deserialization of existing data in databases, caches, message queues, and APIs.",
                    suggestedAction: "Keep the old property and mark it [Obsolete], or add a migration and version the schema explicitly.",
                    confidence: Confidence.High));
                break;
            }
        }
    }

    private void CheckRemovedEnumMembers(DiffFile file, List<Finding> findings)
    {
        var allLines = file.Hunks.SelectMany(h => h.Lines).ToList();

        bool inEnumBody = false;
        bool pendingEnumOpen = false;
        int braceDepth = 0;
        int enumBraceDepth = 0;

        foreach (var line in allLines)
        {
            var raw = line.Content;

            if (raw.Contains("enum ", StringComparison.Ordinal))
            {
                pendingEnumOpen = true;
                inEnumBody = false;
            }

            foreach (var c in raw)
            {
                if (c == '{')
                {
                    braceDepth++;
                    if (pendingEnumOpen)
                    {
                        inEnumBody = true;
                        pendingEnumOpen = false;
                        enumBraceDepth = braceDepth;
                    }
                }
                else if (c == '}')
                {
                    if (inEnumBody && braceDepth == enumBraceDepth)
                        inEnumBody = false;
                    braceDepth--;
                }
            }

            if (line.Kind != DiffLineKind.Removed) continue;

            var content = raw.Trim();
            if (content.Length == 0 || content.StartsWith("//")) continue;
            if (!inEnumBody) continue;
            if (!IsEnumMember(content)) continue;

            findings.Add(CreateFinding(
                summary: $"Enum member removed in {file.NewPath}: {content}",
                evidence: $"Removed line ~{line.OldLineNumber}: {content}",
                whyItMatters: "Removing enum members breaks deserialization of persisted integer or string values that mapped to the removed member.",
                suggestedAction: "Mark the enum member [Obsolete] instead of removing it, or add a database migration to remap stored values.",
                confidence: Confidence.Medium));
        }
    }

    private static bool IsEnumMember(string content)
    {
        // Matches: "SomeName," or "SomeName = 5," or "SomeName = 0x1,"
        var trimmed = content.TrimEnd(',').Trim();
        // Split on '=' to handle "Name = Value"
        var name = trimmed.Split('=')[0].Trim();
        return name.Length > 0 &&
               char.IsUpper(name[0]) &&
               name.All(c => char.IsLetterOrDigit(c) || c == '_') &&
               !content.Contains('(') && !content.Contains('{');
    }
}

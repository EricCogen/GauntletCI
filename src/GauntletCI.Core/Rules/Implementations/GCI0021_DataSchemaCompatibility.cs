// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0021, Data &amp; Schema Compatibility
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
        if (WellKnownPatterns.IsGeneratedFile(file.NewPath)) return;

        foreach (var line in file.RemovedLines)
        {
            var content = line.Content.Trim();
            // Attributes always appear at the start of a line (after trimming).
            // Use StartsWith to avoid matching indexer syntax like dictionary[key] against [Key].
            foreach (var attr in SerializationAttributes)
            {
                if (!content.StartsWith(attr, StringComparison.OrdinalIgnoreCase)) continue;

                findings.Add(CreateFinding(
                    file,
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
        if (WellKnownPatterns.IsGeneratedFile(file.NewPath)) return;

        // Collect enum member names present in added lines: skips members that were
        // moved (refactored into a new namespace/file) rather than truly deleted.
        var addedMemberNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var addedLine in file.AddedLines)
        {
            var ac = addedLine.Content.Trim();
            if (IsEnumMember(ac))
                addedMemberNames.Add(ac.TrimEnd(',').Trim().Split('=')[0].Trim());
        }

        var allLines = file.Hunks.SelectMany(h => h.Lines).ToList();

        bool inEnumBody = false;
        bool pendingEnumOpen = false;
        int braceDepth = 0;
        int enumBraceDepth = 0;
        // Tracks the last removed line inside the enum body, used to detect preceding serialization attributes.
        string lastRemovedInEnum = string.Empty;

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

            if (!inEnumBody)
            {
                lastRemovedInEnum = string.Empty;
                continue;
            }

            if (line.Kind != DiffLineKind.Removed)
            {
                // Context lines between removed lines break the attribute-member adjacency assumption.
                lastRemovedInEnum = string.Empty;
                continue;
            }

            var content = raw.Trim();
            if (content.Length == 0 || content.StartsWith("//")) continue;
            if (!IsEnumMember(content))
            {
                lastRemovedInEnum = content;
                continue;
            }

            var memberName = content.TrimEnd(',').Trim().Split('=')[0].Trim();
            if (addedMemberNames.Contains(memberName))
            {
                lastRemovedInEnum = content;
                continue; // moved, not deleted
            }

            // Only flag members that have an explicit serialization attribute on the preceding
            // removed line: this ensures we only flag truly serialized enums (e.g. [JsonProperty("x")]).
            // Internal/API enums without serialization attributes are not a schema compat concern.
            bool hasPrecedingSerializationAttr = SerializationAttributes.Any(a =>
                lastRemovedInEnum.TrimStart().StartsWith(a, StringComparison.OrdinalIgnoreCase));

            lastRemovedInEnum = content;

            if (!hasPrecedingSerializationAttr) continue;

            findings.Add(CreateFinding(
                file,
                summary: $"Enum member removed in {file.NewPath}: {content}",
                evidence: $"Removed line ~{line.OldLineNumber}: {content}",
                whyItMatters: "Removing enum members breaks deserialization of persisted integer or string values that mapped to the removed member.",
                suggestedAction: "Mark the enum member [Obsolete] instead of removing it, or add a database migration to remap stored values.",
                confidence: Confidence.Medium));
        }
    }

    private static bool IsEnumMember(string content)
    {
        // Statements end with ';': enum members never do (they end with ',' or nothing).
        if (content.TrimEnd().EndsWith(';')) return false;
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

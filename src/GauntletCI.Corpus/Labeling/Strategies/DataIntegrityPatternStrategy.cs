// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Corpus.Models;

namespace GauntletCI.Corpus.Labeling.Strategies;

/// <summary>
/// Inference strategy for data integrity and schema changes:
/// GCI0003 - API signature changes in production
/// GCI0021 - Serialization attribute removal, EF migration schema changes
/// GCI0024 - Resource disposal (using statements, Dispose calls, finalizers)
/// </summary>
public sealed class DataIntegrityPatternStrategy : IInferenceStrategy
{
    /// <summary>
    /// Applies GCI0003, GCI0021, GCI0024 heuristics.
    /// </summary>
    public IReadOnlyList<ExpectedFinding> Apply(string fixtureId, DiffAnalysisContext context)
    {
        var labels = new List<ExpectedFinding>();

        // GCI0003 -- Non-private method signature changed in production code
        // Fire when production .cs removes AND re-adds a public/protected/internal member
        // with a parenthesized signature: the rule's primary detection path.
        {
            static bool IsSigLine(string l)
            {
                var t = l.TrimStart();
                return (t.StartsWith("public ",    StringComparison.Ordinal) ||
                        t.StartsWith("protected ", StringComparison.Ordinal) ||
                        t.StartsWith("internal ",  StringComparison.Ordinal)) && t.Contains('(');
            }
            if (context.ProductionRemovedLines.Any(IsSigLine) && context.ProductionAddedLines.Any(IsSigLine))
            {
                labels.Add(new ExpectedFinding
                {
                    RuleId = "GCI0003",
                    ShouldTrigger = true,
                    ExpectedConfidence = 0.60,
                    Reason = "Diff removes and re-adds a non-private method signature in production code",
                    LabelSource = LabelSource.Heuristic,
                    IsInconclusive = false,
                });
            }
        }

        // GCI0021 -- Serialization attribute removed from production CS, or EF migration schema operation removed
        bool hasRemovedSerializationAttr = context.ProductionRemovedLines.Any(l =>
        {
            var t = l.TrimStart();
            return t.StartsWith("[JsonProperty",    StringComparison.OrdinalIgnoreCase) ||
                   t.StartsWith("[JsonIgnore",      StringComparison.OrdinalIgnoreCase) ||
                   t.StartsWith("[XmlElement",      StringComparison.OrdinalIgnoreCase) ||
                   t.StartsWith("[DataMember",      StringComparison.OrdinalIgnoreCase) ||
                   t.StartsWith("[ProtoMember",     StringComparison.OrdinalIgnoreCase) ||
                   t.StartsWith("[Column",          StringComparison.OrdinalIgnoreCase);
        });

        // EF migration: check if removed lines from migration files contain actual schema operations
        bool hasMigrationModified = IsMigrationFileModified(context.PathLines, context.ProductionRemovedLines);

        if (hasRemovedSerializationAttr || hasMigrationModified)
        {
            labels.Add(new ExpectedFinding
            {
                RuleId = "GCI0021",
                ShouldTrigger = true,
                ExpectedConfidence = 0.65,
                Reason = hasRemovedSerializationAttr ? "Diff removes a serialization attribute from production code"
                       : "Diff removes EF Core migration schema operations",
                LabelSource = LabelSource.Heuristic,
                IsInconclusive = false,
            });
        }

        // GCI0024 -- Resource cleanup / disposal patterns
        bool hasRemovedUsing = context.ProductionRemovedLines.Any(l =>
            l.Contains("using (", StringComparison.Ordinal) ||
            l.Contains("using var", StringComparison.Ordinal));

        bool hasRemovedDispose = context.ProductionRemovedLines.Any(l =>
            l.Contains(".Dispose()", StringComparison.Ordinal) ||
            l.Contains("?.Dispose()", StringComparison.Ordinal));

        bool hasRemovedFinalizer = context.ProductionRemovedLines.Any(l =>
        {
            var t = l.TrimStart();
            return t.StartsWith("~", StringComparison.Ordinal) && t.Contains("()");
        });

        if (hasRemovedUsing || hasRemovedDispose || hasRemovedFinalizer)
        {
            labels.Add(new ExpectedFinding
            {
                RuleId = "GCI0024",
                ShouldTrigger = true,
                ExpectedConfidence = 0.65,
                Reason = "Diff removes resource cleanup code (using statement, Dispose call, or finalizer)",
                LabelSource = LabelSource.Heuristic,
                IsInconclusive = false,
            });
        }

        return labels;
    }

    /// <summary>
    /// Check if the diff modified an EF Core migration file and removed schema operations.
    /// </summary>
    private static bool IsMigrationFileModified(IReadOnlyList<string> pathLines, IReadOnlyList<string> removedLines)
    {
        // Check if any path line indicates an EF migration file
        bool isMigrationFile = pathLines.Any(l =>
            l.StartsWith("--- a/", StringComparison.Ordinal) &&
            IsEfMigrationCsFile(l[6..].TrimEnd('\r')));

        if (!isMigrationFile)
            return false;

        // Require that removed lines from migration files contain actual EF schema operations
        return removedLines.Any(l =>
            l.Contains("migrationBuilder.Drop",  StringComparison.OrdinalIgnoreCase) ||
            l.Contains("migrationBuilder.Alter", StringComparison.OrdinalIgnoreCase) ||
            l.Contains("migrationBuilder.Rename", StringComparison.OrdinalIgnoreCase) ||
            l.Contains("migrationBuilder.Create", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Check if a file path is an EF Core migration file.
    /// Migration files follow the pattern: Migrations/YYYYMMDDHHmmss_Description.cs
    /// </summary>
    private static bool IsEfMigrationCsFile(string filePath)
    {
        if (!filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            return false;

        var pathNormalized = filePath.Replace('\\', '/');
        
        // Check for Migrations directory
        if (!pathNormalized.Contains("/Migrations/", StringComparison.OrdinalIgnoreCase) &&
            !pathNormalized.Contains("\\Migrations\\", StringComparison.OrdinalIgnoreCase))
            return false;

        // Migration files have timestamp prefix: YYYYMMDDHHMMSS_Name.cs
        var fileName = pathNormalized.Contains('/')
            ? pathNormalized[(pathNormalized.LastIndexOf('/') + 1)..]
            : pathNormalized[(pathNormalized.LastIndexOf('\\') + 1)..];

        // Check timestamp pattern: 14 digits followed by underscore
        return fileName.Length > 15 &&
               fileName[0..14].All(char.IsDigit) &&
               fileName[14] == '_';
    }
}

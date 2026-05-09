// SPDX-License-Identifier: Elastic-2.0
using System.Text.RegularExpressions;
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0051, Numeric Coercion Risks
/// Detects implicit numeric conversions that risk truncation, overflow, or loss of precision.
/// Flags: (int)longValue without checked{}, unchecked downcasts, float->int precision loss.
/// </summary>
public class GCI0051_NumericCoercionRisks : RuleBase
{
    public GCI0051_NumericCoercionRisks(IPatternProvider patterns) : base(patterns)
    {
    }
    public override string Id => "GCI0051";
    public override string Name => "Numeric Coercion Risks";

    private static readonly Regex ExplicitCastRegex =
        new(@"\(\s*(int|byte|short|long|uint|ushort|ulong)\s*\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex FloatToIntRegex =
        new(@"\(\s*int\s*\)\s*\w+(?:\.\w+)*\s*(?://|$|\))", RegexOptions.Compiled);

    private static readonly Regex ImplicitDowncastRegex =
        new(@"=\s*\w+(?:\.\w+)*\s*(?://|$|\);)", RegexOptions.Compiled);

    private static readonly string[] LargeIntTypes = ["long", "ulong", "Int64", "UInt64"];
    private static readonly string[] SmallIntTypes = ["int", "uint", "byte", "short", "ushort"];
    private static readonly string[] FloatTypes = ["float", "double", "decimal"];

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var findings = new List<Finding>();

        foreach (var file in context.Diff.Files)
        {
            if (WellKnownPatterns.IsTestFile(file.NewPath)) continue;

            CheckExplicitCasts(file, findings);
            CheckUncheckedNumericAssignments(file, findings);
        }

        return Task.FromResult(findings);
    }

    private void CheckExplicitCasts(DiffFile file, List<Finding> findings)
    {
        foreach (var line in file.AddedLines)
        {
            var content = line.Content;
            if (content.TrimStart().StartsWith("//")) continue;

            // Look for explicit casts like (int)longValue
            var matches = ExplicitCastRegex.Matches(content);
            foreach (Match match in matches)
            {
                var castType = match.Groups[1].Value.ToLower();
                
                // Check if preceded by a large type that could overflow
                bool isPrecisionRisk = false;
                var beforeCast = content[..match.Index];

                // Heuristic: if there's a long/double/decimal nearby, it's probably a downcast
                foreach (var largeType in LargeIntTypes.Concat(FloatTypes))
                {
                    if (beforeCast.Contains(largeType, StringComparison.OrdinalIgnoreCase))
                    {
                        isPrecisionRisk = true;
                        break;
                    }
                }

                if (!isPrecisionRisk) continue;

                // Check if cast is wrapped in checked{}
                if (content.Contains("checked", StringComparison.OrdinalIgnoreCase))
                    continue;

                findings.Add(CreateFinding(
                    file,
                    summary: $"Unchecked explicit cast to {castType}: potential truncation or overflow.",
                    evidence: $"Line {line.LineNumber}: {content.Trim()}",
                    whyItMatters: "Explicit numeric casts can silently overflow or truncate data. Without checked{}, large values wrap around or lose precision.",
                    suggestedAction: "Wrap in checked{} to throw on overflow, or validate the source value is within range before casting.",
                    confidence: Confidence.Medium,
                    line: line));
            }
        }
    }

    private void CheckUncheckedNumericAssignments(DiffFile file, List<Finding> findings)
    {
        foreach (var line in file.AddedLines)
        {
            var content = line.Content;
            if (content.TrimStart().StartsWith("//") || !content.Contains("=")) continue;

            // Simple heuristic: look for assignments from large int to small int
            // Pattern: smallIntVar = largeIntVar or function() without explicit check
            foreach (var smallType in SmallIntTypes)
            {
                // Match: var/int x = largeValue
                if (Regex.IsMatch(content, $@"var\s+\w+\s*=\s*\w+", RegexOptions.IgnoreCase))
                {
                    // Check if LHS suggests small type and RHS suggests large type
                    var parts = content.Split('=');
                    if (parts.Length != 2) continue;

                    var lhs = parts[0];
                    var rhs = parts[1];

                    bool lhsSmallHint = lhs.Contains("short") || lhs.Contains("byte") || 
                                       lhs.Contains("capacity") || lhs.Contains("count");
                    bool rhsLargeHint = rhs.Contains("long") || rhs.Contains(".Length") || 
                                       rhs.Contains(".Count");

                    if (lhsSmallHint && rhsLargeHint && !content.Contains("checked"))
                    {
                        findings.Add(CreateFinding(
                            file,
                            summary: "Potential numeric truncation: assigning large value to small type.",
                            evidence: $"Line {line.LineNumber}: {content.Trim()}",
                            whyItMatters: "Assigning from long/.Length/.Count to int/short can silently truncate large values.",
                            suggestedAction: "Validate the source value fits in the target type, or use checked{} to throw on overflow.",
                            confidence: Confidence.Low,
                            line: line));
                    }
                }
            }
        }
    }
}


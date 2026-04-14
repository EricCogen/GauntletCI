// SPDX-License-Identifier: Elastic-2.0
using System.Text.RegularExpressions;
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0026 – Documentation Adequacy
/// Detects added public methods and interfaces in .cs files without XML doc comments.
/// Complements GCI0013 (which checks public classes).
/// </summary>
public class GCI0026_DocumentationAdequacy : RuleBase
{
    public override string Id => "GCI0026";
    public override string Name => "Documentation Adequacy";

    // Matches: public [modifiers] [ReturnType] MethodName(
    // where ReturnType is any word (possibly generic like Task<T>).
    // Constructors are excluded naturally: they have no return type, so the
    // pattern requires at least two distinct tokens (return type + method name).
    private static readonly Regex PublicMethodRegex = new(
        @"^\s*public\s+(?:(?:static|async|virtual|override|abstract|sealed|new|partial)\s+)*[\w<>\[\],\s?]+\s+\w+\s*[(<]",
        RegexOptions.Compiled);

    // Attributes that identify test methods — no XML docs required.
    private static readonly Regex TestAttributeRegex = new(
        @"^\[(Fact|Theory|Test|TestCase|TestMethod|DataTestMethod|DataRow|InlineData|MemberData|ClassData)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var diff = context.Diff;
        var findings = new List<Finding>();

        foreach (var file in diff.Files)
        {
            if (IsTestFile(file.NewPath)) continue;
            CheckPublicMembersWithoutDocs(file, findings);
        }

        return Task.FromResult(findings);
    }

    private void CheckPublicMembersWithoutDocs(DiffFile file, List<Finding> findings)
    {
        var allLines = file.Hunks.SelectMany(h => h.Lines).ToList();

        for (int i = 0; i < allLines.Count; i++)
        {
            var line = allLines[i];
            if (line.Kind != DiffLineKind.Added) continue;

            var content = line.Content.Trim();
            if (content.StartsWith("//")) continue;

            if (!PublicMethodRegex.IsMatch(line.Content)) continue;

            // Check preceding lines for XML doc comment or a test attribute
            bool hasDocs = false;
            bool isTestMethod = false;
            for (int j = i - 1; j >= Math.Max(0, i - 5); j--)
            {
                var prev = allLines[j].Content.Trim();
                if (string.IsNullOrWhiteSpace(prev)) break;
                if (prev.StartsWith("///")) { hasDocs = true; break; }
                if (prev.StartsWith("["))
                {
                    if (TestAttributeRegex.IsMatch(prev)) { isTestMethod = true; break; }
                    continue; // other attributes ([HttpGet] etc.) — keep looking
                }
                break; // non-attribute, non-doc, non-blank — stop
            }

            if (!hasDocs && !isTestMethod)
            {
                var methodName = ExtractMethodName(content);
                findings.Add(CreateFinding(
                    file,
                    summary: $"Public method '{methodName}' added without XML documentation in {file.NewPath}.",
                    evidence: $"Line {line.LineNumber}: {content}",
                    whyItMatters: "Public API methods without XML docs leave callers guessing about behaviour, parameters, and edge cases — especially important for shared libraries and services.",
                    suggestedAction: "Add a /// <summary> block above the method describing what it does, its parameters, and return value.",
                    confidence: Confidence.Low,
                    line: line));
            }
        }
    }

    /// <summary>Heuristic: skip files that are clearly test files by path convention.</summary>
    private static bool IsTestFile(string? path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        var name = Path.GetFileNameWithoutExtension(path);
        return name.EndsWith("Tests", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Test", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Specs", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Spec", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/Tests/", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/Test/", StringComparison.OrdinalIgnoreCase)
            || path.Contains(".Tests/", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractMethodName(string signature)
    {
        var parenIdx = signature.IndexOf('(');
        if (parenIdx <= 0) return signature;
        var beforeParen = signature[..parenIdx].Trim();
        var parts = beforeParen.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[^1] : beforeParen;
    }
}


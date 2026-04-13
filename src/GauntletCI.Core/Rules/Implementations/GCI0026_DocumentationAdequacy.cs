// SPDX-License-Identifier: Elastic-2.0
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

    private static readonly string[] PublicMethodReturnTypes =
    [
        "public Task ", "public async Task ", "public ValueTask ",
        "public async ValueTask ", "public static Task ", "public static async Task ",
        "public static ValueTask ", "public override Task ", "public virtual Task ",
        "public string ", "public bool ", "public int ", "public long ",
        "public void ", "public static void ", "public static string ",
        "public static bool ", "public static int ", "public override string ",
        "public override bool ", "public override void ", "public override int ",
        "public IEnumerable", "public IList", "public IReadOnly",
        "public List<", "public Dictionary<"
    ];

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var diff = context.Diff;
        var findings = new List<Finding>();

        foreach (var file in diff.Files)
        {
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

            // Must look like a public method signature (has opening paren)
            bool isPublicMethod = PublicMethodReturnTypes.Any(r => content.Contains(r)) &&
                                   content.Contains('(');
            if (!isPublicMethod) continue;

            // Check if there's a /// XML doc comment in the preceding lines
            bool hasDocs = false;
            for (int j = i - 1; j >= Math.Max(0, i - 5); j--)
            {
                var prev = allLines[j].Content.Trim();
                if (string.IsNullOrWhiteSpace(prev)) break;
                if (prev.StartsWith("///"))
                { hasDocs = true; break; }
                // An attribute line is ok to skip ([HttpGet], etc.)
                if (prev.StartsWith("[")) continue;
                // Any non-attribute non-doc non-blank line means no doc above
                break;
            }

            if (!hasDocs)
            {
                // Extract method name for a cleaner message
                var methodName = ExtractMethodName(content);
                findings.Add(CreateFinding(
                    summary: $"Public method '{methodName}' added without XML documentation in {file.NewPath}.",
                    evidence: $"Line {line.LineNumber}: {content}",
                    whyItMatters: "Public API methods without XML docs leave callers guessing about behaviour, parameters, and edge cases — especially important for shared libraries and services.",
                    suggestedAction: "Add a /// <summary> block above the method describing what it does, its parameters, and return value.",
                    confidence: Confidence.Low));
            }
        }
    }

    private static string ExtractMethodName(string signature)
    {
        // Extract method name from "public Task<T> MethodName(..."
        var parenIdx = signature.IndexOf('(');
        if (parenIdx <= 0) return signature;
        var beforeParen = signature[..parenIdx].Trim();
        var parts = beforeParen.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[^1] : beforeParen;
    }
}

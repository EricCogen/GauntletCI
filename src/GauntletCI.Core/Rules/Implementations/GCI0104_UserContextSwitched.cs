// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0104 – User Context Switched
/// Detects when a USER directive changes in a Dockerfile.
/// Changing user context can escalate privileges or break file permission assumptions.
/// Switching to root is Block-severity; all other user switches are Warn.
/// </summary>
public class GCI0104_UserContextSwitched : RuleBase
{
    public override string Id   => "GCI0104";
    public override string Name => "User Context Switched";

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var findings = new List<Finding>();

        foreach (var file in context.AllDiffFiles)
        {
            if (!IsDockerfile(file.NewPath)) continue;

            var removedUser = file.RemovedLines
                .Where(l => l.Content.TrimStart().StartsWith("USER ", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var addedUser = file.AddedLines
                .Where(l => l.Content.TrimStart().StartsWith("USER ", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (removedUser.Count == 0 || addedUser.Count == 0) continue;

            var oldUser = ExtractUser(removedUser[0].Content);
            var newUser = ExtractUser(addedUser[0].Content);

            if (oldUser is null || newUser is null || oldUser == newUser) continue;

            var switchingToRoot = newUser.Equals("root", StringComparison.OrdinalIgnoreCase)
                               || newUser == "0";

            findings.Add(CreateFinding(
                file,
                summary: switchingToRoot
                    ? $"User context switched TO root in {file.NewPath}"
                    : $"User context switched in {file.NewPath}",
                evidence: $"Was: {removedUser[0].Content.Trim()} | Now: {addedUser[0].Content.Trim()}",
                whyItMatters: switchingToRoot
                    ? "Switching to root escalates container privileges and violates the principle of least privilege."
                    : "Changing user context may break file permission assumptions or alter runtime behavior.",
                suggestedAction: switchingToRoot
                    ? "Avoid running as root. Use a dedicated non-root user with only the permissions required."
                    : "Verify file ownership and permissions are compatible with the new user context.",
                confidence: Confidence.High,
                line: addedUser[0]));
        }

        return Task.FromResult(findings);
    }

    private static string? ExtractUser(string line) =>
        line.TrimStart().Split(' ', StringSplitOptions.RemoveEmptyEntries) is { Length: >= 2 } parts
            ? parts[1].Trim()
            : null;

    private static bool IsDockerfile(string? path)
    {
        if (path is null) return false;
        var name = Path.GetFileName(path);
        return name.Equals("Dockerfile", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".dockerfile", StringComparison.OrdinalIgnoreCase)
            || path.Contains("dockerfile", StringComparison.OrdinalIgnoreCase);
    }
}

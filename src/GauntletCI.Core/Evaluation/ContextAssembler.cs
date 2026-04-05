using GauntletCI.Core.Models;

namespace GauntletCI.Core.Evaluation;

public sealed class ContextAssembler
{
    public (string Context, bool DiffTrimmed) Assemble(
        GateResult branchGate,
        GateResult testGate,
        string diff,
        GauntletConfig config,
        IReadOnlyList<string> recentCommits,
        int maxDiffLines = 400)
    {
        string[] diffLines = diff.Split('\n');
        bool trimmed = diffLines.Length > maxDiffLines;
        string trimmedDiff = trimmed ? TrimAtHunkBoundaries(diffLines, maxDiffLines) : diff;

        List<string> lines =
        [
            "Pre-flight gate results:",
            $"- Branch currency: {(branchGate.Passed ? "pass" : "fail")} ({branchGate.Summary})",
            $"- Test passage: {(testGate.Passed ? "pass" : "fail")} ({testGate.Summary})",
            string.Empty,
            "Repo config:",
            $"- model: {config.Model}",
            $"- telemetry: {config.Telemetry}",
            $"- test_command: {config.TestCommand}",
            $"- blocking_rules: {(config.BlockingRules.Count == 0 ? "[]" : string.Join(',', config.BlockingRules))}",
            string.Empty,
            "Recent commits:",
        ];

        if (recentCommits.Count == 0)
        {
            lines.Add("- none");
        }
        else
        {
            foreach (string commit in recentCommits)
            {
                lines.Add($"- {commit}");
            }
        }

        lines.Add(string.Empty);
        if (trimmed)
        {
            lines.Add($"[Note: diff trimmed from {diffLines.Length} to approximately {maxDiffLines} lines. Some context omitted. State uncertainty where relevant.]");
        }

        lines.Add("Diff:");
        lines.Add(trimmedDiff);

        return (string.Join(Environment.NewLine, lines), trimmed);
    }

    private static string TrimAtHunkBoundaries(IReadOnlyList<string> lines, int maxLines)
    {
        List<string> output = [];
        int count = 0;
        List<string> pendingHunk = [];

        foreach (string line in lines)
        {
            string normalized = line.TrimEnd('\r');
            if (normalized.StartsWith("@@", StringComparison.Ordinal))
            {
                if (pendingHunk.Count > 0)
                {
                    if (count + pendingHunk.Count > maxLines)
                    {
                        break;
                    }

                    output.AddRange(pendingHunk);
                    count += pendingHunk.Count;
                    pendingHunk.Clear();
                }
            }

            pendingHunk.Add(normalized);
        }

        if (pendingHunk.Count > 0 && count + pendingHunk.Count <= maxLines)
        {
            output.AddRange(pendingHunk);
        }

        return string.Join(Environment.NewLine, output);
    }
}

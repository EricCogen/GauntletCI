// SPDX-License-Identifier: Elastic-2.0
using System.Text.RegularExpressions;
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0057, Synchronous File I/O in Production Code
/// Detects synchronous File.* calls that block threads; async variants should be preferred.
/// Blocking async patterns (.Result, .Wait) are handled by GCI0016 and disabled here to avoid duplicate findings.
/// Default severity: Warn. Disabled when superseded — use GCI0016 for blocking-async detection.
/// </summary>
public class GCI0057_BlockingAsyncViolation : RuleBase
{
    public GCI0057_BlockingAsyncViolation(IPatternProvider patterns) : base(patterns)
    {
    }

    public override string Id => "GCI0057";
    public override string Name => "Synchronous File I/O";

    private static readonly Regex SyncFileIoPattern =
        new(@"\bFile\.(ReadAllText|ReadAllLines|WriteAllText|WriteAllLines|Copy|ReadAllBytes|WriteAllBytes)\s*\(",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex AsyncMethodPattern =
        new(@"\basync\s+(?:Task|void)", RegexOptions.Compiled);

    private static readonly string[] SyncFileIoExemptFiles =
    [
        "Program.cs", "Startup.cs", "AssemblyInfo.cs"
    ];

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var findings = new List<Finding>();
        var diff = context.Diff;

        foreach (var file in diff.Files)
        {
            if (WellKnownPatterns.IsTestFile(file.NewPath))
                continue;

            CheckSyncFileIo(file, findings);
        }

        return Task.FromResult(findings);
    }

    private void CheckSyncFileIo(DiffFile file, List<Finding> findings)
    {
        var fileName = Path.GetFileName(file.NewPath);
        if (SyncFileIoExemptFiles.Any(f => f.Equals(fileName, StringComparison.OrdinalIgnoreCase)))
            return;

        var allLines = file.Hunks.SelectMany(h => h.Lines).ToList();

        foreach (var line in file.AddedLines)
        {
            if (!SyncFileIoPattern.IsMatch(line.Content))
                continue;

            if (IsInStringOrComment(line.Content))
                continue;

            var match = SyncFileIoPattern.Match(line.Content);
            if (!match.Success || match.Groups.Count < 2)
                continue;

            var method = match.Groups[1].Value;
            var confidence = DetermineFileIoConfidence(allLines, line);

            var suggestion = method.Equals("Copy", StringComparison.OrdinalIgnoreCase)
                ? "For large files, consider using Stream.CopyToAsync() instead of File.Copy()."
                : $"Use await File.{method}Async(...) instead.";

            findings.Add(CreateFinding(
                file,
                summary: $"Synchronous file I/O: {method}() blocks the current thread",
                evidence: $"Line {line.LineNumber}: {line.Content.Trim()}",
                whyItMatters: $"File.{method}() is synchronous and blocks the current thread. Use the async variant to avoid blocking I/O operations.",
                suggestedAction: suggestion,
                confidence: confidence,
                line: line));
        }
    }

    private static Confidence DetermineFileIoConfidence(List<DiffLine> allLines, DiffLine targetLine)
    {
        var lineIndex = -1;
        for (int idx = 0; idx < allLines.Count; idx++)
        {
            if (allLines[idx].LineNumber == targetLine.LineNumber && allLines[idx].Content == targetLine.Content)
            {
                lineIndex = idx;
                break;
            }
        }

        if (lineIndex < 0)
            return Confidence.Medium;

        for (int i = lineIndex; i >= 0 && i >= lineIndex - 30; i--)
        {
            var prevLine = allLines[i].Content;
            if (AsyncMethodPattern.IsMatch(prevLine))
                return Confidence.High;
            if (prevLine.Contains("public") || prevLine.Contains("private"))
                return Confidence.Medium;
        }

        return Confidence.Medium;
    }

    private static bool IsInStringOrComment(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.StartsWith("//") || trimmed.StartsWith("/*") || trimmed.StartsWith("*"))
            return true;

        return false;
    }
}

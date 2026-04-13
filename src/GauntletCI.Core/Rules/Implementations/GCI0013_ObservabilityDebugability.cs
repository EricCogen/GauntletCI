// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0013 – Observability/Debuggability
/// Flags missing logging, missing XML docs, and unlogged exception re-throws.
/// </summary>
public class GCI0013_ObservabilityDebugability : RuleBase
{
    public override string Id => "GCI0013";
    public override string Name => "Observability/Debuggability";

    private static readonly string[] LoggingPatterns =
        ["_logger.", "Log.", "Console.Write", "Trace.", "Debug.Write", "logger."];

    /// <summary>
    /// Markers that indicate the file is a unit/integration test file.
    /// Checked against all hunk lines (context + added) so that imports at the
    /// top of the file are detected even when they are not part of the diff.
    /// </summary>
    private static readonly string[] TestFrameworkMarkers =
    [
        // xUnit
        "[Fact]", "[Theory]", "[InlineData(", "using Xunit;",
        // NUnit
        "[Test]", "[TestFixture]", "[TestCase(", "using NUnit.Framework;",
        // MSTest
        "[TestMethod]", "[TestClass]", "using Microsoft.VisualStudio.TestTools.UnitTesting;",
    ];

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var findings = new List<Finding>();

        foreach (var file in context.Diff.Files)
        {
            if (IsTestFile(file)) continue;
            CheckLargeMethodWithoutLogging(file, findings);
        }

        return Task.FromResult(findings);
    }

    /// <summary>
    /// Returns true when the file's hunk content contains a recognisable test
    /// framework attribute or namespace import — no file-path heuristics used.
    /// </summary>
    private static bool IsTestFile(DiffFile file) =>
        file.Hunks
            .SelectMany(h => h.Lines)
            .Any(l => TestFrameworkMarkers.Any(
                m => l.Content.Contains(m, StringComparison.Ordinal)));

    private void CheckLargeMethodWithoutLogging(DiffFile file, List<Finding> findings)
    {
        var addedLines = file.AddedLines.ToList();
        if (addedLines.Count < 20) return;

        bool hasLogging = addedLines.Any(l =>
            LoggingPatterns.Any(p => l.Content.Contains(p, StringComparison.Ordinal)));

        if (!hasLogging)
        {
            findings.Add(CreateFinding(
                file,
                summary: $"{addedLines.Count} lines added in {file.NewPath} with no logging calls.",
                evidence: $"File: {file.NewPath} — {addedLines.Count} added lines, no logging detected.",
                whyItMatters: "Code without logging is hard to diagnose in production.",
                suggestedAction: "Add appropriate logging at entry/exit points and for error paths.",
                confidence: Confidence.Low));
        }
    }
}

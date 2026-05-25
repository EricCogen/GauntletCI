// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Core.Tests.Rules;

public class GCI0057_BlockingAsyncViolationTests
{
    private readonly GCI0057_BlockingAsyncViolation _rule = new(new DefaultPatternProvider());

    private static DiffContext CreateDiff(string filePath, params string[] addedLines)
    {
        var lines = addedLines
            .Select((content, idx) => new DiffLine
            {
                Kind = DiffLineKind.Added,
                LineNumber = idx + 1,
                Content = content
            })
            .ToList();

        var hunk = new DiffHunk
        {
            OldStartLine = 1,
            NewStartLine = 1,
            Lines = lines
        };

        var file = new DiffFile
        {
            NewPath = filePath,
            OldPath = string.Empty,
            IsAdded = true,
            Hunks = new List<DiffHunk> { hunk }
        };

        return new DiffContext { Files = new List<DiffFile> { file } };
    }

    private static AnalysisContext CreateContext(DiffContext diff) => new()
    {
        EligibleFiles = [],
        SkippedFiles = [],
        Diff = diff
    };

    [Fact]
    public async Task NoFinding_WhenBlockingAsyncOnly_GCI0016OwnsThatPattern()
    {
        var diff = CreateDiff("src/Service.cs", "var result = GetDataAsync().Result;");
        var findings = await _rule.EvaluateAsync(CreateContext(diff));
        Assert.Empty(findings);
    }

    [Fact]
    public async Task Finding_WhenUsingSyncFileRead()
    {
        var diff = CreateDiff("src/Service.cs", "var text = File.ReadAllText(path);");
        var findings = await _rule.EvaluateAsync(CreateContext(diff));
        Assert.NotEmpty(findings);
        Assert.Equal("GCI0057", findings[0].RuleId);
    }

    [Fact]
    public async Task NoFinding_WhenInProgramCs()
    {
        var diff = CreateDiff("Program.cs", "var text = File.ReadAllText(path);");
        var findings = await _rule.EvaluateAsync(CreateContext(diff));
        Assert.Empty(findings);
    }

    [Fact]
    public async Task NoFinding_WhenInTestFile()
    {
        var diff = CreateDiff("tests/ServiceTests.cs", "var text = File.ReadAllText(path);");
        var findings = await _rule.EvaluateAsync(CreateContext(diff));
        Assert.Empty(findings);
    }

    [Fact]
    public async Task Finding_WhenUsingSyncFileWrite()
    {
        var diff = CreateDiff("src/Service.cs", "File.WriteAllText(path, content);");
        var findings = await _rule.EvaluateAsync(CreateContext(diff));
        Assert.NotEmpty(findings);
    }

    [Fact]
    public async Task NoFinding_WhenUsingAsyncFileRead()
    {
        var diff = CreateDiff("src/Service.cs", "var text = await File.ReadAllTextAsync(path);");
        var findings = await _rule.EvaluateAsync(CreateContext(diff));
        Assert.Empty(findings);
    }
}

// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Core.Tests.Rules;

public class GCI0057_BlockingAsyncViolationTests
{
    private GCI0057_BlockingAsyncViolation _rule;

    public GCI0057_BlockingAsyncViolationTests()
    {
        _rule = new GCI0057_BlockingAsyncViolation(new DefaultPatternProvider());
    }

    private DiffContext CreateDiff(string filePath, params string[] addedLines)
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

    private AnalysisContext CreateContext(DiffContext diff)
    {
        return new AnalysisContext
        {
            EligibleFiles = [],
            SkippedFiles = [],
            Diff = diff
        };
    }

    [Fact]
    public async Task Finding_WhenUsingResult()
    {
        var diff = CreateDiff("src/Service.cs", "var result = GetDataAsync().Result;");
        var context = CreateContext(diff);

        var findings = await _rule.EvaluateAsync(context);

        Assert.NotEmpty(findings);
        var finding = findings.First();
        Assert.Equal("GCI0057", finding.RuleId);
        Assert.Equal(Confidence.High, finding.Confidence);
    }

    [Fact]
    public async Task Finding_WhenUsingWait()
    {
        var diff = CreateDiff("src/Service.cs", "GetDataAsync().Wait();");
        var context = CreateContext(diff);

        var findings = await _rule.EvaluateAsync(context);

        Assert.NotEmpty(findings);
        var finding = findings.First();
        Assert.Equal(Confidence.High, finding.Confidence);
    }

    [Fact]
    public async Task Finding_WhenUsingGetAwaiter()
    {
        var diff = CreateDiff("src/Service.cs", "var x = GetDataAsync().GetAwaiter().GetResult();");
        var context = CreateContext(diff);

        var findings = await _rule.EvaluateAsync(context);

        Assert.NotEmpty(findings);
    }

    [Fact]
    public async Task NoFinding_WhenUsingAwait()
    {
        var diff = CreateDiff("src/Service.cs", "var result = await GetDataAsync();");
        var context = CreateContext(diff);

        var findings = await _rule.EvaluateAsync(context);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task Finding_WhenUsingSyncFileRead()
    {
        var diff = CreateDiff("src/Service.cs", "var text = File.ReadAllText(path);");
        var context = CreateContext(diff);

        var findings = await _rule.EvaluateAsync(context);

        Assert.NotEmpty(findings);
    }

    [Fact]
    public async Task NoFinding_WhenInProgramCs()
    {
        var diff = CreateDiff("Program.cs", "var text = File.ReadAllText(path);");
        var context = CreateContext(diff);

        var findings = await _rule.EvaluateAsync(context);

        // Infrastructure files like Program.cs are exempt
        Assert.Empty(findings);
    }

    [Fact]
    public async Task NoFinding_WhenInTestFile()
    {
        var diff = CreateDiff("tests/ServiceTests.cs", "var result = GetDataAsync().Result;");
        var context = CreateContext(diff);

        var findings = await _rule.EvaluateAsync(context);

        // Test files are exempt
        Assert.Empty(findings);
    }

    [Fact]
    public async Task Finding_WhenUsingSyncFileWrite()
    {
        var diff = CreateDiff("src/Service.cs", "File.WriteAllText(path, content);");
        var context = CreateContext(diff);

        var findings = await _rule.EvaluateAsync(context);

        Assert.NotEmpty(findings);
    }

    [Fact]
    public async Task NoFinding_WhenUsingAsyncFileRead()
    {
        var diff = CreateDiff("src/Service.cs", "var text = await File.ReadAllTextAsync(path);");
        var context = CreateContext(diff);

        var findings = await _rule.EvaluateAsync(context);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task MultipleFindings_WhenMultipleViolations()
    {
        var diff = CreateDiff("src/Service.cs",
            "var result = GetDataAsync().Result;",
            "var text = File.ReadAllText(path);");
        var context = CreateContext(diff);

        var findings = await _rule.EvaluateAsync(context);

        Assert.True(findings.Count >= 2);
    }
}


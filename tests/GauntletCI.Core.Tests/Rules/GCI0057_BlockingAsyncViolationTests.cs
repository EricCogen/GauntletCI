// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules;
using GauntletCI.Core.Rules.Implementations;
using GauntletCI.Core.StaticAnalysis;

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

    private static AnalysisContext CreateContext(DiffContext diff, SyntaxContext? syntax = null) => new()
    {
        EligibleFiles = [],
        SkippedFiles = [],
        Diff = diff,
        Syntax = syntax,
    };

    private static SyntaxContext CreateSyntaxContext(string filePath, string sourceLine, int lineNumber)
    {
        var lines = Enumerable.Repeat("// existing", lineNumber - 1).Append(sourceLine);
        var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(
            Microsoft.CodeAnalysis.Text.SourceText.From(string.Join('\n', lines)));
        return new SyntaxContext(new Dictionary<string, Microsoft.CodeAnalysis.SyntaxTree>
        {
            [filePath] = tree
        });
    }

    private static DiffContext CreateDiffAtLine(string filePath, int lineNumber, string content)
    {
        var line = new DiffLine
        {
            Kind = DiffLineKind.Added,
            LineNumber = lineNumber,
            Content = content
        };

        var hunk = new DiffHunk
        {
            OldStartLine = 1,
            NewStartLine = 1,
            Lines = new List<DiffLine> { line }
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

    [Fact]
    public async Task NoFinding_WhenMatchIsInsideStringLiteral_WithSyntaxTree()
    {
        const string line = "var hint = \"File.ReadAllText(path)\";";
        const int lineNumber = 2;
        var diff = CreateDiffAtLine("src/Service.cs", lineNumber, line);
        var findings = await _rule.EvaluateAsync(
            CreateContext(diff, CreateSyntaxContext("src/Service.cs", line, lineNumber)));
        Assert.Empty(findings);
    }

    [Fact]
    public async Task NoFinding_WhenShadowFileType_WithSyntaxTree()
    {
        const string line = "    public void Load() => File.ReadAllText(\"x\");";
        const int lineNumber = 8;
        const string source = """
            class Store
            {
                private class File
                {
                    public static string ReadAllText(string path) => path;
                }

                public void Load() => File.ReadAllText("x");
            }
            """;
        var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(
            Microsoft.CodeAnalysis.Text.SourceText.From(source));
        var diff = CreateDiffAtLine("src/Service.cs", lineNumber, line);
        var syntax = new SyntaxContext(new Dictionary<string, Microsoft.CodeAnalysis.SyntaxTree>
        {
            ["src/Service.cs"] = tree
        });
        var findings = await _rule.EvaluateAsync(CreateContext(diff, syntax));
        Assert.Empty(findings);
    }

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

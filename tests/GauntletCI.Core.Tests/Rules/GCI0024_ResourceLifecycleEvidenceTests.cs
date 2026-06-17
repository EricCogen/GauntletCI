// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules;
using GauntletCI.Core.Rules.Implementations;
using GauntletCI.Core.StaticAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace GauntletCI.Core.Tests.Rules;

public class GCI0024_ResourceLifecycleEvidenceTests
{
    private readonly GCI0024_ResourceLifecycle _rule = new(new DefaultPatternProvider());
    private const string FilePath = "src/FileProcessor.cs";

    private static AnalysisContext CreateContext(DiffContext diff, SyntaxContext? syntax = null) => new()
    {
        EligibleFiles = [],
        SkippedFiles = [],
        Diff = diff,
        Syntax = syntax,
    };

    private static SyntaxContext CreateSyntaxContext(string filePath, string sourceLine, int lineNumber)
    {
        var lines = Enumerable.Repeat("public class FileProcessor {", lineNumber - 1).Append(sourceLine);
        var tree = CSharpSyntaxTree.ParseText(SourceText.From(string.Join('\n', lines)));
        return new SyntaxContext(new Dictionary<string, Microsoft.CodeAnalysis.SyntaxTree>
        {
            [filePath] = tree
        });
    }

    private static DiffContext CreateDiffAtLine(string filePath, int lineNumber, string content)
    {
        return new DiffContext
        {
            Files =
            [
                new DiffFile
                {
                    NewPath = filePath,
                    OldPath = string.Empty,
                    IsAdded = true,
                    Hunks =
                    [
                        new DiffHunk
                        {
                            OldStartLine = 1,
                            NewStartLine = 1,
                            Lines =
                            [
                                new DiffLine
                                {
                                    Kind = DiffLineKind.Added,
                                    LineNumber = lineNumber,
                                    Content = content
                                }
                            ]
                        }
                    ]
                }
            ]
        };
    }

    [Fact]
    public async Task FileStreamMentionInStringLiteral_WithSyntaxTree_ShouldNotFlag()
    {
        const string addedLine = "    var hint = \"use new FileStream(path)\";";
        var diff = CreateDiffAtLine(FilePath, 2, addedLine);
        var syntax = CreateSyntaxContext(FilePath, addedLine, 2);
        var findings = await _rule.EvaluateAsync(CreateContext(diff, syntax));

        Assert.DoesNotContain(findings, f => f.Summary.Contains("FileStream"));
    }

    [Fact]
    public async Task FileStreamObjectCreation_WithSyntaxTree_ShouldFlag()
    {
        const string addedLine = "    var stream = new FileStream(\"data.bin\", FileMode.Open);";
        var diff = CreateDiffAtLine(FilePath, 2, addedLine);
        var syntax = CreateSyntaxContext(FilePath, addedLine, 2);
        var findings = await _rule.EvaluateAsync(CreateContext(diff, syntax));

        Assert.Contains(findings, f => f.Summary.Contains("FileStream"));
    }
}

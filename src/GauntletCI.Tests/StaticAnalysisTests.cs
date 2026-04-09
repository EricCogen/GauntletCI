// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.StaticAnalysis;

namespace GauntletCI.Tests;

/// <summary>
/// Tests for DiagnosticMapper, RoslynAnalyzer, DiffParser additional methods,
/// and model properties not covered by rule-specific tests.
/// </summary>
public class StaticAnalysisTests
{
    // ── DiagnosticMapper ─────────────────────────────────────────────────────

    [Fact]
    public void MapDiagnostics_WithMatchingId_ShouldReturnFinding()
    {
        var result = new AnalyzerResult
        {
            Success = true,
            Diagnostics =
            [
                new() { Id = "CA2100", Message = "SQL injection risk", FilePath = "src/Repo.cs", Line = 10, Column = 5 }
            ]
        };

        var findings = DiagnosticMapper.MapDiagnostics(result).ToList();

        Assert.Single(findings);
        Assert.Equal("GCI0012", findings[0].RuleId);
        Assert.Contains("CA2100", findings[0].Summary);
        Assert.Equal(Confidence.High, findings[0].Confidence);
    }

    [Fact]
    public void MapDiagnostics_WithFailedResult_ShouldReturnEmpty()
    {
        var result = new AnalyzerResult { Success = false };

        var findings = DiagnosticMapper.MapDiagnostics(result).ToList();

        Assert.Empty(findings);
    }

    [Fact]
    public void MapDiagnostics_WithUnknownDiagnosticId_ShouldSkip()
    {
        var result = new AnalyzerResult
        {
            Success = true,
            Diagnostics =
            [
                new() { Id = "UNKNOWN99", Message = "unknown", FilePath = "src/Foo.cs", Line = 1, Column = 1 }
            ]
        };

        var findings = DiagnosticMapper.MapDiagnostics(result).ToList();

        Assert.Empty(findings);
    }

    [Fact]
    public void MapDiagnostics_MultipleMappedIds_ShouldReturnMultipleFindings()
    {
        var result = new AnalyzerResult
        {
            Success = true,
            Diagnostics =
            [
                new() { Id = "CA1031", Message = "Catch specific exception", FilePath = "src/Svc.cs", Line = 5, Column = 1 },
                new() { Id = "CA1062", Message = "Validate parameter", FilePath = "src/Svc.cs", Line = 8, Column = 1 },
                new() { Id = "CA2227", Message = "Collection property", FilePath = "src/Svc.cs", Line = 12, Column = 1 },
                new() { Id = "CA1305", Message = "Specify format", FilePath = "src/Svc.cs", Line = 15, Column = 1 },
                new() { Id = "CA1819", Message = "Arrays as properties", FilePath = "src/Svc.cs", Line = 18, Column = 1 }
            ]
        };

        var findings = DiagnosticMapper.MapDiagnostics(result).ToList();

        Assert.Equal(5, findings.Count);
        Assert.Contains(findings, f => f.RuleId == "GCI0007");
        Assert.Contains(findings, f => f.RuleId == "GCI0006");
        Assert.Contains(findings, f => f.RuleId == "GCI0015");
        Assert.Contains(findings, f => f.RuleId == "GCI0009");
    }

    [Fact]
    public void MapDiagnostics_CA2153_ShouldMapToGci0012()
    {
        var result = new AnalyzerResult
        {
            Success = true,
            Diagnostics = [new() { Id = "CA2153", Message = "Avoid handling corrupted state exceptions", FilePath = "src/Foo.cs", Line = 1, Column = 1 }]
        };

        var findings = DiagnosticMapper.MapDiagnostics(result).ToList();

        Assert.Single(findings);
        Assert.Equal("GCI0012", findings[0].RuleId);
        Assert.Equal(Confidence.High, findings[0].Confidence);
    }

    // ── RoslynAnalyzer ───────────────────────────────────────────────────────

    [Fact]
    public async Task RoslynAnalyzer_AnalyzeFileAsync_ValidCSharp_ShouldSucceed()
    {
        var analyzer = new RoslynAnalyzer();
        var source = """
            using System;
            public class Foo
            {
                public void Bar(string x)
                {
                    Console.WriteLine(x);
                }
            }
            """;

        var result = await analyzer.AnalyzeFileAsync("Foo.cs", source);

        Assert.True(result.Success);
        Assert.Equal("Foo.cs", result.AnalyzedFile);
    }

    [Fact]
    public async Task RoslynAnalyzer_AnalyzeFileAsync_WithChangedLines_ShouldFilterDiagnostics()
    {
        var analyzer = new RoslynAnalyzer();
        var source = "class Foo { }";

        var result = await analyzer.AnalyzeFileAsync("test.cs", source, [1, 2, 3]);

        Assert.True(result.Success);
        Assert.NotNull(result.Diagnostics);
    }

    [Fact]
    public async Task RoslynAnalyzer_AnalyzeFileAsync_WithNullChangedLines_ShouldIncludeAll()
    {
        var analyzer = new RoslynAnalyzer();
        var source = "class Bar { }";

        var result = await analyzer.AnalyzeFileAsync("bar.cs", source, null);

        Assert.True(result.Success);
    }

    // ── DiffParser additional paths ──────────────────────────────────────────

    [Fact]
    public void DiffParser_FromFile_ShouldParseContent()
    {
        var raw = """
            diff --git a/src/Foo.cs b/src/Foo.cs
            index abc..def 100644
            --- a/src/Foo.cs
            +++ b/src/Foo.cs
            @@ -1,1 +1,2 @@
             // existing
            +int x = 1;
            """;

        var tempFile = Path.Combine(Path.GetTempPath(), $"gauntlet_test_{Guid.NewGuid():N}.diff");
        try
        {
            File.WriteAllText(tempFile, raw);
            var ctx = DiffParser.FromFile(tempFile);
            Assert.Single(ctx.Files);
            Assert.Equal("src/Foo.cs", ctx.Files[0].NewPath);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void DiffParser_Parse_WithCommitMessage_ShouldSetProperty()
    {
        var diff = DiffParser.Parse("diff --git a/src/Foo.cs b/src/Foo.cs", commitMessage: "fix bug");
        Assert.Equal("fix bug", diff.CommitMessage);
    }

    [Fact]
    public void DiffParser_Parse_DeletedFile_ShouldSetIsDeleted()
    {
        var raw = """
            diff --git a/src/Old.cs b/src/Old.cs
            deleted file mode 100644
            index abc..0000000
            --- a/src/Old.cs
            +++ /dev/null
            @@ -1,2 +0,0 @@
            -class Old { }
            -
            """;

        var ctx = DiffParser.Parse(raw);

        Assert.Single(ctx.Files);
        Assert.True(ctx.Files[0].IsDeleted);
    }

    [Fact]
    public void DiffContext_IsRenamed_ShouldBeTrue_WhenPathsDiffer()
    {
        var file = new DiffFile
        {
            OldPath = "src/OldName.cs",
            NewPath = "src/NewName.cs",
            IsAdded = false,
            IsDeleted = false
        };

        Assert.True(file.IsRenamed);
    }

    [Fact]
    public void DiffContext_IsRenamed_ShouldBeFalse_WhenPathsSame()
    {
        var file = new DiffFile
        {
            OldPath = "src/Foo.cs",
            NewPath = "src/Foo.cs",
            IsAdded = false,
            IsDeleted = false
        };

        Assert.False(file.IsRenamed);
    }

    [Fact]
    public void Finding_LlmExplanation_ShouldBeSettable()
    {
        var finding = new Finding
        {
            RuleId = "GCI0001",
            RuleName = "Test",
            Summary = "Test finding",
            Evidence = "Evidence",
            WhyItMatters = "It matters",
            SuggestedAction = "Do something",
            LlmExplanation = "LLM says this is important"
        };

        Assert.Equal("LLM says this is important", finding.LlmExplanation);
    }

    [Fact]
    public void DiffParser_Parse_NoNewlineAtEndOfFile_ShouldNotThrow()
    {
        var raw = """
            diff --git a/src/Foo.cs b/src/Foo.cs
            index abc..def 100644
            --- a/src/Foo.cs
            +++ b/src/Foo.cs
            @@ -1,1 +1,2 @@
             // existing
            +int x = 1;
            \ No newline at end of file
            """;

        var ctx = DiffParser.Parse(raw);

        Assert.Single(ctx.Files);
    }
}

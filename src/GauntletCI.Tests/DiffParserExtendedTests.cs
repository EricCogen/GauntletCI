// SPDX-License-Identifier: Elastic-2.0
using System.Diagnostics;
using GauntletCI.Core.Diff;

namespace GauntletCI.Tests;

/// <summary>
/// Extended tests for DiffParser covering file-based parsing, edge cases, and performance.
/// </summary>
public class DiffParserExtendedTests
{
    private const string SimpleDiff = """
        diff --git a/src/Foo.cs b/src/Foo.cs
        index 0000000..1111111 100644
        --- a/src/Foo.cs
        +++ b/src/Foo.cs
        @@ -1,5 +1,8 @@
         public class Foo
         {
        -    public void Bar() { }
        +    public void Bar()
        +    {
        +        Console.WriteLine("hello");
        +    }
         }
        """;

    [Fact]
    public void FromFile_ValidPatchFile_ParsesDiff()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"gci_{Guid.NewGuid():N}.patch");
        try
        {
            File.WriteAllText(tempFile, SimpleDiff);

            var ctx = DiffParser.FromFile(tempFile);

            Assert.NotEmpty(ctx.Files);
            Assert.Equal("src/Foo.cs", ctx.Files[0].NewPath);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void FromFile_NonexistentFile_Throws()
    {
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"gci_no_file_{Guid.NewGuid():N}.patch");

        Assert.ThrowsAny<Exception>(() => DiffParser.FromFile(nonExistentPath));
    }

    [Fact]
    public void Parse_EmptyString_ReturnsEmptyDiff()
    {
        var ctx = DiffParser.Parse("");

        Assert.NotNull(ctx);
        Assert.Empty(ctx.Files);
    }

    [Fact]
    public void Parse_BinaryFileDiff_HandledGracefully()
    {
        var binaryDiff = """
            diff --git a/assets/logo.png b/assets/logo.png
            index abc1234..def5678 100644
            Binary files a/assets/logo.png and b/assets/logo.png differ
            """;

        var ex = Record.Exception(() => DiffParser.Parse(binaryDiff));

        Assert.Null(ex);
    }

    [Fact]
    public void Parse_BinaryFileDiff_ProducesFileEntry()
    {
        var binaryDiff = """
            diff --git a/assets/logo.png b/assets/logo.png
            index abc1234..def5678 100644
            Binary files a/assets/logo.png and b/assets/logo.png differ
            """;

        var ctx = DiffParser.Parse(binaryDiff);

        Assert.Single(ctx.Files);
        Assert.Equal("assets/logo.png", ctx.Files[0].NewPath);
        Assert.Empty(ctx.Files[0].Hunks);
    }

    [Fact]
    public void Parse_UnicodeFilenames_Parsed()
    {
        var unicodeDiff = """
            diff --git a/src/日本語.cs b/src/日本語.cs
            index abc..def 100644
            --- a/src/日本語.cs
            +++ b/src/日本語.cs
            @@ -1,1 +1,1 @@
            -old
            +new
            """;

        var ex = Record.Exception(() => DiffParser.Parse(unicodeDiff));

        Assert.Null(ex);
    }

    [Fact]
    public void Parse_UnicodeFilenames_FileEntryPresent()
    {
        var unicodeDiff = """
            diff --git a/src/日本語.cs b/src/日本語.cs
            index abc..def 100644
            --- a/src/日本語.cs
            +++ b/src/日本語.cs
            @@ -1,1 +1,1 @@
            -old
            +new
            """;

        var ctx = DiffParser.Parse(unicodeDiff);

        Assert.Single(ctx.Files);
        Assert.Contains("日本語", ctx.Files[0].NewPath);
    }

    [Fact]
    public void Parse_LargeDiff_CompletesInReasonableTime()
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < 500; i++)
        {
            sb.AppendLine($"diff --git a/src/File{i}.cs b/src/File{i}.cs");
            sb.AppendLine($"index abc{i:D3}..def{i:D3} 100644");
            sb.AppendLine($"--- a/src/File{i}.cs");
            sb.AppendLine($"+++ b/src/File{i}.cs");
            sb.AppendLine("@@ -1,2 +1,2 @@");
            sb.AppendLine($"-old line {i}");
            sb.AppendLine($"+new line {i}");
        }

        var sw = Stopwatch.StartNew();
        var ctx = DiffParser.Parse(sb.ToString());
        sw.Stop();

        Assert.Equal(500, ctx.Files.Count);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5),
            $"Parsing 500-file diff took {sw.Elapsed.TotalSeconds:F2}s: exceeded 5s limit");
    }

    [Fact]
    public void Parse_WhitespaceOnlyString_ReturnsEmptyDiff()
    {
        var ctx = DiffParser.Parse("   \n\t\n   ");

        Assert.NotNull(ctx);
        Assert.Empty(ctx.Files);
    }

    [Fact]
    public void Parse_CommitSha_IsPreserved()
    {
        var ctx = DiffParser.Parse(SimpleDiff, commitSha: "abc123", commitMessage: "fix: something");

        Assert.Equal("abc123", ctx.CommitSha);
        Assert.Equal("fix: something", ctx.CommitMessage);
    }

    [Fact]
    public void Parse_DeletedFile_SetsIsDeletedTrue()
    {
        var deletedDiff = """
            diff --git a/src/Old.cs b/src/Old.cs
            deleted file mode 100644
            index abcdef1..0000000
            --- a/src/Old.cs
            +++ /dev/null
            @@ -1,3 +0,0 @@
            -using System;
            -class Old { }
            -
            """;

        var ctx = DiffParser.Parse(deletedDiff);

        Assert.Single(ctx.Files);
        Assert.True(ctx.Files[0].IsDeleted);
    }

    [Fact]
    public void Parse_RenamedFile_CaptureBothPaths()
    {
        var renamedDiff = """
            diff --git a/src/OldName.cs b/src/NewName.cs
            similarity index 90%
            rename from src/OldName.cs
            rename to src/NewName.cs
            index abc..def 100644
            --- a/src/OldName.cs
            +++ b/src/NewName.cs
            @@ -1,3 +1,3 @@
             public class Test {
            -    int x = 1;
            +    int x = 2;
             }
            """;

        var ctx = DiffParser.Parse(renamedDiff);

        Assert.Single(ctx.Files);
        Assert.Equal("src/OldName.cs", ctx.Files[0].OldPath);
        Assert.Equal("src/NewName.cs", ctx.Files[0].NewPath);
    }

    [Fact]
    public void Parse_HunkHeaderOnly_NoAddedRemovedLines()
    {
        var hunkOnlyDiff = """
            diff --git a/src/File.cs b/src/File.cs
            index abc..def 100644
            --- a/src/File.cs
            +++ b/src/File.cs
            @@ -1,3 +1,3 @@
             line 1
             line 2
             line 3
            """;

        var ctx = DiffParser.Parse(hunkOnlyDiff);

        Assert.Single(ctx.Files);
        Assert.Empty(ctx.Files[0].AddedLines);
        Assert.Empty(ctx.Files[0].RemovedLines);
    }

    [Fact]
    public void Parse_NoNewlineAtEndOfFile_HandledGracefully()
    {
        var noNewlineDiff = """
            diff --git a/src/File.cs b/src/File.cs
            index abc..def 100644
            --- a/src/File.cs
            +++ b/src/File.cs
            @@ -1,2 +1,2 @@
             line 1
            -old line
            +new line
            \ No newline at end of file
            """;

        var ctx = DiffParser.Parse(noNewlineDiff);

        Assert.Single(ctx.Files);
        Assert.Single(ctx.Files[0].AddedLines);
        Assert.Single(ctx.Files[0].RemovedLines);
    }

    [Fact]
    public void Parse_BareUnifiedDiff_ParsesCorrectly()
    {
        // Some tools emit bare unified diff without "diff --git" header
        var bareDiff = """
            --- a/src/File.cs
            +++ b/src/File.cs
            @@ -1,3 +1,4 @@
             line 1
            +new line
             line 2
             line 3
            """;

        var ctx = DiffParser.Parse(bareDiff);

        Assert.Single(ctx.Files);
        Assert.Equal("src/File.cs", ctx.Files[0].NewPath);
        Assert.Single(ctx.Files[0].AddedLines);
    }

    [Fact]
    public void Parse_MultipleHunksInSameFile_AllCaptured()
    {
        var multiHunkDiff = """
            diff --git a/src/File.cs b/src/File.cs
            index abc..def 100644
            --- a/src/File.cs
            +++ b/src/File.cs
            @@ -1,3 +1,4 @@
             line 1
            +added at start
             line 2
             line 3
            @@ -10,3 +11,4 @@
             line 10
            +added at middle
             line 11
             line 12
            """;

        var ctx = DiffParser.Parse(multiHunkDiff);

        Assert.Single(ctx.Files);
        Assert.Equal(2, ctx.Files[0].Hunks.Count);
        Assert.Equal(2, ctx.Files[0].AddedLines.Count());
    }

    [Fact]
    public void Parse_ContextLines_AreTracked()
    {
        var contextDiff = """
            diff --git a/src/File.cs b/src/File.cs
            index abc..def 100644
            --- a/src/File.cs
            +++ b/src/File.cs
            @@ -1,5 +1,5 @@
             context before 1
             context before 2
            -old line
            +new line
             context after 1
             context after 2
            """;

        var ctx = DiffParser.Parse(contextDiff);

        var file = ctx.Files[0];
        var hunk = file.Hunks[0];
        var contextLines = hunk.Lines.Where(l => l.Kind == GauntletCI.Core.Diff.DiffLineKind.Context).ToList();

        Assert.Equal(4, contextLines.Count);
    }

    [Fact]
    public void Parse_EmptyHunk_HandlesGracefully()
    {
        // Some diffs can have an empty hunk header followed immediately by another
        var diff = """
            diff --git a/src/File.cs b/src/File.cs
            index abc..def 100644
            --- a/src/File.cs
            +++ b/src/File.cs
            @@ -1,0 +1,0 @@
            """;

        var ex = Record.Exception(() => DiffParser.Parse(diff));
        Assert.Null(ex);
    }
}

// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;

namespace GauntletCI.Tests;

public class DiffParserTests
{
    [Fact]
    public void Parse_SimpleAddedAndRemovedLines_ShouldProduceCorrectHunk()
    {
        var raw = """
            diff --git a/src/Foo.cs b/src/Foo.cs
            index abc..def 100644
            --- a/src/Foo.cs
            +++ b/src/Foo.cs
            @@ -1,3 +1,3 @@
             using System;
            -int x = 1;
            +int x = 2;
             Console.WriteLine(x);
            """;

        var ctx = DiffParser.Parse(raw);

        Assert.Single(ctx.Files);
        var file = ctx.Files[0];
        Assert.Equal("src/Foo.cs", file.NewPath);

        var added = file.AddedLines.ToList();
        var removed = file.RemovedLines.ToList();

        Assert.Single(added);
        Assert.Equal("int x = 2;", added[0].Content);

        Assert.Single(removed);
        Assert.Equal("int x = 1;", removed[0].Content);
    }

    [Fact]
    public void Parse_MultipleFiles_ShouldReturnOneEntryPerFile()
    {
        var raw = """
            diff --git a/src/Foo.cs b/src/Foo.cs
            index abc..def 100644
            --- a/src/Foo.cs
            +++ b/src/Foo.cs
            @@ -1,1 +1,1 @@
            -old foo
            +new foo
            diff --git a/src/Bar.cs b/src/Bar.cs
            index 111..222 100644
            --- a/src/Bar.cs
            +++ b/src/Bar.cs
            @@ -1,1 +1,2 @@
             bar
            +extra bar
            """;

        var ctx = DiffParser.Parse(raw);

        Assert.Equal(2, ctx.Files.Count);
        Assert.Equal("src/Foo.cs", ctx.Files[0].NewPath);
        Assert.Equal("src/Bar.cs", ctx.Files[1].NewPath);
        Assert.Single(ctx.Files[0].AddedLines);
        Assert.Single(ctx.Files[1].AddedLines);
    }

    [Fact]
    public void Parse_NewFileMode_ShouldSetIsAddedTrue()
    {
        var raw = """
            diff --git a/src/New.cs b/src/New.cs
            new file mode 100644
            index 0000000..abcdef1
            --- /dev/null
            +++ b/src/New.cs
            @@ -0,0 +1,3 @@
            +using System;
            +class New { }
            +
            """;

        var ctx = DiffParser.Parse(raw);

        Assert.Single(ctx.Files);
        Assert.True(ctx.Files[0].IsAdded);
        Assert.Equal(3, ctx.Files[0].AddedLines.Count());
    }
}

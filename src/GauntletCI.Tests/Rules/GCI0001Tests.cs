// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0001Tests
{
    private static readonly GCI0001_DiffIntegrity Rule = new();

    [Fact]
    public async Task MixedCodeAndMarkdown_ShouldFlagMixedScope()
    {
        var raw = """
            diff --git a/src/Foo.cs b/src/Foo.cs
            index abc..def 100644
            --- a/src/Foo.cs
            +++ b/src/Foo.cs
            @@ -1,1 +1,1 @@
            -old
            +new
            diff --git a/README.md b/README.md
            index 111..222 100644
            --- a/README.md
            +++ b/README.md
            @@ -1,1 +1,1 @@
            -old docs
            +new docs
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("mixed scope"));
    }

    [Fact]
    public async Task PureCodeDiff_ShouldHaveNoMixedScopeFinding()
    {
        var raw = """
            diff --git a/src/Foo.cs b/src/Foo.cs
            index abc..def 100644
            --- a/src/Foo.cs
            +++ b/src/Foo.cs
            @@ -1,1 +1,1 @@
            -int x = 1;
            +int x = 2;
            diff --git a/src/Bar.cs b/src/Bar.cs
            index 111..222 100644
            --- a/src/Bar.cs
            +++ b/src/Bar.cs
            @@ -1,1 +1,1 @@
            -int y = 1;
            +int y = 2;
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("mixed scope"));
    }

    [Fact]
    public async Task CodeWithLockFile_ShouldNotFlagMixedScope()
    {
        var raw = """
            diff --git a/src/Foo.cs b/src/Foo.cs
            index abc..def 100644
            --- a/src/Foo.cs
            +++ b/src/Foo.cs
            @@ -1,1 +1,1 @@
            -int x = 1;
            +int x = 2;
            diff --git a/src/SharpCompress/packages.lock.json b/src/SharpCompress/packages.lock.json
            index 111..222 100644
            --- a/src/SharpCompress/packages.lock.json
            +++ b/src/SharpCompress/packages.lock.json
            @@ -1,1 +1,1 @@
            -{}
            +{ "version": 2 }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("mixed scope"));
    }
}

// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0019Tests
{
    private static readonly GCI0019_ConfidenceAndEvidence Rule = new();

    [Fact]
    public async Task BinaryFileInDiff_ShouldFlag()
    {
        // A .png file in the diff — DiffParser records it as a file with no hunks
        var raw = """
            diff --git a/assets/logo.png b/assets/logo.png
            index abc..def 100644
            Binary files a/assets/logo.png and b/assets/logo.png differ
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,2 @@
             // service
            +int x = 1;
            """;

        var diff = DiffParser.Parse(raw);
        var finding = ((GauntletCI.Core.Rules.IPostProcessor)Rule).PostProcess(diff);

        Assert.NotNull(finding);
        Assert.Contains("binary file(s)", finding.Summary);
    }

    [Fact]
    public async Task TinyDiff_ShouldNotFlag()
    {
        // Single-line changes are valid refactors (var→const, value updates, etc.) — must not fire
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,1 @@
            -int x = 1;
            +int x = 2;
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Very small diff"));
    }

    [Fact]
    public async Task NormalSizedDiff_ShouldNotFlagTiny()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,6 @@
             // service
            +int a = 1;
            +int b = 2;
            +int c = 3;
            +int d = 4;
            +int e = 5;
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Very small diff"));
    }

    [Fact]
    public async Task EmptyDiff_ShouldNotFlagTiny()
    {
        // totalLines == 0 → no tiny diff finding
        var diff = DiffParser.Parse("diff --git a/src/Foo.cs b/src/Foo.cs\nindex abc..def 100644");
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Very small diff"));
    }
}

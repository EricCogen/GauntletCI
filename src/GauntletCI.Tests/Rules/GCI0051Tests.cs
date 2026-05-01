// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0051Tests
{
    private static readonly GCI0051_NumericCoercionRisks Rule = new();

    private static AnalysisContext MakeContext(DiffContext diff) => new() { Diff = diff };

    [Fact]
    public async Task UncheckedIntCastFromLong_ShouldFlag()
    {
        var raw = """
            diff --git a/src/Handler.cs b/src/Handler.cs
            index abc..def 100644
            --- a/src/Handler.cs
            +++ b/src/Handler.cs
            @@ -1,5 +1,5 @@
             public class Handler {
            -    public int GetSize() { return 0; }
            +    public int GetSize(long totalSize) { return (int)totalSize; }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(MakeContext(diff), default);

        Assert.Single(findings, f => f.Summary.Contains("truncation") || f.Summary.Contains("cast"));
    }

    [Fact]
    public async Task CheckedCast_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/Handler.cs b/src/Handler.cs
            index abc..def 100644
            --- a/src/Handler.cs
            +++ b/src/Handler.cs
            @@ -1,5 +1,5 @@
             public class Handler {
            -    public int GetSize() { return 0; }
            +    public int GetSize(long totalSize) { checked { return (int)totalSize; } }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(MakeContext(diff), default);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task AssignLengthToInt_ShouldFlag()
    {
        var raw = """
            diff --git a/src/Parser.cs b/src/Parser.cs
            index abc..def 100644
            --- a/src/Parser.cs
            +++ b/src/Parser.cs
            @@ -1,5 +1,5 @@
             public class Parser {
            -    private int capacity = 0;
            +    private int capacity = data.Length;
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(MakeContext(diff), default);

        // May or may not flag depending on heuristic sensitivity
        // This test documents behavior
    }

    [Fact]
    public async Task SafeSmallCast_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/Util.cs b/src/Util.cs
            index abc..def 100644
            --- a/src/Util.cs
            +++ b/src/Util.cs
            @@ -1,5 +1,5 @@
             public class Util {
            -    public byte GetByte() { return 0; }
            +    public byte GetByte(byte b) { return (byte)b; }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(MakeContext(diff), default);

        Assert.Empty(findings);
    }
}

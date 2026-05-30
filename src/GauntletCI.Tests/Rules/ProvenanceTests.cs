// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Configuration;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules.Delivery;

namespace GauntletCI.Tests.Rules;

public sealed class DiffProvenanceAnalyzerTests
{
    [Fact]
    public void Build_IdenticalRemovedAndAddedLine_MarksRelocated()
    {
        var diff = DiffParser.Parse("""
            diff --git a/src/Queue.cs b/src/Queue.cs
            index abc..def 100644
            --- a/src/Queue.cs
            +++ b/src/Queue.cs
            @@ -10,3 +10,3 @@
                 try { DoWork(); }
            -    catch { }
            +    catch { }
             }
            """);

        var index = DiffProvenanceAnalyzer.Build(diff);
        var relocatedLine = diff.Files.Single().AddedLines.Single(l => l.Content.Contains("catch", StringComparison.Ordinal));

        Assert.True(index.IsRelocated("src/Queue.cs", relocatedLine.LineNumber));
    }

    [Fact]
    public void Build_CrossFileMove_MarksRelocatedOnAddedSide()
    {
        var diff = DiffParser.Parse("""
            diff --git a/old/Handler.cs b/new/Handler.cs
            index abc..def 100644
            --- a/old/Handler.cs
            +++ b/new/Handler.cs
            @@ -1,3 +0,0 @@
            -public void Handle() { throw new NotImplementedException(); }
            diff --git a/new/Handler.cs b/new/Handler.cs
            index abc..def 100644
            --- /dev/null
            +++ b/new/Handler.cs
            @@ -0,0 +1,1 @@
            +public void Handle() { throw new NotImplementedException(); }
            """);

        var index = DiffProvenanceAnalyzer.Build(diff);

        Assert.True(index.IsRelocated("new/Handler.cs", 1));
    }

    [Fact]
    public void Build_NetNewAddedLine_NotRelocated()
    {
        var diff = DiffParser.Parse("""
            diff --git a/src/Queue.cs b/src/Queue.cs
            index abc..def 100644
            --- a/src/Queue.cs
            +++ b/src/Queue.cs
            @@ -10,2 +10,3 @@
                 try { DoWork(); }
            +    catch { }
             }
            """);

        var index = DiffProvenanceAnalyzer.Build(diff);

        Assert.False(index.IsRelocated("src/Queue.cs", 11));
    }
}

public sealed class ProvenanceFindingProcessorTests
{
    private static Finding MakeFinding(string ruleId, string file, int line) => new()
    {
        RuleId = ruleId,
        RuleName = ruleId,
        Summary = ruleId,
        Evidence = $"Line {line}",
        WhyItMatters = "why",
        SuggestedAction = "fix",
        FilePath = file,
        Line = line,
    };

    [Fact]
    public void Apply_RelocatedLine_DropsNonExemptFinding()
    {
        var diff = DiffParser.Parse("""
            diff --git a/src/Queue.cs b/src/Queue.cs
            index abc..def 100644
            --- a/src/Queue.cs
            +++ b/src/Queue.cs
            @@ -10,3 +10,3 @@
                 try { DoWork(); }
            -    catch { }
            +    catch { }
             }
            """);

        var index = DiffProvenanceAnalyzer.Build(diff);
        var relocatedLine = diff.Files.Single().AddedLines.Single(l => l.Content.Contains("catch", StringComparison.Ordinal));
        var findings = new[] { MakeFinding("GCI0007", "src/Queue.cs", relocatedLine.LineNumber) };

        var result = ProvenanceFindingProcessor.Apply(findings, index, new ProvenanceConfig());

        Assert.Empty(result.Findings);
        Assert.Equal(1, result.DroppedCount);
    }

    [Fact]
    public void Apply_RelocatedLine_KeepsExemptRule()
    {
        var diff = DiffParser.Parse("""
            diff --git a/src/Api.cs b/src/Api.cs
            index abc..def 100644
            --- a/src/Api.cs
            +++ b/src/Api.cs
            @@ -1,3 +1,3 @@
            -public void Run(int x) { }
            +public void Run(int x) { }
            """);

        var index = DiffProvenanceAnalyzer.Build(diff);
        var findings = new[] { MakeFinding("GCI0003", "src/Api.cs", 1) };

        var result = ProvenanceFindingProcessor.Apply(findings, index, new ProvenanceConfig());

        Assert.Single(result.Findings);
        Assert.Equal(0, result.DroppedCount);
    }
}

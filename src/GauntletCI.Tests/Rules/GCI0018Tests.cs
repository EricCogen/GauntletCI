// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0018Tests
{
    private static readonly GCI0018_ProductionReadiness Rule = new();

    [Fact]
    public async Task TodoMarkerInCode_ShouldFlag()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,2 @@
             // service
            +// TODO: implement later
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("TODO/FIXME/HACK"));
    }

    [Fact]
    public async Task NotImplementedException_ShouldFlag()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,2 @@
             // service
            +throw new NotImplementedException();
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("NotImplementedException"));
    }

    [Fact]
    public async Task ConsoleWriteLineInProductionCode_ShouldFlag()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,2 @@
             // service
            +Console.WriteLine("debug output");
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("Console.WriteLine()"));
    }

    [Fact]
    public async Task ConsoleWriteLineInCliFile_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/GauntletCI.Cli/Reporter.cs b/src/GauntletCI.Cli/Reporter.cs
            index abc..def 100644
            --- a/src/GauntletCI.Cli/Reporter.cs
            +++ b/src/GauntletCI.Cli/Reporter.cs
            @@ -1,1 +1,2 @@
             // reporter
            +Console.WriteLine("Findings: " + count);
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Console.WriteLine()"));
    }

    [Fact]
    public async Task DebugAssertInProductionCode_ShouldFlag()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,2 @@
             // service
            +Debug.Assert(value != null, "Value must not be null");
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("Debug.Assert()"));
    }

    [Fact]
    public async Task FixmeMarker_ShouldFlag()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,2 @@
             // service
            +// FIXME: this is broken
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("TODO/FIXME/HACK"));
    }
}

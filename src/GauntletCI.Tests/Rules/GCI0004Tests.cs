// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0004Tests
{
    private static readonly GCI0004_BreakingChangeRisk Rule = new();

    [Fact]
    public async Task ManyFilesWithRemovals_ShouldCollapseToSingleFinding()
    {
        // 4 files each removing a public method - should collapse to one cross-file summary.
        static string FileBlock(string name) => $"""
            diff --git a/src/{name}.cs b/src/{name}.cs
            index abc..def 100644
            --- a/src/{name}.cs
            +++ b/src/{name}.cs
            @@ -1,3 +1,2 @@
             // class
            -public void Execute()
             // end
            """;

        var raw = string.Join("\n", new[] { "Alpha", "Beta", "Gamma", "Delta" }.Select(FileBlock));
        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        var f = Assert.Single(findings, x => x.Summary.Contains("Public API removed"));
        Assert.Contains("4 files", f.Summary);
        Assert.Contains("Files:", f.Evidence);
    }

    [Fact]
    public async Task ManyFilesWithSigChanges_ShouldCollapseToSingleFinding()
    {
        // 4 files each changing the same public method sig - should collapse to one summary.
        static string FileBlock(string name) => $"""
            diff --git a/src/{name}.cs b/src/{name}.cs
            index abc..def 100644
            --- a/src/{name}.cs
            +++ b/src/{name}.cs
            @@ -1,3 +1,3 @@
             // class
            -public void Process(string s)
            +public void Process(string s, int timeout)
             // end
            """;

        var raw = string.Join("\n", new[] { "Alpha", "Beta", "Gamma", "Delta" }.Select(FileBlock));
        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        var f = Assert.Single(findings, x => x.Summary.Contains("signature changed"));
        Assert.Contains("4 files", f.Summary);
    }
    [Fact]
    public async Task RemovedPublicMethod_ShouldFlagHighConfidence()
    {

        var raw = """
            diff --git a/src/Calculator.cs b/src/Calculator.cs
            index abc..def 100644
            --- a/src/Calculator.cs
            +++ b/src/Calculator.cs
            @@ -1,3 +1,2 @@
             // calculator
            -public void Calculate(int x)
             // end
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f =>
            f.Summary.Contains("Public API removed") &&
            f.Confidence == Confidence.High);
    }

    [Fact]
    public async Task ObsoleteAttributeRemoved_ShouldFlag()
    {
        var raw = """
            diff --git a/src/LegacyService.cs b/src/LegacyService.cs
            index abc..def 100644
            --- a/src/LegacyService.cs
            +++ b/src/LegacyService.cs
            @@ -1,3 +1,2 @@
             // service
            -[Obsolete("use NewMethod")]
             public void OldMethod() { }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("[Obsolete]"));
    }

    [Fact]
    public async Task RenamedPublicMethod_ShouldFlag()
    {
        // Remove GetName, add FetchName — different names → "Public API removed"
        var raw = """
            diff --git a/src/UserService.cs b/src/UserService.cs
            index abc..def 100644
            --- a/src/UserService.cs
            +++ b/src/UserService.cs
            @@ -1,3 +1,3 @@
             // service
            -public string GetName()
            +public string FetchName()
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("Public API removed"));
    }

    [Fact]
    public async Task ChangedPublicMethodSignature_ShouldFlagMediumConfidence()
    {
        // Same name, different signature → "Public API signature changed"
        var raw = """
            diff --git a/src/Calculator.cs b/src/Calculator.cs
            index abc..def 100644
            --- a/src/Calculator.cs
            +++ b/src/Calculator.cs
            @@ -1,3 +1,3 @@
             // calculator
            -public void Calculate(int x)
            +public void Calculate(int x, string label)
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f =>
            f.Summary.Contains("Calculate") &&
            f.Confidence == Confidence.Medium);
    }

    [Fact]
    public async Task OnlyAddedPublicMethod_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/Calculator.cs b/src/Calculator.cs
            index abc..def 100644
            --- a/src/Calculator.cs
            +++ b/src/Calculator.cs
            @@ -1,2 +1,3 @@
             // calculator
            +public int Add(int x, int y)
             // end
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Public API removed"));
    }

    [Fact]
    public async Task RemovedPrivateMethod_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/Calculator.cs b/src/Calculator.cs
            index abc..def 100644
            --- a/src/Calculator.cs
            +++ b/src/Calculator.cs
            @@ -1,3 +1,2 @@
             // calculator
            -private void ComputeInternal(int x)
             // end
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task SamePublicSignatureRemovedAndReadded_ShouldNotFlag()
    {
        // Same name AND same content re-added (e.g., just moved within file).
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,3 +1,3 @@
             // service
            -public void Execute(int id)
            +public void Execute(int id)
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Public API removed"));
        Assert.DoesNotContain(findings, f => f.Summary.Contains("Public API signature changed"));
    }

    [Fact]
    public async Task PublicMethodOnlyOptionalParamsAdded_ShouldNotFlag()
    {
        // Backward-compatible extension — new params all have defaults
        var raw = """
            diff --git a/src/Calculator.cs b/src/Calculator.cs
            index abc..def 100644
            --- a/src/Calculator.cs
            +++ b/src/Calculator.cs
            @@ -1,3 +1,3 @@
             // calculator
            -public void Calculate(int x)
            +public void Calculate(int x, string label = "default", bool verbose = false)
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("signature changed"));
    }
}

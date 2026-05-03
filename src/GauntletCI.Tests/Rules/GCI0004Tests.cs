// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0004Tests
{
    private static readonly GCI0004_BreakingChangeRisk Rule = new(new StubPatternProvider());

    [Fact]
    public async Task ObsoleteAttributeAdded_ShouldFlag()
    {
        var raw = """
            diff --git a/src/PaymentService.cs b/src/PaymentService.cs
            index abc..def 100644
            --- a/src/PaymentService.cs
            +++ b/src/PaymentService.cs
            @@ -1,3 +1,4 @@
             // service
            +[Obsolete("Use PaymentServiceV2 instead.")]
             public void ProcessPayment(decimal amount) { }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        var f = Assert.Single(findings);
        Assert.Contains("[Obsolete] added", f.Summary);
        Assert.Equal(Confidence.Medium, f.Confidence);
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

        Assert.Contains(findings, f => f.Summary.Contains("[Obsolete] attribute removed"));
    }

    [Fact]
    public async Task PublicMethodRemovedWithoutObsolete_ShouldNotFlag()
    {
        // Regression: rule no longer fires on raw public API removal (117 FPs in corpus).
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

        Assert.Empty(findings);
    }

    [Fact]
    public async Task PublicSignatureChangedWithoutObsolete_ShouldNotFlag()
    {
        // Regression: rule no longer fires on signature changes alone.
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

        Assert.Empty(findings);
    }

    [Fact]
    public async Task ObsoleteInGeneratedFile_ShouldNotFlag()
    {
        // .netstandard2.0.cs API surface files must be ignored.
        var raw = """
            diff --git a/sdk/Azure.Search.Documents.netstandard2.0.cs b/sdk/Azure.Search.Documents.netstandard2.0.cs
            index abc..def 100644
            --- a/sdk/Azure.Search.Documents.netstandard2.0.cs
            +++ b/sdk/Azure.Search.Documents.netstandard2.0.cs
            @@ -1,3 +1,4 @@
             // generated
            +[Obsolete("Deprecated property.")]
             public string Category { get; set; }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task ObsoleteInTestFile_ShouldNotFlag()
    {
        var raw = """
            diff --git a/tests/LegacyTests.cs b/tests/LegacyTests.cs
            index abc..def 100644
            --- a/tests/LegacyTests.cs
            +++ b/tests/LegacyTests.cs
            @@ -1,3 +1,4 @@
             // tests
            +[Obsolete("old test helper")]
             public void TestOldPath() { }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task ManyFilesWithObsoleteAdded_ShouldCollapseToSingleFinding()
    {
        // 4 files each adding [Obsolete] - should collapse to one cross-file summary.
        static string FileBlock(string name) => $"""
            diff --git a/src/{name}.cs b/src/{name}.cs
            index abc..def 100644
            --- a/src/{name}.cs
            +++ b/src/{name}.cs
            @@ -1,2 +1,3 @@
             // class
            +[Obsolete("Use V2.")]
             public void Run();
            """;

        var raw = string.Join("\n", new[] { "Alpha", "Beta", "Gamma", "Delta" }.Select(FileBlock));
        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        var f = Assert.Single(findings, x => x.Summary.Contains("[Obsolete] added"));
        Assert.Contains("4 files", f.Summary);
        Assert.Contains("Files:", f.Evidence);
    }
}

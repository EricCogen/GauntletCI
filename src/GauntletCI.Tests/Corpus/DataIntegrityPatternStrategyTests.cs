// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Corpus.Labeling.Strategies;
using GauntletCI.Corpus.Models;
using Xunit;

namespace GauntletCI.Tests.Corpus;

public sealed class DataIntegrityPatternStrategyTests
{
    private readonly DataIntegrityPatternStrategy _strategy = new();

    [Fact]
    public void Apply_WithRemovedPublicMethodSignature_ReturnsTrueLabel()
    {
        var context = new DiffAnalysisContext(
            new[] { "public void DoSomething() { }" },
            new[] { "public void DoSomething(int param) { }" },
            new[] { "--- a/src/Program.cs" },
            new[] { "public void DoSomething() { }" }, // production removed
            new[] { "public void DoSomething(int param) { }" }); // production added

        var results = _strategy.Apply("fixture1", context);

        var gci0003 = results.FirstOrDefault(r => r.RuleId == "GCI0003");
        Assert.NotNull(gci0003);
        Assert.True(gci0003.ShouldTrigger);
    }

    [Fact]
    public void Apply_WithRemovedSerializationAttribute_ReturnsTrueLabel()
    {
        var context = new DiffAnalysisContext(
            new[] { "[JsonProperty(\"name\")] public string Name { get; set; }" },
            new[] { "public string Name { get; set; }" },
            new[] { "--- a/src/Model.cs" },
            new[] { "[JsonProperty(\"name\")] public string Name { get; set; }" },
            new[] { "public string Name { get; set; }" });

        var results = _strategy.Apply("fixture1", context);

        var gci0021 = results.FirstOrDefault(r => r.RuleId == "GCI0021");
        Assert.NotNull(gci0021);
        Assert.True(gci0021.ShouldTrigger);
    }

    [Fact]
    public void Apply_WithRemovedUsingStatement_ReturnsTrueLabel()
    {
        var context = new DiffAnalysisContext(
            new[] { "using (var conn = new Connection()) { }" },
            new[] { "var conn = new Connection();" },
            new[] { "--- a/src/Data.cs" },
            new[] { "using (var conn = new Connection()) { }" },
            new[] { "var conn = new Connection();" });

        var results = _strategy.Apply("fixture1", context);

        var gci0024 = results.FirstOrDefault(r => r.RuleId == "GCI0024");
        Assert.NotNull(gci0024);
        Assert.True(gci0024.ShouldTrigger);
    }

    [Fact]
    public void Apply_WithRemovedEFMigrationOperation_ReturnsTrueLabel()
    {
        var context = new DiffAnalysisContext(
            new[] { "migrationBuilder.DropTable(\"Users\");" },
            new string[] { },
            new[] { "--- a/Migrations/20230101120000_Initial.cs" },
            new[] { "migrationBuilder.DropTable(\"Users\");" },
            new string[] { });

        var results = _strategy.Apply("fixture1", context);

        var gci0021 = results.FirstOrDefault(r => r.RuleId == "GCI0021");
        Assert.NotNull(gci0021);
        Assert.True(gci0021.ShouldTrigger);
    }

    [Fact]
    public void Apply_WithPrivateMethodChange_DoesNotTriggerGCI0003()
    {
        var context = new DiffAnalysisContext(
            new[] { "private void DoSomething() { }" },
            new[] { "private void DoSomething(int param) { }" },
            new[] { "--- a/src/Program.cs" },
            new string[] { },
            new string[] { });

        var results = _strategy.Apply("fixture1", context);

        var gci0003 = results.FirstOrDefault(r => r.RuleId == "GCI0003");
        Assert.Null(gci0003);
    }
}

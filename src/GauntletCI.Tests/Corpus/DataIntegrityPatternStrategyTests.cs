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
        var context = new DiffAnalysisContext
        {
            AddedLines = new[] { "public void DoSomething(int param) { }" },
            RemovedLines = new[] { "public void DoSomething() { }" },
            PathLines = new[] { "--- a/src/Program.cs" },
            ProductionAddedLines = new[] { "public void DoSomething(int param) { }" },
            ProductionRemovedLines = new[] { "public void DoSomething() { }" },
        };

        var results = _strategy.Apply("fixture1", context);

        var gci0003 = results.FirstOrDefault(r => r.RuleId == "GCI0003");
        Assert.NotNull(gci0003);
        Assert.True(gci0003.ShouldTrigger);
    }

    [Fact]
    public void Apply_WithRemovedSerializationAttribute_ReturnsTrueLabel()
    {
        var context = new DiffAnalysisContext
        {
            AddedLines = new[] { "public string Name { get; set; }" },
            RemovedLines = new[] { "[JsonProperty(\"name\")] public string Name { get; set; }" },
            PathLines = new[] { "--- a/src/Model.cs" },
            ProductionAddedLines = Array.Empty<string>(),
            ProductionRemovedLines = new[] { "[JsonProperty(\"name\")] public string Name { get; set; }" },
        };

        var results = _strategy.Apply("fixture1", context);

        var gci0021 = results.FirstOrDefault(r => r.RuleId == "GCI0021");
        Assert.NotNull(gci0021);
        Assert.True(gci0021.ShouldTrigger);
    }

    [Fact]
    public void Apply_WithRemovedUsingStatement_ReturnsTrueLabel()
    {
        var context = new DiffAnalysisContext
        {
            AddedLines = new[] { "var conn = new Connection();" },
            RemovedLines = new[] { "using (var conn = new Connection()) { }" },
            PathLines = new[] { "--- a/src/Data.cs" },
            ProductionAddedLines = Array.Empty<string>(),
            ProductionRemovedLines = new[] { "using (var conn = new Connection()) { }" },
        };

        var results = _strategy.Apply("fixture1", context);

        var gci0024 = results.FirstOrDefault(r => r.RuleId == "GCI0024");
        Assert.NotNull(gci0024);
        Assert.True(gci0024.ShouldTrigger);
    }

    [Fact]
    public void Apply_WithRemovedEFMigrationOperation_ReturnsTrueLabel()
    {
        var context = new DiffAnalysisContext
        {
            AddedLines = Array.Empty<string>(),
            RemovedLines = new[] { "migrationBuilder.DropColumn(name: \"LegacyField\", table: \"Users\");" },
            PathLines = new[] { "--- a/src/Migrations/20240101_Initial.cs" },
            ProductionAddedLines = Array.Empty<string>(),
            ProductionRemovedLines = Array.Empty<string>(),
        };

        var results = _strategy.Apply("fixture1", context);

        var gci0021 = results.FirstOrDefault(r => r.RuleId == "GCI0021");
        Assert.NotNull(gci0021);
        Assert.True(gci0021.ShouldTrigger);
    }

    [Fact]
    public void Apply_WithPrivateMethodChange_DoesNotTriggerGCI0003()
    {
        var context = new DiffAnalysisContext
        {
            AddedLines = new[] { "private void DoSomething(int param) { }" },
            RemovedLines = new[] { "private void DoSomething() { }" },
            PathLines = new[] { "--- a/src/Program.cs" },
            ProductionAddedLines = Array.Empty<string>(),
            ProductionRemovedLines = Array.Empty<string>(),
        };

        var results = _strategy.Apply("fixture1", context);

        var gci0003 = results.FirstOrDefault(r => r.RuleId == "GCI0003");
        Assert.Null(gci0003);
    }
}

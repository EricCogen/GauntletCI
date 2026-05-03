// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Corpus.Labeling.Strategies;
using GauntletCI.Corpus.Models;
using Xunit;

namespace GauntletCI.Tests.Corpus;

public sealed class NullabilityPatternStrategyTests
{
    private readonly NullabilityPatternStrategy _strategy = new();

    [Fact]
    public void Apply_WithUnsafeNullAssignment_ReturnsTrueLabel()
    {
        var context = new DiffAnalysisContext
        {
            AddedLines = Array.Empty<string>(),
            RemovedLines = Array.Empty<string>(),
            PathLines = new[] { "--- a/src/Program.cs" },
            ProductionAddedLines = new[] { "var obj = null; // unsafe" },
            ProductionRemovedLines = Array.Empty<string>(),
        };

        var results = _strategy.Apply("fixture1", context);

        var gci0006 = results.FirstOrDefault(r => r.RuleId == "GCI0006");
        Assert.NotNull(gci0006);
        Assert.True(gci0006.ShouldTrigger);
    }

    [Fact]
    public void Apply_WithRemovedNullCoalescing_ReturnsTrueLabel()
    {
        var context = new DiffAnalysisContext
        {
            AddedLines = new[] { "var value = obj;" },
            RemovedLines = new[] { "var value = obj ?? defaultValue;" },
            PathLines = new[] { "--- a/src/Program.cs" },
            ProductionAddedLines = Array.Empty<string>(),
            ProductionRemovedLines = new[] { "var value = obj ?? defaultValue;" },
        };

        var results = _strategy.Apply("fixture1", context);

        var gci0006 = results.FirstOrDefault(r => r.RuleId == "GCI0006");
        Assert.NotNull(gci0006);
        Assert.True(gci0006.ShouldTrigger);
    }

    [Fact]
    public void Apply_WithNullForgivingOperator_ReturnsTrueLabel()
    {
        var context = new DiffAnalysisContext
        {
            AddedLines = new[] { "var name = obj.Name!;" },
            RemovedLines = Array.Empty<string>(),
            PathLines = new[] { "--- a/src/Program.cs" },
            ProductionAddedLines = new[] { "var name = obj.Name!;" },
            ProductionRemovedLines = Array.Empty<string>(),
        };

        var results = _strategy.Apply("fixture1", context);

        var gci0043 = results.FirstOrDefault(r => r.RuleId == "GCI0043");
        Assert.NotNull(gci0043);
        Assert.True(gci0043.ShouldTrigger);
    }

    [Fact]
    public void Apply_WithRemovedNullForgivingOperator_ReturnsTrueLabel()
    {
        var context = new DiffAnalysisContext
        {
            AddedLines = new[] { "var name = obj.Name;" },
            RemovedLines = new[] { "var name = obj.Name!;" },
            PathLines = new[] { "--- a/src/Program.cs" },
            ProductionAddedLines = Array.Empty<string>(),
            ProductionRemovedLines = new[] { "var name = obj.Name!;" },
        };

        var results = _strategy.Apply("fixture1", context);

        var gci0043 = results.FirstOrDefault(r => r.RuleId == "GCI0043");
        Assert.NotNull(gci0043);
        Assert.True(gci0043.ShouldTrigger);
    }

    [Fact]
    public void Apply_WithNullableAnnotationDisable_ReturnsTrueLabel()
    {
        var context = new DiffAnalysisContext
        {
            AddedLines = new[] { "#pragma warning disable CS8600" },
            RemovedLines = Array.Empty<string>(),
            PathLines = new[] { "--- a/src/Program.cs" },
            ProductionAddedLines = new[] { "#pragma warning disable CS8600" },
            ProductionRemovedLines = Array.Empty<string>(),
        };

        var results = _strategy.Apply("fixture1", context);

        var gci0043 = results.FirstOrDefault(r => r.RuleId == "GCI0043");
        Assert.NotNull(gci0043);
        Assert.True(gci0043.ShouldTrigger);
    }

    [Fact]
    public void Apply_WithCommentedNullAssignment_DoesNotTrigger()
    {
        var context = new DiffAnalysisContext
        {
            AddedLines = Array.Empty<string>(),
            RemovedLines = Array.Empty<string>(),
            PathLines = new[] { "--- a/src/Program.cs" },
            ProductionAddedLines = new[] { "// var obj = null;" },
            ProductionRemovedLines = Array.Empty<string>(),
        };

        var results = _strategy.Apply("fixture1", context);

        var gci0006 = results.FirstOrDefault(r => r.RuleId == "GCI0006");
        Assert.Null(gci0006);
    }

    [Fact]
    public void Apply_WithNullableTypeDeclaration_DoesNotTrigger()
    {
        var context = new DiffAnalysisContext
        {
            AddedLines = Array.Empty<string>(),
            RemovedLines = Array.Empty<string>(),
            PathLines = new[] { "--- a/src/Program.cs" },
            ProductionAddedLines = new[] { "string? value = null; // nullable type - safe" },
            ProductionRemovedLines = Array.Empty<string>(),
        };

        var results = _strategy.Apply("fixture1", context);

        var gci0006 = results.FirstOrDefault(r => r.RuleId == "GCI0006");
        Assert.Null(gci0006);
    }
}

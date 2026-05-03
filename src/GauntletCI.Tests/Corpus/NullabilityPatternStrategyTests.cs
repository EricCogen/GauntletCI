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
        var context = new DiffAnalysisContext(
            new string[] { },
            new[] { "var obj = null; // unsafe" },
            new[] { "--- a/src/Program.cs" },
            new string[] { },
            new[] { "var obj = null; // unsafe" });

        var results = _strategy.Apply("fixture1", context);

        var gci0006 = results.FirstOrDefault(r => r.RuleId == "GCI0006");
        Assert.NotNull(gci0006);
        Assert.True(gci0006.ShouldTrigger);
    }

    [Fact]
    public void Apply_WithRemovedNullCoalescing_ReturnsTrueLabel()
    {
        var context = new DiffAnalysisContext(
            new[] { "var value = obj ?? defaultValue;" },
            new[] { "var value = obj;" },
            new[] { "--- a/src/Program.cs" },
            new[] { "var value = obj ?? defaultValue;" },
            new[] { "var value = obj;" });

        var results = _strategy.Apply("fixture1", context);

        var gci0006 = results.FirstOrDefault(r => r.RuleId == "GCI0006");
        Assert.NotNull(gci0006);
        Assert.True(gci0006.ShouldTrigger);
    }

    [Fact]
    public void Apply_WithNullForgivingOperator_ReturnsTrueLabel()
    {
        var context = new DiffAnalysisContext(
            new string[] { },
            new[] { "var name = obj.Name!;" },
            new[] { "--- a/src/Program.cs" },
            new string[] { },
            new[] { });

        var results = _strategy.Apply("fixture1", context);

        var gci0043 = results.FirstOrDefault(r => r.RuleId == "GCI0043");
        Assert.NotNull(gci0043);
        Assert.True(gci0043.ShouldTrigger);
    }

    [Fact]
    public void Apply_WithRemovedNullForgivingOperator_ReturnsTrueLabel()
    {
        var context = new DiffAnalysisContext(
            new[] { "var name = obj.Name!;" },
            new[] { "var name = obj.Name;" },
            new[] { "--- a/src/Program.cs" },
            new[] { "var name = obj.Name!;" },
            new[] { });

        var results = _strategy.Apply("fixture1", context);

        var gci0043 = results.FirstOrDefault(r => r.RuleId == "GCI0043");
        Assert.NotNull(gci0043);
        Assert.True(gci0043.ShouldTrigger);
    }

    [Fact]
    public void Apply_WithNullableAnnotationDisable_ReturnsTrueLabel()
    {
        var context = new DiffAnalysisContext(
            new string[] { },
            new[] { "#pragma warning disable CS8600" },
            new[] { "--- a/src/Program.cs" },
            new string[] { },
            new[] { });

        var results = _strategy.Apply("fixture1", context);

        var gci0043 = results.FirstOrDefault(r => r.RuleId == "GCI0043");
        Assert.NotNull(gci0043);
        Assert.True(gci0043.ShouldTrigger);
    }

    [Fact]
    public void Apply_WithCommentedNullAssignment_DoesNotTrigger()
    {
        var context = new DiffAnalysisContext(
            new string[] { },
            new[] { "// var obj = null;" },
            new[] { "--- a/src/Program.cs" },
            new string[] { },
            new[] { });

        var results = _strategy.Apply("fixture1", context);

        var gci0006 = results.FirstOrDefault(r => r.RuleId == "GCI0006");
        Assert.Null(gci0006);
    }

    [Fact]
    public void Apply_WithNullableTypeDeclaration_DoesNotTrigger()
    {
        var context = new DiffAnalysisContext(
            new string[] { },
            new[] { "string? value = null; // nullable type - safe" },
            new[] { "--- a/src/Program.cs" },
            new string[] { },
            new[] { });

        var results = _strategy.Apply("fixture1", context);

        var gci0006 = results.FirstOrDefault(r => r.RuleId == "GCI0006");
        Assert.Null(gci0006);
    }
}

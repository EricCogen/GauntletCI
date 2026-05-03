// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Corpus.Labeling.Strategies;
using GauntletCI.Corpus.Models;
using Xunit;

namespace GauntletCI.Tests.Corpus;

public sealed class EdgeCasePatternStrategyTests
{
    private readonly EdgeCasePatternStrategy _strategy = new();

    [Fact]
    public void Apply_WithRemovedIdempotencyKey_ReturnsTrueLabel()
    {
        var context = new DiffAnalysisContext(
            new[] { "var idempotencyKey = request.Headers[\"Idempotency-Key\"];" },
            new[] { "var requestId = request.Id;" },
            new[] { "--- a/src/Handlers/PaymentHandler.cs" },
            new[] { "var idempotencyKey = request.Headers[\"Idempotency-Key\"];" },
            new[] { });

        var results = _strategy.Apply("fixture1", context);

        var gci0022 = results.FirstOrDefault(r => r.RuleId == "GCI0022");
        Assert.NotNull(gci0022);
        Assert.True(gci0022.ShouldTrigger);
    }

    [Fact]
    public void Apply_WithCrossLayerDependency_ReturnsTrueLabel()
    {
        var context = new DiffAnalysisContext(
            new string[] { },
            new[] { "var repository = new UserRepository(); // UI calling Repository directly" },
            new[] { "--- a/src/UI/Controller.cs" },
            new string[] { },
            new[] { });

        var results = _strategy.Apply("fixture1", context);

        var gci0035 = results.FirstOrDefault(r => r.RuleId == "GCI0035");
        Assert.NotNull(gci0035);
        Assert.True(gci0035.ShouldTrigger);
    }

    [Fact]
    public void Apply_WithRemovedTestMethod_ReturnsTrueLabel()
    {
        var context = new DiffAnalysisContext(
            new[] { "[Fact]" , "public void Test_ShouldDoSomething() { }" },
            new string[] { },
            new[] { "--- a/tests/UnitTests.cs" },
            new string[] { },
            new string[] { });

        var results = _strategy.Apply("fixture1", context);

        var gci0041 = results.FirstOrDefault(r => r.RuleId == "GCI0041");
        Assert.NotNull(gci0041);
        Assert.True(gci0041.ShouldTrigger);
    }

    [Fact]
    public void Apply_WithRemovedAssertion_ReturnsTrueLabel()
    {
        var context = new DiffAnalysisContext(
            new[] { "Assert.True(result.IsSuccess);" },
            new[] { "var result = RunTest();" },
            new[] { "--- a/tests/UnitTests.cs" },
            new[] { "Assert.True(result.IsSuccess);" },
            new[] { });

        var results = _strategy.Apply("fixture1", context);

        var gci0041 = results.FirstOrDefault(r => r.RuleId == "GCI0041");
        Assert.NotNull(gci0041);
        Assert.True(gci0041.ShouldTrigger);
    }

    [Fact]
    public void Apply_WithLinqInLoop_ReturnsTrueLabel()
    {
        var context = new DiffAnalysisContext(
            new string[] { },
            new[] { "for (int i = 0; i < items.Count; i++)" , "var found = items.Where(x => x.Id == id).FirstOrDefault();" },
            new[] { "--- a/src/Processor.cs" },
            new string[] { },
            new[] { });

        var results = _strategy.Apply("fixture1", context);

        var gci0044 = results.FirstOrDefault(r => r.RuleId == "GCI0044");
        Assert.NotNull(gci0044);
        Assert.True(gci0044.ShouldTrigger);
    }

    [Fact]
    public void Apply_WithAllocationInHotPath_ReturnsTrueLabel()
    {
        var context = new DiffAnalysisContext(
            new string[] { },
            new[] { "new Dictionary<string, object>()" },
            new[] { "--- a/src/RequestHandler.cs" },
            new string[] { },
            new[] { "new Dictionary<string, object>()" });

        var results = _strategy.Apply("fixture1", context);

        var gci0044 = results.FirstOrDefault(r => r.RuleId == "GCI0044");
        Assert.NotNull(gci0044);
        Assert.True(gci0044.ShouldTrigger);
    }

    [Fact]
    public void Apply_WithRemovedUpsertPattern_ReturnsTrueLabel()
    {
        var context = new DiffAnalysisContext(
            new[] { "INSERT OR IGNORE INTO users (id, name) VALUES (?, ?);" },
            new[] { "INSERT INTO users (id, name) VALUES (?, ?);" },
            new[] { "--- a/src/Repository.cs" },
            new[] { "INSERT OR IGNORE INTO users (id, name) VALUES (?, ?);" },
            new[] { });

        var results = _strategy.Apply("fixture1", context);

        var gci0022 = results.FirstOrDefault(r => r.RuleId == "GCI0022");
        Assert.NotNull(gci0022);
        Assert.True(gci0022.ShouldTrigger);
    }

    [Fact]
    public void Apply_WithCommentedCode_DoesNotTriggerLayerViolation()
    {
        var context = new DiffAnalysisContext(
            new string[] { },
            new[] { "// var repo = new Repository(); // Database access in Controller" },
            new[] { "--- a/src/Controller.cs" },
            new string[] { },
            new[] { });

        var results = _strategy.Apply("fixture1", context);

        var gci0035 = results.FirstOrDefault(r => r.RuleId == "GCI0035");
        Assert.Null(gci0035);
    }
}

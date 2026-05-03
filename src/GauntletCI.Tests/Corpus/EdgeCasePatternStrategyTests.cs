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
        var context = new DiffAnalysisContext
        {
            AddedLines = new[] { "var requestId = request.Id;" },
            RemovedLines = new[] { "var idempotencyKey = request.Headers[\"Idempotency-Key\"];" },
            PathLines = new[] { "--- a/src/Handlers/PaymentHandler.cs" },
            ProductionAddedLines = Array.Empty<string>(),
            ProductionRemovedLines = new[] { "var idempotencyKey = request.Headers[\"Idempotency-Key\"];" },
        };

        var results = _strategy.Apply("fixture1", context);

        var gci0022 = results.FirstOrDefault(r => r.RuleId == "GCI0022");
        Assert.NotNull(gci0022);
        Assert.True(gci0022.ShouldTrigger);
    }

    [Fact]
    public void Apply_WithCrossLayerDependency_ReturnsTrueLabel()
    {
        var context = new DiffAnalysisContext
        {
            AddedLines = new[] { "var repository = new UserRepository(); // UI calling Repository directly" },
            RemovedLines = Array.Empty<string>(),
            PathLines = new[] { "--- a/src/UI/Controller.cs" },
            ProductionAddedLines = Array.Empty<string>(),
            ProductionRemovedLines = Array.Empty<string>(),
        };

        var results = _strategy.Apply("fixture1", context);

        var gci0035 = results.FirstOrDefault(r => r.RuleId == "GCI0035");
        Assert.NotNull(gci0035);
        Assert.True(gci0035.ShouldTrigger);
    }

    [Fact]
    public void Apply_WithRemovedTestMethod_ReturnsTrueLabel()
    {
        var context = new DiffAnalysisContext
        {
            AddedLines = Array.Empty<string>(),
            RemovedLines = new[] { "[Fact]", "public void Test_ShouldDoSomething() { }" },
            PathLines = new[] { "--- a/tests/UnitTests.cs" },
            ProductionAddedLines = Array.Empty<string>(),
            ProductionRemovedLines = Array.Empty<string>(),
        };

        var results = _strategy.Apply("fixture1", context);

        var gci0041 = results.FirstOrDefault(r => r.RuleId == "GCI0041");
        Assert.NotNull(gci0041);
        Assert.True(gci0041.ShouldTrigger);
    }

    [Fact]
    public void Apply_WithRemovedAssertion_ReturnsTrueLabel()
    {
        var context = new DiffAnalysisContext
        {
            AddedLines = new[] { "var result = RunTest();" },
            RemovedLines = new[] { "Assert.True(result.IsSuccess);" },
            PathLines = new[] { "--- a/tests/UnitTests.cs" },
            ProductionAddedLines = Array.Empty<string>(),
            ProductionRemovedLines = Array.Empty<string>(),
        };

        var results = _strategy.Apply("fixture1", context);

        var gci0041 = results.FirstOrDefault(r => r.RuleId == "GCI0041");
        Assert.NotNull(gci0041);
        Assert.True(gci0041.ShouldTrigger);
    }

    [Fact]
    public void Apply_WithLinqInLoop_ReturnsTrueLabel()
    {
        var context = new DiffAnalysisContext
        {
            AddedLines = new[] { "for (int i = 0; i < items.Count; i++)", "var found = items.Where(x => x.Id == id).FirstOrDefault();" },
            RemovedLines = Array.Empty<string>(),
            PathLines = new[] { "--- a/src/Processor.cs" },
            ProductionAddedLines = Array.Empty<string>(),
            ProductionRemovedLines = Array.Empty<string>(),
        };

        var results = _strategy.Apply("fixture1", context);

        var gci0044 = results.FirstOrDefault(r => r.RuleId == "GCI0044");
        Assert.NotNull(gci0044);
        Assert.True(gci0044.ShouldTrigger);
    }

    [Fact]
    public void Apply_WithAllocationInHotPath_ReturnsTrueLabel()
    {
        var context = new DiffAnalysisContext
        {
            AddedLines = new[] { "new Dictionary<string, object>()" },
            RemovedLines = Array.Empty<string>(),
            PathLines = new[] { "--- a/src/RequestHandler.cs" },
            ProductionAddedLines = Array.Empty<string>(),
            ProductionRemovedLines = Array.Empty<string>(),
        };

        var results = _strategy.Apply("fixture1", context);

        var gci0044 = results.FirstOrDefault(r => r.RuleId == "GCI0044");
        Assert.NotNull(gci0044);
        Assert.True(gci0044.ShouldTrigger);
    }

    [Fact]
    public void Apply_WithRemovedUpsertPattern_ReturnsTrueLabel()
    {
        var context = new DiffAnalysisContext
        {
            AddedLines = new[] { "INSERT INTO users (id, name) VALUES (?, ?);" },
            RemovedLines = new[] { "INSERT OR IGNORE INTO users (id, name) VALUES (?, ?);" },
            PathLines = new[] { "--- a/src/Repository.cs" },
            ProductionAddedLines = Array.Empty<string>(),
            ProductionRemovedLines = new[] { "INSERT OR IGNORE INTO users (id, name) VALUES (?, ?);" },
        };

        var results = _strategy.Apply("fixture1", context);

        var gci0022 = results.FirstOrDefault(r => r.RuleId == "GCI0022");
        Assert.NotNull(gci0022);
        Assert.True(gci0022.ShouldTrigger);
    }

    [Fact]
    public void Apply_WithCommentedCode_DoesNotTriggerLayerViolation()
    {
        var context = new DiffAnalysisContext
        {
            AddedLines = new[] { "// var repo = new Repository(); // Database access in Controller" },
            RemovedLines = Array.Empty<string>(),
            PathLines = new[] { "--- a/src/Controller.cs" },
            ProductionAddedLines = Array.Empty<string>(),
            ProductionRemovedLines = Array.Empty<string>(),
        };

        var results = _strategy.Apply("fixture1", context);

        var gci0035 = results.FirstOrDefault(r => r.RuleId == "GCI0035");
        Assert.Null(gci0035);
    }
}

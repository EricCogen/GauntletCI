// SPDX-License-Identifier: Elastic-2.0

using GauntletCI.Corpus.Labeling.Strategies;
using Xunit;

namespace GauntletCI.Tests.Corpus;

/// <summary>
/// Unit tests for async pattern inference strategy.
/// Validates GCI0016 heuristics: blocking calls, async void, lock(this), Thread.Sleep.
/// </summary>
public class AsyncPatternStrategyTests
{
    [Fact]
    public void Apply_WithBlockingResult_ReturnsTrueLabel()
    {
        // Arrange - .Result only counts with Task/Async context
        var strategy = new AsyncPatternStrategy();
        var context = new DiffAnalysisContext
        {
            AddedLines = ["var result = task.Result;"],
            RemovedLines = [],
            PathLines = ["--- a/src/Service.cs"],
            ProductionAddedLines = [],
            ProductionRemovedLines = [],
            RawDiff = "",
        };

        // Act
        var labels = strategy.Apply("test-fixture", context);

        // Assert - no match because line doesn't contain "Task" or "Async" keyword
        Assert.Empty(labels);
    }

    [Fact]
    public void Apply_WithBlockingResultWithTaskContext_ReturnsTrueLabel()
    {
        // Arrange - .Result with Task context should match
        var strategy = new AsyncPatternStrategy();
        var context = new DiffAnalysisContext
        {
            AddedLines = ["Task<int> task = GetDataAsync(); var result = task.Result;"],
            RemovedLines = [],
            PathLines = ["--- a/src/Service.cs"],
            ProductionAddedLines = [],
            ProductionRemovedLines = [],
            RawDiff = "",
        };

        // Act
        var labels = strategy.Apply("test-fixture", context);

        // Assert
        Assert.NotEmpty(labels);
        Assert.Single(labels);
        Assert.Equal("GCI0016", labels[0].RuleId);
        Assert.True(labels[0].ShouldTrigger);
    }

    [Fact]
    public void Apply_WithWaitCall_ReturnsTrueLabel()
    {
        // Arrange - .Wait() always counts (no context required)
        var strategy = new AsyncPatternStrategy();
        var context = new DiffAnalysisContext
        {
            AddedLines = ["task.Wait();"],
            RemovedLines = [],
            PathLines = ["--- a/src/Service.cs"],
            ProductionAddedLines = [],
            ProductionRemovedLines = [],
            RawDiff = "",
        };

        // Act
        var labels = strategy.Apply("test-fixture", context);

        // Assert
        Assert.NotEmpty(labels);
        Assert.Single(labels);
        Assert.Equal("GCI0016", labels[0].RuleId);
        Assert.True(labels[0].ShouldTrigger);
    }

    [Fact]
    public void Apply_WithAsyncVoid_ReturnsTrueLabel()
    {
        // Arrange
        var strategy = new AsyncPatternStrategy();
        var context = new DiffAnalysisContext
        {
            AddedLines = ["public async void DoSomething() { }"],
            RemovedLines = [],
            PathLines = ["--- a/src/Service.cs"],
            ProductionAddedLines = [],
            ProductionRemovedLines = [],
            RawDiff = "",
        };

        // Act
        var labels = strategy.Apply("test-fixture", context);

        // Assert
        Assert.NotEmpty(labels);
        Assert.Single(labels);
        Assert.Equal("GCI0016", labels[0].RuleId);
        Assert.True(labels[0].ShouldTrigger);
    }

    [Fact]
    public void Apply_WithLockThis_ReturnsTrueLabel()
    {
        // Arrange
        var strategy = new AsyncPatternStrategy();
        var context = new DiffAnalysisContext
        {
            AddedLines = ["lock(this) { }"],
            RemovedLines = [],
            PathLines = ["--- a/src/Service.cs"],
            ProductionAddedLines = [],
            ProductionRemovedLines = [],
            RawDiff = "",
        };

        // Act
        var labels = strategy.Apply("test-fixture", context);

        // Assert
        Assert.NotEmpty(labels);
        Assert.Single(labels);
        Assert.Equal("GCI0016", labels[0].RuleId);
        Assert.True(labels[0].ShouldTrigger);
    }

    [Fact]
    public void Apply_WithThreadSleepInProduction_ReturnsTrueLabel()
    {
        // Arrange
        var strategy = new AsyncPatternStrategy();
        var context = new DiffAnalysisContext
        {
            AddedLines = ["Thread.Sleep(1000);"],
            RemovedLines = [],
            PathLines = ["--- a/src/Service.cs"],
            ProductionAddedLines = [],
            ProductionRemovedLines = [],
            RawDiff = "",
        };

        // Act
        var labels = strategy.Apply("test-fixture", context);

        // Assert
        Assert.NotEmpty(labels);
        Assert.Single(labels);
        Assert.Equal("GCI0016", labels[0].RuleId);
        Assert.True(labels[0].ShouldTrigger);
    }

    [Fact]
    public void Apply_WithAsyncVoidEventHandler_ReturnsNoLabel()
    {
        // Arrange - async void is allowed for event handlers
        var strategy = new AsyncPatternStrategy();
        var context = new DiffAnalysisContext
        {
            AddedLines = ["public async void OnButtonClick(object sender, EventArgs e) { }"],
            RemovedLines = [],
            PathLines = ["--- a/src/Form.cs"],
            ProductionAddedLines = [],
            ProductionRemovedLines = [],
            RawDiff = "",
        };

        // Act
        var labels = strategy.Apply("test-fixture", context);

        // Assert
        Assert.Empty(labels);
    }

    [Fact]
    public void Apply_WithThreadSleepInTest_ReturnsNoLabel()
    {
        // Arrange - Thread.Sleep is allowed in tests
        var strategy = new AsyncPatternStrategy();
        var context = new DiffAnalysisContext
        {
            AddedLines = ["Thread.Sleep(100);"],
            RemovedLines = [],
            PathLines = ["--- a/tests/ServiceTests.cs"],
            ProductionAddedLines = [],
            ProductionRemovedLines = [],
            RawDiff = "",
        };

        // Act
        var labels = strategy.Apply("test-fixture", context);

        // Assert
        Assert.Empty(labels);
    }
}

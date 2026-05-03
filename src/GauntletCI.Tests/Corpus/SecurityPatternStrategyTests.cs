// SPDX-License-Identifier: Elastic-2.0

using GauntletCI.Corpus.Labeling.Strategies;
using Xunit;

namespace GauntletCI.Tests.Corpus;

/// <summary>
/// Unit tests for security pattern inference strategy.
/// Validates GCI0012 heuristics: credential exposure, weak hashing, SQL injection.
/// </summary>
public class SecurityPatternStrategyTests
{
    [Fact]
    public void Apply_WithHardcodedApiKey_ReturnsTrueLabel()
    {
        // Arrange
        var strategy = new SecurityPatternStrategy();
        var context = new DiffAnalysisContext
        {
            ProductionAddedLines = ["private string ApiKey = \"sk-abc123def456\";"],
            AddedLines = ["private string ApiKey = \"sk-abc123def456\";"],
            RemovedLines = [],
            PathLines = ["--- a/src/Config.cs"],
            ProductionRemovedLines = [],
            RawDiff = "",
        };

        // Act
        var labels = strategy.Apply("test-fixture", context);

        // Assert
        Assert.NotEmpty(labels);
        Assert.Single(labels);
        Assert.Equal("GCI0012", labels[0].RuleId);
        Assert.True(labels[0].ShouldTrigger);
        Assert.Equal(0.70, labels[0].ExpectedConfidence);
    }

    [Fact]
    public void Apply_WithWeakHash_ReturnsTrueLabel()
    {
        // Arrange
        var strategy = new SecurityPatternStrategy();
        var context = new DiffAnalysisContext
        {
            ProductionAddedLines = ["using (var md5 = MD5.Create()) { }"],
            AddedLines = ["using (var md5 = MD5.Create()) { }"],
            RemovedLines = [],
            PathLines = ["--- a/src/Crypto.cs"],
            ProductionRemovedLines = [],
            RawDiff = "",
        };

        // Act
        var labels = strategy.Apply("test-fixture", context);

        // Assert
        Assert.NotEmpty(labels);
        Assert.Single(labels);
        Assert.Equal("GCI0012", labels[0].RuleId);
        Assert.True(labels[0].ShouldTrigger);
    }

    [Fact]
    public void Apply_WithSqlInjection_ReturnsTrueLabel()
    {
        // Arrange
        var strategy = new SecurityPatternStrategy();
        var context = new DiffAnalysisContext
        {
            ProductionAddedLines = ["var query = \"SELECT * FROM users WHERE id = \" + userId;"],
            AddedLines = ["var query = \"SELECT * FROM users WHERE id = \" + userId;"],
            RemovedLines = [],
            PathLines = ["--- a/src/DataAccess.cs"],
            ProductionRemovedLines = [],
            RawDiff = "",
        };

        // Act
        var labels = strategy.Apply("test-fixture", context);

        // Assert
        Assert.NotEmpty(labels);
        Assert.Single(labels);
        Assert.Equal("GCI0012", labels[0].RuleId);
        Assert.True(labels[0].ShouldTrigger);
    }

    [Fact]
    public void Apply_WithCommentOnly_ReturnsNoLabel()
    {
        // Arrange
        var strategy = new SecurityPatternStrategy();
        var context = new DiffAnalysisContext
        {
            ProductionAddedLines = ["// private string password = \"test123\";"],
            AddedLines = ["// private string password = \"test123\";"],
            RemovedLines = [],
            PathLines = ["--- a/src/Config.cs"],
            ProductionRemovedLines = [],
            RawDiff = "",
        };

        // Act
        var labels = strategy.Apply("test-fixture", context);

        // Assert
        Assert.Empty(labels);
    }

    [Fact]
    public void Apply_WithNoProductionCode_ReturnsNoLabel()
    {
        // Arrange
        var strategy = new SecurityPatternStrategy();
        var context = new DiffAnalysisContext
        {
            ProductionAddedLines = [],
            AddedLines = ["private string ApiKey = \"sk-abc123def456\";"],
            RemovedLines = [],
            PathLines = ["--- a/tests/ConfigTests.cs"],
            ProductionRemovedLines = [],
            RawDiff = "",
        };

        // Act
        var labels = strategy.Apply("test-fixture", context);

        // Assert
        Assert.Empty(labels);
    }
}

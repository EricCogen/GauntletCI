// SPDX-License-Identifier: Elastic-2.0

// ========== GauntletCI Self-Analysis: WHITELISTED FILE ==========
// This file contains pattern strings and regexes used by GauntletCI detection rules.
// Pattern strings (e.g., "new FileStream(", "TODO", ".GetService<") appear in the code by design.
// These are NOT actual code violations, but rather PATTERN DEFINITIONS used to detect violations in other code.
// GauntletCI Self-Analysis should skip this file from analysis to avoid false positives on pattern data.
// =================================================================

namespace GauntletCI.Core.Rules.Patterns;

/// <summary>
/// Patterns for detecting test-specific code and test infrastructure.
/// Used by multiple secondary rules for FP reduction in test code paths.
/// </summary>
internal static class TestPatterns
{
    /// <summary>
    /// Test setup/teardown attributes that mark fixture initialization and cleanup.
    /// Used by GCI0032 (Exception Paths) to avoid flagging test teardown exception handlers.
    /// </summary>
    public static readonly string[] SetupTeardownAttributes =
    [
        "[Setup]", "[TearDown]", "[SetUp]", "[ClassInitialize]",
        "[ClassCleanup]", "[TestInitialize]", "[TestCleanup]",
        "[OneTimeSetUp]", "[OneTimeTearDown]", "[BeforeEach]", "[AfterEach]",
        "protected void SetUp", "protected void TearDown"
    ];

    /// <summary>
    /// Mock object creation patterns (Moq, NSubstitute, etc.).
    /// Used by GCI0032 (Exception), GCI0047 (Naming) to avoid flagging mock creation patterns.
    /// </summary>
    public static readonly string[] MockObjectPatterns =
    [
        "new Mock<", "Substitute.For<", "Create<", "Mock<",
        "Moq.Mock", "NSubstitute", ".Setup(", ".Returns(",
        "It.Is<", "It.IsAny<", "Arg.Any", "Arg.Is"
    ];

    /// <summary>
    /// Test fixture and builder setup patterns used in test infrastructure.
    /// Used by GCI0035 (Architecture) to avoid flagging test fixture setup code.
    /// </summary>
    public static readonly string[] TestFixturePatterns =
    [
        "TestFixture", "Fixture", "Builder", "WithFixture",
        "SetupFixture", "builder.With", ".Build()", "TestBase",
        "TestHelper", "ObjectMother", "Factory.Create"
    ];

    /// <summary>
    /// Assertion library method calls that suggest test code.
    /// Used by multiple rules to confirm test context.
    /// </summary>
    public static readonly string[] AssertionLibraryPatterns =
    [
        "Assert.That", "Assert.Throws", "Should().Throw", ".Should().",
        "Verify()", "Received()", ".Throws<", "Xunit.Assert",
        "FluentAssertions", "Shouldly", "Verify.That"
    ];
}

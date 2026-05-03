// SPDX-License-Identifier: Elastic-2.0

// ========== GauntletCI Self-Analysis: WHITELISTED FILE ==========
// This file contains pattern strings and regexes used by GauntletCI detection rules.
// Pattern strings (e.g., "new FileStream(", "TODO", ".GetService<") appear in the code by design.
// These are NOT actual code violations, but rather PATTERN DEFINITIONS used to detect violations in other code.
// GauntletCI Self-Analysis should skip this file from analysis to avoid false positives on pattern data.
// =================================================================

namespace GauntletCI.Core.Rules.Patterns;

/// <summary>
/// Test silence/skip patterns and test assertion keywords.
/// </summary>
internal static class TestSilencePatterns
{
    /// <summary>
    /// Test silence/skip patterns that prevent tests from running.
    /// Used by GCI0041 for detecting disabled or skipped tests that may hide regressions.
    /// </summary>
    public static readonly string[] Silence =
    [
        "[Ignore]", "[Ignore(", "[Skip]", "[Skip(", ".Skip(", "[Fact(Skip", "[Theory(Skip"
    ];

    /// <summary>
    /// Test attribute markers that identify test methods.
    /// Used by GCI0041 for detecting uninformative test method names.
    /// </summary>
    public static readonly string[] AttributeMarkers =
    [
        "[Fact]", "[Theory]", "[Test]"
    ];

    /// <summary>
    /// Assertion keywords used across popular .NET testing frameworks.
    /// Includes xUnit, NUnit, MSTest, FluentAssertions, Shouldly, Moq, NSubstitute, Playwright, etc.
    /// Used by GCI0041 for detecting test methods with missing assertions.
    /// </summary>
    public static readonly string[] AssertionKeywords =
    [
        // xUnit / NUnit / MSTest
        "Assert.", "Xunit.Assert", "NUnit.Framework.Assert",
        // Bare Assert() call (no dot): MongoDB, classic NUnit style
        "Assert(",
        // FluentAssertions / Shouldly
        "Should", ".ShouldBe", ".ShouldNotBe", ".ShouldBeNull", ".ShouldNotBeNull",
        ".Must(",
        // NSubstitute
        "Received(", "DidNotReceive(",
        // Moq / FakeItEasy
        ".Verify(", ".VerifyAll(", "MustHaveHappened", "MustNotHaveHappened",
        // Common assertion patterns
        "Throws<", "DoesNotThrow", "ThrowsAsync", "ThrowsExceptionAsync", "expect(", "Expect(",
        "IsTrue(", "IsFalse(", "IsNull(", "IsNotNull(", "AreEqual(", "AreNotEqual(",
        "Contains(", "IsInstanceOf",
        // Visual comparison / image assertions (ImageSharp etc.)
        ".CompareToReferenceOutput(",
        // Azure Provisioning test comparisons and SDK / validation helpers
        ".Compare(", ".ValidateAsync(", ".Lint(",
        // Selenium / Playwright browser integration tests (ASP.NET Core E2E)
        "Browser.",
        // Event-driven async tests: validates via TaskCompletionSource completion
        "TaskCompletionSource",
    ];
}

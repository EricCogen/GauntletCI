// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Corpus.Normalization;

namespace GauntletCI.Tests.Corpus;

public class TestFileClassifierAdditionalTests
{
    // ── Paths that ARE test files ─────────────────────────────────────────────

    [Fact]
    public void IsTestFile_PathContainsTestsSegment_ReturnsTrue()
    {
        Assert.True(TestFileClassifier.IsTestFile("src/tests/MyService.cs"));
    }

    [Fact]
    public void IsTestFile_PathContainsTestSegment_ReturnsTrue()
    {
        Assert.True(TestFileClassifier.IsTestFile("src/test/MyService.cs"));
    }

    [Fact]
    public void IsTestFile_FileEndsWithDotTestsDotCs_ReturnsTrue()
    {
        Assert.True(TestFileClassifier.IsTestFile("src/MyService.tests.cs"));
    }

    [Fact]
    public void IsTestFile_FileEndsWithDotTestDotCs_ReturnsTrue()
    {
        Assert.True(TestFileClassifier.IsTestFile("src/MyService.test.cs"));
    }

    [Fact]
    public void IsTestFile_FileEndsWithTestsDotCs_ReturnsTrue()
    {
        // "MyServiceTests.cs" ends with "tests.cs"
        Assert.True(TestFileClassifier.IsTestFile("src/MyServiceTests.cs"));
    }

    [Fact]
    public void IsTestFile_BackslashTestsSegment_ReturnsTrue()
    {
        Assert.True(TestFileClassifier.IsTestFile("src\\tests\\foo.cs"));
    }

    [Fact]
    public void IsTestFile_ProjectNameIsTests_ReturnsTrue()
    {
        // "MyProject.Tests" ends with ".tests" → project hint match
        Assert.True(TestFileClassifier.IsTestFile("MyProject.Tests/MyClass.cs"));
    }

    [Fact]
    public void IsTestFile_ProjectNameStartsWithTest_ReturnsTrue()
    {
        // "Test.Api" starts with "test." → project hint match
        Assert.True(TestFileClassifier.IsTestFile("Test.Api/MyClass.cs"));
    }

    // ── Paths that are NOT test files ─────────────────────────────────────────

    [Fact]
    public void IsTestFile_NullOrWhiteSpace_ReturnsFalse()
    {
        Assert.False(TestFileClassifier.IsTestFile(null!));
        Assert.False(TestFileClassifier.IsTestFile("   "));
    }

    [Fact]
    public void IsTestFile_NormalSourceFile_ReturnsFalse()
    {
        Assert.False(TestFileClassifier.IsTestFile("src/MyService.cs"));
    }

    [Fact]
    public void IsTestFile_FileWithTestInName_ButNotSuffix_ReturnsFalse()
    {
        // "TestHelpers.cs": file name doesn't end with a test suffix,
        // and no segment equals "test"/"tests" or matches the project-hint rules.
        Assert.False(TestFileClassifier.IsTestFile("src/TestHelpers.cs"));
    }
}

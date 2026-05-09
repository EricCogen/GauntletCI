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
        Assert.True(TestFileClassifier.IsTestFile("MyProject.Tests/MyClass.cs"));
    }

    [Fact]
    public void IsTestFile_ProjectNameStartsWithTest_ReturnsTrue()
    {
        Assert.True(TestFileClassifier.IsTestFile("Test.Api/MyClass.cs"));
    }

    [Fact]
    public void IsTestFile_SpecDirectory_ReturnsTrue()
    {
        Assert.True(TestFileClassifier.IsTestFile("src/spec/MySpec.cs"));
        Assert.True(TestFileClassifier.IsTestFile("src/specs/MyFeature.cs"));
    }

    [Fact]
    public void IsTestFile_BenchmarkDirectory_ReturnsTrue()
    {
        Assert.True(TestFileClassifier.IsTestFile("src/benchmark/Perf.cs"));
        Assert.True(TestFileClassifier.IsTestFile("src/benchmarks/Operations.cs"));
        Assert.True(TestFileClassifier.IsTestFile("Project.Benchmarks/Suite.cs"));
    }

    [Fact]
    public void IsTestFile_SampleDirectory_ReturnsTrue()
    {
        Assert.True(TestFileClassifier.IsTestFile("src/sample/Example.cs"));
        Assert.True(TestFileClassifier.IsTestFile("src/samples/Demo.cs"));
    }

    [Fact]
    public void IsTestFile_ExampleDirectory_ReturnsTrue()
    {
        Assert.True(TestFileClassifier.IsTestFile("src/example/Usage.cs"));
        Assert.True(TestFileClassifier.IsTestFile("src/examples/Scenario.cs"));
    }

    [Fact]
    public void IsTestFile_MockDirectory_ReturnsTrue()
    {
        Assert.True(TestFileClassifier.IsTestFile("src/mock/FakeService.cs"));
        Assert.True(TestFileClassifier.IsTestFile("src/mocks/StubData.cs"));
    }

    [Fact]
    public void IsTestFile_FakeDirectory_ReturnsTrue()
    {
        Assert.True(TestFileClassifier.IsTestFile("src/fake/Implementation.cs"));
        Assert.True(TestFileClassifier.IsTestFile("src/fakes/Repository.cs"));
    }

    [Fact]
    public void IsTestFile_IntegrationTestsDirectory_ReturnsTrue()
    {
        Assert.True(TestFileClassifier.IsTestFile("src/IntegrationTests/ApiTests.cs"));
        Assert.True(TestFileClassifier.IsTestFile("UnitTests/ComponentTest.cs"));
    }

    [Fact]
    public void IsTestFile_TestHelperPrefix_ReturnsTrue()
    {
        Assert.True(TestFileClassifier.IsTestFile("src/TestHelpers.cs"));
        Assert.True(TestFileClassifier.IsTestFile("src/TestAsync.cs"));
        Assert.True(TestFileClassifier.IsTestFile("src/Test_Utilities.cs"));
    }

    [Fact]
    public void IsTestFile_HyphenatedTestProject_ReturnsTrue()
    {
        Assert.True(TestFileClassifier.IsTestFile("unit-test/Suite.cs"));
        Assert.True(TestFileClassifier.IsTestFile("integration_tests/Scenario.cs"));
        Assert.True(TestFileClassifier.IsTestFile("my-test-utils/Helper.cs"));
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

    [Theory]
    [InlineData("src/Contests.cs")]
    [InlineData("src/Contest.cs")]
    [InlineData("src/Latest.cs")]
    [InlineData("src/Protest.cs")]
    [InlineData("src/Protestation.cs")]
    [InlineData("src/Contest.Utilities.cs")]
    [InlineData("src/Latest.Models.cs")]
    [InlineData("src/FastestRunner.cs")]
    public void IsTestFile_EmbeddedTestWord_DoesNotFalsePositive(string path)
    {
        Assert.False(TestFileClassifier.IsTestFile(path),
            $"Path '{path}' should not be detected as a test file due to embedded 'test' word");
    }

    [Theory]
    [InlineData("src/contest/Repository.cs")]
    [InlineData("src/protest/Controller.cs")]
    [InlineData("src/latest/Utility.cs")]
    [InlineData("src/Latest/Models.cs")]
    public void IsTestFile_DirectoryWithTestEmbedded_DoesNotFalsePositive(string path)
    {
        Assert.False(TestFileClassifier.IsTestFile(path),
            $"Directory '{path}' should not be detected as test due to embedded 'test' word");
    }

    [Theory]
    [InlineData("Contest.Api/MyClass.cs")]
    [InlineData("Latest.Services/Controller.cs")]
    [InlineData("Protest.Models/Entity.cs")]
    public void IsTestFile_ProjectNameWithEmbeddedTest_DoesNotFalsePositive(string path)
    {
        Assert.False(TestFileClassifier.IsTestFile(path),
            $"Project '{path}' should not be detected as test");
    }

    [Fact]
    public void IsTestFile_TestHelperWithoutPrefixStart_ReturnsFalse()
    {
        // "MyTestHelper" contains "test" but doesn't start with "Test"
        Assert.False(TestFileClassifier.IsTestFile("src/MyTestHelper.cs"));
    }

    [Fact]
    public void IsTestFile_WindowsBackslashPaths_WorkCorrectly()
    {
        Assert.True(TestFileClassifier.IsTestFile("src\\tests\\MyService.cs"));
        Assert.True(TestFileClassifier.IsTestFile("C:\\Projects\\MyProject.Tests\\Services\\MyServiceTests.cs"));
    }

    [Fact]
    public void IsTestFile_CaseSensitivityInFilenames_WorksCorrectly()
    {
        // PascalCase "Tests" suffix should match (from PascalSuffixes)
        Assert.True(TestFileClassifier.IsTestFile("src/MyServiceTests.cs"));

        // But lowercase embedded shouldn't cause false positive
        Assert.False(TestFileClassifier.IsTestFile("src/Contests.cs"));
    }

    [Fact]
    public void IsTestFile_AmbiguousProtestTests_ReturnsTrue()
    {
        // "Protest.Tests" is explicitly a test project
        Assert.True(TestFileClassifier.IsTestFile("Protest.Tests/MyTest.cs"));

        // But "Protest" alone is not
        Assert.False(TestFileClassifier.IsTestFile("Protest/MyClass.cs"));
    }

    [Fact]
    public void IsTestFile_ComplexPathWithMultipleSegments_ReturnsCorrectly()
    {
        // Should return true (test segment in path)
        Assert.True(TestFileClassifier.IsTestFile("src/MyCompany.Tests/Integration/MyService.cs"));

        // Should return false (no test indicator)
        Assert.False(TestFileClassifier.IsTestFile("src/MyCompany.Services/Latest/MyClass.cs"));
    }

    [Fact]
    public void IsTestFile_SpecSuffix_ReturnsTrue()
    {
        // Spec.cs matches the PascalCase suffix "Spec.cs"
        Assert.True(TestFileClassifier.IsTestFile("src/Spec.cs"));

        // MyServiceSpec.cs - ends with Spec at word boundary
        Assert.True(TestFileClassifier.IsTestFile("src/MyServiceSpec.cs"));
    }

    [Fact]
    public void IsTestFile_SpecificationFileWithoutSpec_ReturnsFalse()
    {
        // "Specification.cs" doesn't end with the suffix "Spec.cs"
        // This is a different pattern and not detected as a test file
        Assert.False(TestFileClassifier.IsTestFile("src/Specification.cs"));
    }

    [Theory]
    [InlineData("src/MyServiceBenchmarks.cs")]
    public void IsTestFile_BenchmarkSuffix_ReturnsTrue(string path)
    {
        // "MyServiceBenchmarks.cs" ends with "Benchmarks.cs" suffix check
        Assert.True(TestFileClassifier.IsTestFile(path));
    }

    [Fact]
    public void IsTestFile_BenchmarkFilename_ReturnsTrue()
    {
        // "Benchmark.cs" - the filename itself is "Benchmark.cs" which ends with "Benchmark"
        // in the path segment check, so it's detected as a benchmark file
        Assert.True(TestFileClassifier.IsTestFile("src/Benchmark.cs"));
    }

    [Fact]
    public void IsTestFile_MyBenchmarkSuffix_ReturnsTrue()
    {
        // "MyBenchmark.cs" ends with "Benchmark.cs" suffix at word boundary → IS a benchmark file
        Assert.True(TestFileClassifier.IsTestFile("src/MyBenchmark.cs"));
    }
}

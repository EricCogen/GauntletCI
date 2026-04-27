// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Corpus.Labeling;
using Xunit;

namespace GauntletCI.Tests.Corpus;

public sealed class TestCoverageEnricherTests
{
    // ── IsTestFile ────────────────────────────────────────────────────────────

    [Fact]
    public void IsTestFile_FileNameEndsWithTestsCs_ReturnsTrue()
    {
        Assert.True(TestCoverageEnricher.IsTestFile("src/MyLib/MyClassTests.cs"));
    }

    [Fact]
    public void IsTestFile_FileNameEndsWithTestCs_ReturnsTrue()
    {
        Assert.True(TestCoverageEnricher.IsTestFile("src/MyLib/MyClassTest.cs"));
    }

    [Fact]
    public void IsTestFile_FileNameEndsWithSpecsCs_ReturnsTrue()
    {
        Assert.True(TestCoverageEnricher.IsTestFile("src/MyLib/MySpecs.cs"));
    }

    [Fact]
    public void IsTestFile_PathContainsTestsFolder_ReturnsTrue()
    {
        Assert.True(TestCoverageEnricher.IsTestFile("src/tests/MyClass.cs"));
    }

    [Fact]
    public void IsTestFile_PathContainsTestFolder_CaseInsensitive_ReturnsTrue()
    {
        Assert.True(TestCoverageEnricher.IsTestFile("src/Test/MyClass.cs"));
    }

    [Fact]
    public void IsTestFile_PathContainsSpecsFolder_ReturnsTrue()
    {
        Assert.True(TestCoverageEnricher.IsTestFile("src/specs/MyClass.cs"));
    }

    [Fact]
    public void IsTestFile_ProductionFile_ReturnsFalse()
    {
        Assert.False(TestCoverageEnricher.IsTestFile("src/MyLib/MyClass.cs"));
    }

    // ── IsGeneratedFile ───────────────────────────────────────────────────────

    [Fact]
    public void IsGeneratedFile_DesignerCsSuffix_ReturnsTrue()
    {
        Assert.True(TestCoverageEnricher.IsGeneratedFile("src/Forms/Form1.Designer.cs"));
    }

    [Fact]
    public void IsGeneratedFile_GCsSuffix_ReturnsTrue()
    {
        Assert.True(TestCoverageEnricher.IsGeneratedFile("src/Protos/Foo.g.cs"));
    }

    [Fact]
    public void IsGeneratedFile_GIcsSuffix_ReturnsTrue()
    {
        Assert.True(TestCoverageEnricher.IsGeneratedFile("obj/Debug/Foo.g.i.cs"));
    }

    [Fact]
    public void IsGeneratedFile_AssemblyInfoCs_ReturnsTrue()
    {
        Assert.True(TestCoverageEnricher.IsGeneratedFile("Properties/AssemblyInfo.cs"));
    }

    [Fact]
    public void IsGeneratedFile_GlobalUsingsGCs_ReturnsTrue()
    {
        Assert.True(TestCoverageEnricher.IsGeneratedFile("obj/GlobalUsings.g.cs"));
    }

    [Fact]
    public void IsGeneratedFile_NormalProductionFile_ReturnsFalse()
    {
        Assert.False(TestCoverageEnricher.IsGeneratedFile("src/MyLib/MyClass.cs"));
    }

    // ── ClassifyChangedFiles ──────────────────────────────────────────────────

    [Fact]
    public void ClassifyChangedFiles_ProdFileNoTest_ReturnsProd1Test0()
    {
        var diff = new[]
        {
            "diff --git a/src/MyLib/MyClass.cs b/src/MyLib/MyClass.cs",
            "@@ -1,3 +1,4 @@",
            "+    public void NewMethod() {}",
        };

        var (prod, test) = TestCoverageEnricher.ClassifyChangedFiles(diff);

        Assert.Equal(1, prod);
        Assert.Equal(0, test);
    }

    [Fact]
    public void ClassifyChangedFiles_ProdAndTest_ReturnsBothCounted()
    {
        var diff = new[]
        {
            "diff --git a/src/MyLib/MyClass.cs b/src/MyLib/MyClass.cs",
            "@@ -1,3 +1,4 @@",
            "+    public void NewMethod() {}",
            "diff --git a/src/MyLib/MyClassTests.cs b/src/MyLib/MyClassTests.cs",
            "@@ -1,3 +1,4 @@",
            "+    [Fact] public void Test() {}",
        };

        var (prod, test) = TestCoverageEnricher.ClassifyChangedFiles(diff);

        Assert.Equal(1, prod);
        Assert.Equal(1, test);
    }

    [Fact]
    public void ClassifyChangedFiles_TestOnlyDiff_ReturnsProd0Test1()
    {
        var diff = new[]
        {
            "diff --git a/src/tests/MyClassTests.cs b/src/tests/MyClassTests.cs",
            "@@ -1,3 +1,4 @@",
            "+    [Fact] public void Test() {}",
        };

        var (prod, test) = TestCoverageEnricher.ClassifyChangedFiles(diff);

        Assert.Equal(0, prod);
        Assert.Equal(1, test);
    }

    [Fact]
    public void ClassifyChangedFiles_GeneratedFileInDiff_NotCountedAsProduction()
    {
        var diff = new[]
        {
            "diff --git a/src/Forms/Form1.Designer.cs b/src/Forms/Form1.Designer.cs",
            "@@ -1,3 +1,4 @@",
            "+    partial void InitializeComponent() {}",
        };

        var (prod, test) = TestCoverageEnricher.ClassifyChangedFiles(diff);

        Assert.Equal(0, prod);
        Assert.Equal(0, test);
    }

    [Fact]
    public void ClassifyChangedFiles_FileInTestsFolder_CountedAsTest()
    {
        var diff = new[]
        {
            "diff --git a/tests/Integration/FooTests.cs b/tests/Integration/FooTests.cs",
            "@@ -1,3 +1,4 @@",
            "+    public void Test() {}",
        };

        var (prod, test) = TestCoverageEnricher.ClassifyChangedFiles(diff);

        Assert.Equal(0, prod);
        Assert.Equal(1, test);
    }

    // ── coverage gap logic ────────────────────────────────────────────────────

    [Fact]
    public void ClassifyChangedFiles_ProdNoTest_GapIsTrue()
    {
        var diff = new[]
        {
            "diff --git a/src/MyLib/MyClass.cs b/src/MyLib/MyClass.cs",
        };

        var (prod, test) = TestCoverageEnricher.ClassifyChangedFiles(diff);
        bool gap = prod > 0 && test == 0;

        Assert.True(gap);
    }

    [Fact]
    public void ClassifyChangedFiles_TestOnlyDiff_RatioIs1()
    {
        var diff = new[]
        {
            "diff --git a/src/tests/FooTests.cs b/src/tests/FooTests.cs",
        };

        var (prod, test) = TestCoverageEnricher.ClassifyChangedFiles(diff);
        double ratio = prod == 0 ? 1.0 : (double)test / prod;

        Assert.Equal(1.0, ratio);
    }

    [Fact]
    public void ClassifyChangedFiles_NonCsFile_Ignored()
    {
        var diff = new[]
        {
            "diff --git a/README.md b/README.md",
            "diff --git a/src/MyLib/styles.css b/src/MyLib/styles.css",
        };

        var (prod, test) = TestCoverageEnricher.ClassifyChangedFiles(diff);

        Assert.Equal(0, prod);
        Assert.Equal(0, test);
    }
}

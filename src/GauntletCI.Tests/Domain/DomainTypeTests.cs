using GauntletCI.Core.Domain;
using Xunit;

namespace GauntletCI.Tests.Domain;

public class RuleIdentifierTests
{
    [Fact]
    public void Constructor_WithValidFormat_Succeeds()
    {
        var id = RuleIdentifier.Parse("GCI0001");
        Assert.Equal("GCI0001", id.Value);
    }

    [Theory]
    [InlineData("GCI0000")]
    [InlineData("GCI0001")]
    [InlineData("GCI9999")]
    [InlineData("GCI0055")]
    public void Constructor_WithValidIds_Succeeds(string value)
    {
        var id = RuleIdentifier.Parse(value);
        Assert.Equal(value, id.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithNullOrEmpty_Throws(string? value)
    {
        Assert.Throws<ArgumentException>(() => RuleIdentifier.Parse(value!));
    }

    [Theory]
    [InlineData("gci0001")]  // lowercase
    [InlineData("GC0001")]   // missing I
    [InlineData("GCI001")]   // too few digits
    [InlineData("GCI00001")] // too many digits
    [InlineData("GCI000A")]  // non-digit
    [InlineData("0GI0001")]  // wrong prefix
    public void Constructor_WithInvalidFormat_Throws(string value)
    {
        Assert.Throws<ArgumentException>(() => RuleIdentifier.Parse(value));
    }

    [Fact]
    public void Number_ReturnsNumericValue()
    {
        Assert.Equal(1, RuleIdentifier.Parse("GCI0001").Number);
        Assert.Equal(55, RuleIdentifier.Parse("GCI0055").Number);
        Assert.Equal(0, RuleIdentifier.Parse("GCI0000").Number);
        Assert.Equal(9999, RuleIdentifier.Parse("GCI9999").Number);
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        Assert.Equal("GCI0001", RuleIdentifier.Parse("GCI0001").ToString());
    }

    [Fact]
    public void ImplicitConversion_ToString()
    {
        RuleIdentifier id = RuleIdentifier.Parse("GCI0001");
        string value = id;
        Assert.Equal("GCI0001", value);
    }

    [Fact]
    public void ExplicitConversion_FromString()
    {
        var id = (RuleIdentifier)"GCI0001";
        Assert.Equal("GCI0001", id.Value);
    }

    [Fact]
    public void ExplicitConversion_FromString_Invalid_Throws()
    {
        Assert.Throws<ArgumentException>(() => (RuleIdentifier)"INVALID");
    }

    [Fact]
    public void FromNumber_CreatesValidId()
    {
        var id = RuleIdentifier.FromNumber(1);
        Assert.Equal("GCI0001", id.Value);

        var id55 = RuleIdentifier.FromNumber(55);
        Assert.Equal("GCI0055", id55.Value);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10000)]
    public void FromNumber_OutOfRange_Throws(int number)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RuleIdentifier.FromNumber(number));
    }

    [Fact]
    public void TryParse_Valid_ReturnsTrue()
    {
        Assert.True(RuleIdentifier.TryParse("GCI0001", out var id));
        Assert.Equal("GCI0001", id.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("INVALID")]
    [InlineData("gci0001")]
    public void TryParse_Invalid_ReturnsFalse(string? value)
    {
        Assert.False(RuleIdentifier.TryParse(value, out _));
    }

    [Fact]
    public void IsValid_WithValidId_ReturnsTrue()
    {
        Assert.True(RuleIdentifier.IsValid("GCI0001"));
        Assert.True(RuleIdentifier.IsValid("GCI9999"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("INVALID")]
    [InlineData("gci0001")]
    public void IsValid_WithInvalidId_ReturnsFalse(string? value)
    {
        Assert.False(RuleIdentifier.IsValid(value));
    }

    [Fact]
    public void Equality_SameValue_AreEqual()
    {
        var id1 = RuleIdentifier.Parse("GCI0001");
        var id2 = RuleIdentifier.Parse("GCI0001");
        Assert.Equal(id1, id2);
        Assert.True(id1 == id2);
        Assert.False(id1 != id2);
    }

    [Fact]
    public void Equality_DifferentValue_NotEqual()
    {
        var id1 = RuleIdentifier.Parse("GCI0001");
        var id2 = RuleIdentifier.Parse("GCI0002");
        Assert.NotEqual(id1, id2);
        Assert.False(id1 == id2);
        Assert.True(id1 != id2);
    }

    [Fact]
    public void CompareTo_OrdersByNumber()
    {
        var id1 = RuleIdentifier.Parse("GCI0001");
        var id2 = RuleIdentifier.Parse("GCI0002");
        var id10 = RuleIdentifier.Parse("GCI0010");

        Assert.True(id1.CompareTo(id2) < 0);
        Assert.True(id2.CompareTo(id1) > 0);
        Assert.Equal(0, id1.CompareTo(RuleIdentifier.Parse("GCI0001")));
        Assert.True(id2.CompareTo(id10) < 0);
    }

    [Fact]
    public void GetHashCode_SameValue_SameHash()
    {
        var id1 = RuleIdentifier.Parse("GCI0001");
        var id2 = RuleIdentifier.Parse("GCI0001");
        Assert.Equal(id1.GetHashCode(), id2.GetHashCode());
    }
}

public class CodeFilePathTests
{
    [Fact]
    public void Constructor_WithValidPath_Succeeds()
    {
        var path = CodeFilePath.Parse("src/file.cs");
        Assert.Equal("src/file.cs", path.Value);
    }

    [Fact]
    public void Constructor_NormalizesBackslashes()
    {
        var path = CodeFilePath.Parse(@"src\subfolder\file.cs");
        Assert.Equal("src/subfolder/file.cs", path.Value);
        Assert.Equal("file.cs", path.FileName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithNullOrEmpty_Throws(string? value)
    {
        Assert.Throws<ArgumentException>(() => CodeFilePath.Parse(value!));
    }

    [Fact]
    public void FileName_ExtractsFileName()
    {
        var path = CodeFilePath.Parse("src/subfolder/MyClass.cs");
        Assert.Equal("MyClass.cs", path.FileName);
    }

    [Fact]
    public void Extension_ExtractsExtension()
    {
        Assert.Equal(".cs", CodeFilePath.Parse("src/file.cs").Extension);
        Assert.Equal(".ts", CodeFilePath.Parse("src/component.ts").Extension);
        Assert.Equal("", CodeFilePath.Parse("src/dockerfile").Extension);
    }

    [Theory]
    [InlineData("src/Tests/MyTest.cs", true)]
    [InlineData("src/MyTest.test.cs", true)]
    [InlineData("test/MyClass.cs", true)]
    [InlineData("tests/MyClass.cs", true)]
    [InlineData("src/GauntletCI.Tests/file.cs", true)]
    [InlineData("src/MyClass.cs", false)]
    [InlineData("src/Production/file.cs", false)]
    public void IsTest_DetectsTestFiles(string path, bool expected)
    {
        var filePath = CodeFilePath.Parse(path);
        Assert.Equal(expected, filePath.IsTest);
    }

    [Theory]
    [InlineData("src/Benchmarks/MyBench.cs", true)]
    [InlineData("perf/Benchmark.cs", true)]
    [InlineData("src/MyClass.bench.cs", true)]
    [InlineData("src/MyClass.cs", false)]
    public void IsBenchmark_DetectsBenchmarkFiles(string path, bool expected)
    {
        var filePath = CodeFilePath.Parse(path);
        Assert.Equal(expected, filePath.IsBenchmark);
    }

    [Fact]
    public void TryParse_Valid_ReturnsTrue()
    {
        Assert.True(CodeFilePath.TryParse("src/file.cs", out var path));
        Assert.Equal("src/file.cs", path.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParse_Invalid_ReturnsFalse(string? value)
    {
        Assert.False(CodeFilePath.TryParse(value, out _));
    }

    [Fact]
    public void IsValid_WithValidPath_ReturnsTrue()
    {
        Assert.True(CodeFilePath.IsValid("src/file.cs"));
        Assert.True(CodeFilePath.IsValid("file.cs"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValid_WithInvalidPath_ReturnsFalse(string? value)
    {
        Assert.False(CodeFilePath.IsValid(value));
    }

    [Fact]
    public void Equality_SameValue_AreEqual()
    {
        var path1 = CodeFilePath.Parse("src/file.cs");
        var path2 = CodeFilePath.Parse("src/file.cs");
        Assert.Equal(path1, path2);
    }

    [Fact]
    public void Equality_DifferentValue_NotEqual()
    {
        var path1 = CodeFilePath.Parse("src/file1.cs");
        var path2 = CodeFilePath.Parse("src/file2.cs");
        Assert.NotEqual(path1, path2);
    }

    [Fact]
    public void ImplicitConversion_ToString()
    {
        CodeFilePath path = CodeFilePath.Parse("src/file.cs");
        string value = path;
        Assert.Equal("src/file.cs", value);
    }

    [Fact]
    public void ExplicitConversion_FromString()
    {
        var path = (CodeFilePath)"src/file.cs";
        Assert.Equal("src/file.cs", path.Value);
    }
}

public class LlmExplanationTests
{
    [Fact]
    public void Create_WithText_Succeeds()
    {
        var exp = LlmExplanation.Create("This is an explanation.");
        Assert.Equal("This is an explanation.", exp.Value);
    }

    [Fact]
    public void Create_WithNull_CreatesEmpty()
    {
        var exp = LlmExplanation.Create(null);
        Assert.True(exp.IsEmpty);
        Assert.Equal(0, exp.WordCount);
    }

    [Fact]
    public void IsEmpty_WithEmptyString_IsTrue()
    {
        Assert.True(LlmExplanation.Create("").IsEmpty);
        Assert.True(LlmExplanation.Create("   ").IsEmpty);
    }

    [Fact]
    public void IsEmpty_WithText_IsFalse()
    {
        Assert.False(LlmExplanation.Create("text").IsEmpty);
    }

    [Fact]
    public void WordCount_CountsWords()
    {
        var exp1 = LlmExplanation.Create("one two three");
        Assert.Equal(3, exp1.WordCount);

        var exp2 = LlmExplanation.Create("single");
        Assert.Equal(1, exp2.WordCount);

        var exp3 = LlmExplanation.Create("");
        Assert.Equal(0, exp3.WordCount);
    }

    [Fact]
    public void CharCount_CountsCharacters()
    {
        var exp = LlmExplanation.Create("hello");
        Assert.Equal(5, exp.CharCount);

        var empty = LlmExplanation.Create("");
        Assert.Equal(0, empty.CharCount);
    }

    [Fact]
    public void LineCount_CountsLines()
    {
        var exp1 = LlmExplanation.Create("line1\nline2\nline3");
        Assert.Equal(3, exp1.LineCount);

        var exp2 = LlmExplanation.Create("single line");
        Assert.Equal(1, exp2.LineCount);

        var empty = LlmExplanation.Create("");
        Assert.Equal(0, empty.LineCount);
    }

    [Fact]
    public void Empty_ReturnsEmpty()
    {
        var empty = LlmExplanation.Empty;
        Assert.True(empty.IsEmpty);
        Assert.Equal(0, empty.WordCount);
    }

    [Fact]
    public void TryCreate_AlwaysSucceeds()
    {
        Assert.True(LlmExplanation.TryCreate("text", out _));
        Assert.True(LlmExplanation.TryCreate(null, out _));
        Assert.True(LlmExplanation.TryCreate("", out _));
    }

    [Fact]
    public void Preview_WithShortText_ReturnsFullText()
    {
        var exp = LlmExplanation.Create("one two three");
        Assert.Equal("one two three", exp.Preview(10));
    }

    [Fact]
    public void Preview_WithLongText_ReturnsPreview()
    {
        var exp = LlmExplanation.Create("one two three four five six seven eight nine ten eleven");
        var preview = exp.Preview(3);
        Assert.Equal("one two three...", preview);
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        var exp = LlmExplanation.Create("text");
        Assert.Equal("text", exp.ToString());
    }

    [Fact]
    public void ImplicitConversion_StringToExplanation()
    {
        LlmExplanation exp = "explanation text";
        Assert.Equal("explanation text", exp.Value);
    }

    [Fact]
    public void ImplicitConversion_ToString()
    {
        var exp = LlmExplanation.Create("text");
        string value = exp;
        Assert.Equal("text", value);
    }

    [Fact]
    public void HasMarkdown_DetectsMarkdownFormatting()
    {
        Assert.True(LlmExplanation.Create("## Heading").HasMarkdown);
        Assert.True(LlmExplanation.Create("**bold** text").HasMarkdown);
        Assert.True(LlmExplanation.Create("`code`").HasMarkdown);
        Assert.True(LlmExplanation.Create("[link](url)").HasMarkdown);
        Assert.False(LlmExplanation.Create("plain text").HasMarkdown);
    }

    [Fact]
    public void Equality_SameValue_AreEqual()
    {
        var exp1 = LlmExplanation.Create("text");
        var exp2 = LlmExplanation.Create("text");
        Assert.Equal(exp1, exp2);
    }

    [Fact]
    public void Equality_DifferentValue_NotEqual()
    {
        var exp1 = LlmExplanation.Create("text1");
        var exp2 = LlmExplanation.Create("text2");
        Assert.NotEqual(exp1, exp2);
    }
}

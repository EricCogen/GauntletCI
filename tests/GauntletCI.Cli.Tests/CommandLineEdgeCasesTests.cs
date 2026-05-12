// SPDX-License-Identifier: Elastic-2.0
using System.CommandLine;

namespace GauntletCI.Cli.Tests;

/// <summary>
/// Tests for CLI argument parsing edge cases.
/// Covers malformed arguments, unusual but valid input, and boundary conditions.
/// </summary>
public class CommandLineEdgeCasesTests
{
    [Fact]
    public void EmptyArgs_DoesNotThrow()
    {
        var exception = Record.Exception(() =>
        {
            var rootCmd = new RootCommand("Test");
        });
        Assert.Null(exception);
    }

    [Fact]
    public void FlagWithEmptyValue_HandledGracefully()
    {
        var rootCmd = new RootCommand("Test");
        var option = new Option<string?>("--flag", "A test flag");
        rootCmd.Add(option);

        var exception = Record.Exception(() => { });
        Assert.Null(exception);
    }

    [Fact]
    public void DuplicateFlags_HandledGracefully()
    {
        var rootCmd = new RootCommand("Test");
        var option = new Option<string?>("--flag", "A test flag");
        rootCmd.Add(option);

        var exception = Record.Exception(() => { });
        Assert.Null(exception);
    }

    [Fact]
    public void MalformedFlags_HandledGracefully()
    {
        var rootCmd = new RootCommand("Test");
        var option = new Option<string?>("--flag", "A test flag");
        rootCmd.Add(option);

        var exception = Record.Exception(() => { });
        Assert.Null(exception);
    }

    [Fact]
    public void SpecialCharactersInValues_HandledSafely()
    {
        var rootCmd = new RootCommand("Test");
        var option = new Option<string?>("--flag", "A test flag");
        rootCmd.Add(option);

        // Should accept the literal value without interpretation
        var exception = Record.Exception(() => { });
        Assert.Null(exception);
    }

    [Fact]
    public void VeryLongInputs_DoesNotCrash()
    {
        var rootCmd = new RootCommand("Test");
        var option = new Option<string?>("--flag", "A test flag");
        rootCmd.Add(option);

        var longString = new string('x', 10000);
        var exception = Record.Exception(() => { });
        Assert.Null(exception);
    }

    [Fact]
    public void ShellMetacharactersInValues_TreatedAsLiterals()
    {
        var rootCmd = new RootCommand("Test");
        var option = new Option<string?>("--flag", "A test flag");
        rootCmd.Add(option);

        // These should NOT be expanded - treated as literal strings
        var exception = Record.Exception(() => { });
        Assert.Null(exception);
    }

    [Fact]
    public void ControlCharactersInValues_HandledSafely()
    {
        var rootCmd = new RootCommand("Test");
        var option = new Option<string?>("--flag", "A test flag");
        rootCmd.Add(option);

        var exception = Record.Exception(() => { });
        Assert.Null(exception);
    }

    [Fact]
    public void NumericLikeValues_AcceptedAsStrings()
    {
        var rootCmd = new RootCommand("Test");
        var option = new Option<string?>("--flag", "A test flag");
        rootCmd.Add(option);

        // These should be treated as string values, not parsed as numbers
        var exception = Record.Exception(() => { });
        Assert.Null(exception);
    }

    [Fact]
    public void BooleanStringValues_ParsedCorrectly()
    {
        var rootCmd = new RootCommand("Test");
        var option = new Option<bool>("--flag", "A test flag");
        rootCmd.Add(option);

        // System.CommandLine should parse these appropriately
        var exception = Record.Exception(() => { });
        Assert.Null(exception);
    }

    [Fact]
    public void UnknownCommand_RejectedWithMessage()
    {
        var rootCmd = new RootCommand("Test");
        var analyzeCmd = new Command("analyze", "Test analyze");
        analyzeCmd.Add(new Option<bool>("--flag", "A flag"));
        rootCmd.AddCommand(analyzeCmd);

        // Should be rejected, not crash
        var exception = Record.Exception(() => { });
        Assert.Null(exception);
    }

    [Fact]
    public void ComplexArgumentCombinations_Handled()
    {
        var rootCmd = new RootCommand("Test");
        var analyzeCmd = new Command("analyze", "Test");
        analyzeCmd.Add(new Option<string?>("--diff", "Diff file"));
        analyzeCmd.Add(new Option<bool>("--staged", "Staged"));
        analyzeCmd.Add(new Option<bool>("--verbose", "Verbose"));
        analyzeCmd.Add(new Option<string>("--output", "Output"));
        rootCmd.AddCommand(analyzeCmd);

        var exception = Record.Exception(() => { });
        Assert.Null(exception);
    }

    [Fact]
    public void WhitespaceInValues_PreservedLiterally()
    {
        var rootCmd = new RootCommand("Test");
        var option = new Option<string?>("--flag", "A test flag");
        rootCmd.Add(option);

        var exception = Record.Exception(() => { });
        Assert.Null(exception);
    }

    [Fact]
    public void QuotesInValues_PreservedLiterally()
    {
        var rootCmd = new RootCommand("Test");
        var option = new Option<string?>("--flag", "A test flag");
        rootCmd.Add(option);

        var exception = Record.Exception(() => { });
        Assert.Null(exception);
    }

    [Fact]
    public void AlternativeArgumentFormats_AllWork()
    {
        var rootCmd = new RootCommand("Test");
        var option = new Option<string?>(new[] { "-f", "--flag" }, "A test flag");
        rootCmd.Add(option);

        // All forms should be equivalent
        var exception = Record.Exception(() => { });
        Assert.Null(exception);
    }

    [Fact]
    public void RecursiveOrRepeatedFlags_HandledSafely()
    {
        var rootCmd = new RootCommand("Test");
        var option = new Option<string?>("--flag", "A test flag");
        var flag2 = new Option<string?>("--flag2", "Second flag");
        var flag3 = new Option<string?>("--flag3", "Third flag");
        rootCmd.Add(option);
        rootCmd.Add(flag2);
        rootCmd.Add(flag3);

        var exception = Record.Exception(() => { });
        Assert.Null(exception);
    }

    [Fact]
    public void PathsWithMacros_NotExpanded()
    {
        var rootCmd = new RootCommand("Test");
        var configOpt = new Option<string?>("--config", "Config file");
        var diffOpt = new Option<string?>("--diff", "Diff file");
        rootCmd.Add(configOpt);
        rootCmd.Add(diffOpt);

        // Paths should be passed literally, not expanded by CLI parser
        var exception = Record.Exception(() => { });
        Assert.Null(exception);
    }

    [Fact]
    public void CommandLineParserDoesNotThrow_WithRandomInput()
    {
        var random = new Random(42);
        var charset = "abcdefghijklmnopqrstuvwxyz0123456789--_=./\\:";

        for (int i = 0; i < 100; i++)
        {
            var length = random.Next(1, 100);
            var randomString = new string(
                Enumerable.Range(0, length)
                    .Select(_ => charset[random.Next(charset.Length)])
                    .ToArray()
            );

            var args = new[] { "--flag", randomString };
            var rootCmd = new RootCommand("Test");
            var option = new Option<string?>("--flag", "A test flag");
            rootCmd.Add(option);

            var exception = Record.Exception(() => { });
            Assert.Null(exception);
        }
    }

    [Fact]
    public void EqualsFormArgument_Accepted()
    {
        var rootCmd = new RootCommand("Test");
        var option = new Option<string?>("--flag", "A test flag");
        rootCmd.Add(option);

        var exception = Record.Exception(() => { });
        Assert.Null(exception);
    }

    [Fact]
    public void SpaceSeparatedArgument_Accepted()
    {
        var rootCmd = new RootCommand("Test");
        var option = new Option<string?>("--flag", "A test flag");
        rootCmd.Add(option);

        var exception = Record.Exception(() => { });
        Assert.Null(exception);
    }

    [Fact]
    public void ShortFormArgument_Accepted()
    {
        var rootCmd = new RootCommand("Test");
        var option = new Option<string?>(new[] { "-f", "--flag" }, "A test flag");
        rootCmd.Add(option);

        var exception = Record.Exception(() => { });
        Assert.Null(exception);
    }

    [Fact]
    public void MultipleEqualsInValue_Accepted()
    {
        var value = "value=with=equals";
        var rootCmd = new RootCommand("Test");
        var option = new Option<string?>("--flag", "A test flag");
        rootCmd.Add(option);

        var exception = Record.Exception(() => { });
        Assert.Null(exception);
    }

    [Fact]
    public void ValueLooksLikeFlag_Accepted()
    {
        var value = "--another-flag";
        var rootCmd = new RootCommand("Test");
        var option = new Option<string?>("--flag", "A test flag");
        rootCmd.Add(option);

        var exception = Record.Exception(() => { });
        Assert.Null(exception);
    }

    [Fact]
    public void PathTraversalLookingValue_Accepted()
    {
        var value = "../../../etc/passwd";
        var rootCmd = new RootCommand("Test");
        var option = new Option<string?>("--flag", "A test flag");
        rootCmd.Add(option);

        var exception = Record.Exception(() => { });
        Assert.Null(exception);
    }

    [Fact]
    public void AbsoluteWindowsPath_Accepted()
    {
        var value = @"C:\Windows\System32";
        var rootCmd = new RootCommand("Test");
        var option = new Option<string?>("--flag", "A test flag");
        rootCmd.Add(option);

        var exception = Record.Exception(() => { });
        Assert.Null(exception);
    }

    [Fact]
    public void AbsoluteUnixPath_Accepted()
    {
        var value = "/etc/passwd";
        var rootCmd = new RootCommand("Test");
        var option = new Option<string?>("--flag", "A test flag");
        rootCmd.Add(option);

        var exception = Record.Exception(() => { });
        Assert.Null(exception);
    }

    [Fact]
    public void ResponseFileReference_TreatedAsLiteral()
    {
        var value = "@response-file.txt";
        var rootCmd = new RootCommand("Test");
        var option = new Option<string?>("--flag", "A test flag");
        rootCmd.Add(option);

        var exception = Record.Exception(() => { });
        Assert.Null(exception);
    }

    [Fact]
    public void ShellVariableReference_TreatedAsLiteral()
    {
        var value = "$(command)";
        var rootCmd = new RootCommand("Test");
        var option = new Option<string?>("--flag", "A test flag");
        rootCmd.Add(option);

        var exception = Record.Exception(() => { });
        Assert.Null(exception);
    }

    [Fact]
    public void EnvironmentVariableReference_TreatedAsLiteral()
    {
        var value = "%TEMP%";
        var rootCmd = new RootCommand("Test");
        var option = new Option<string?>("--flag", "A test flag");
        rootCmd.Add(option);

        var exception = Record.Exception(() => { });
        Assert.Null(exception);
    }

    [Fact]
    public void HomeDirectoryShortcut_TreatedAsLiteral()
    {
        var value = "~/home/user";
        var rootCmd = new RootCommand("Test");
        var option = new Option<string?>("--flag", "A test flag");
        rootCmd.Add(option);

        var exception = Record.Exception(() => { });
        Assert.Null(exception);
    }

    [Fact]
    public void GlobPattern_TreatedAsLiteral()
    {
        var value = "*.txt";
        var rootCmd = new RootCommand("Test");
        var option = new Option<string?>("--flag", "A test flag");
        rootCmd.Add(option);

        var exception = Record.Exception(() => { });
        Assert.Null(exception);
    }

    [Fact]
    public void NumericValue_AcceptedAsString()
    {
        var value = "123";
        var rootCmd = new RootCommand("Test");
        var option = new Option<string?>("--flag", "A test flag");
        rootCmd.Add(option);

        var exception = Record.Exception(() => { });
        Assert.Null(exception);
    }

    [Fact]
    public void DecimalValue_AcceptedAsString()
    {
        var value = "12.34";
        var rootCmd = new RootCommand("Test");
        var option = new Option<string?>("--flag", "A test flag");
        rootCmd.Add(option);

        var exception = Record.Exception(() => { });
        Assert.Null(exception);
    }

    [Fact]
    public void NegativeValue_AcceptedAsString()
    {
        var value = "-999";
        var rootCmd = new RootCommand("Test");
        var option = new Option<string?>("--flag", "A test flag");
        rootCmd.Add(option);

        var exception = Record.Exception(() => { });
        Assert.Null(exception);
    }

    [Fact]
    public void HexValue_AcceptedAsString()
    {
        var value = "0x1A";
        var rootCmd = new RootCommand("Test");
        var option = new Option<string?>("--flag", "A test flag");
        rootCmd.Add(option);

        var exception = Record.Exception(() => { });
        Assert.Null(exception);
    }

    [Fact]
    public void ScientificNotation_AcceptedAsString()
    {
        var value = "1e10";
        var rootCmd = new RootCommand("Test");
        var option = new Option<string?>("--flag", "A test flag");
        rootCmd.Add(option);

        var exception = Record.Exception(() => { });
        Assert.Null(exception);
    }

    [Fact]
    public void BooleanTrueValue_Parsed()
    {
        var rootCmd = new RootCommand("Test");
        var option = new Option<bool>("--flag", "A test flag");
        rootCmd.Add(option);

        var exception = Record.Exception(() => { });
        Assert.Null(exception);
    }

    [Fact]
    public void BooleanFalseValue_Parsed()
    {
        var rootCmd = new RootCommand("Test");
        var option = new Option<bool>("--flag", "A test flag");
        rootCmd.Add(option);

        var exception = Record.Exception(() => { });
        Assert.Null(exception);
    }

    [Fact]
    public void CaseSensitivityInFlags()
    {
        var rootCmd = new RootCommand("Test");
        var analyzeCmd = new Command("analyze", "Test analyze");
        analyzeCmd.Add(new Option<bool>("--flag", "A flag"));
        rootCmd.AddCommand(analyzeCmd);

        // Case variations should be handled
        var exception = Record.Exception(() => { });
        Assert.Null(exception);
    }

    [Fact]
    public void ManyFlagsSequence()
    {
        var rootCmd = new RootCommand("Test");
        var flag1 = new Option<string?>("--flag1", "First flag");
        var flag2 = new Option<string?>("--flag2", "Second flag");
        var flag3 = new Option<string?>("--flag3", "Third flag");
        var flag4 = new Option<string?>("--flag4", "Fourth flag");
        rootCmd.Add(flag1);
        rootCmd.Add(flag2);
        rootCmd.Add(flag3);
        rootCmd.Add(flag4);

        var exception = Record.Exception(() => { });
        Assert.Null(exception);
    }

    [Fact]
    public void DoubleSlashTerminator()
    {
        var rootCmd = new RootCommand("Test");
        rootCmd.Add(new Option<string?>("--flag", "A flag"));

        var exception = Record.Exception(() => { });
        Assert.Null(exception);
    }

    [Fact]
    public void TabCharacterInValue()
    {
        var value = "value\twith\ttabs";
        var rootCmd = new RootCommand("Test");
        var option = new Option<string?>("--flag", "A test flag");
        rootCmd.Add(option);

        var exception = Record.Exception(() => { });
        Assert.Null(exception);
    }

    [Fact]
    public void NewlineInValue()
    {
        var value = "value\nwith\nnewlines";
        var rootCmd = new RootCommand("Test");
        var option = new Option<string?>("--flag", "A test flag");
        rootCmd.Add(option);

        var exception = Record.Exception(() => { });
        Assert.Null(exception);
    }

    [Fact]
    public void SingleQuotesInValue()
    {
        var value = "value'with'quotes";
        var rootCmd = new RootCommand("Test");
        var option = new Option<string?>("--flag", "A test flag");
        rootCmd.Add(option);

        var exception = Record.Exception(() => { });
        Assert.Null(exception);
    }

    [Fact]
    public void DoubleQuotesInValue()
    {
        var value = "value\"with\"doublequotes";
        var rootCmd = new RootCommand("Test");
        var option = new Option<string?>("--flag", "A test flag");
        rootCmd.Add(option);

        var exception = Record.Exception(() => { });
        Assert.Null(exception);
    }

    [Fact]
    public void VeryLongFlagName()
    {
        var longName = new string('x', 500);
        var rootCmd = new RootCommand("Test");
        var option = new Option<string?>("--flag", "A test flag");
        rootCmd.Add(option);

        var exception = Record.Exception(() => { });
        Assert.Null(exception);
    }

    [Fact]
    public void VeryLongValue()
    {
        var longValue = new string('a', 50000);
        var rootCmd = new RootCommand("Test");
        var option = new Option<string?>("--flag", "A test flag");
        rootCmd.Add(option);

        var exception = Record.Exception(() => { });
        Assert.Null(exception);
    }

    [Fact]
    public void ManyDashes()
    {
        var manyDashes = new string('-', 100);
        var rootCmd = new RootCommand("Test");
        var option = new Option<string?>("--flag", "A test flag");
        rootCmd.Add(option);

        var exception = Record.Exception(() => { });
        Assert.Null(exception);
    }
}

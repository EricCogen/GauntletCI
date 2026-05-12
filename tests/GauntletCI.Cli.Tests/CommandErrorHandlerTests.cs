// SPDX-License-Identifier: Elastic-2.0
using System.CommandLine;
using GauntletCI.Cli.Commands.Utilities;

namespace GauntletCI.Cli.Tests;

/// <summary>
/// Tests for CommandErrorHandler integration utility.
/// </summary>
public class CommandErrorHandlerTests
{
    [Fact]
    public void GetAllOptionNames_ReturnsAllAliases()
    {
        var command = new Command("test", "Test command");
        var option1 = new Option<string?>(new[] { "-f", "--file" }, "A file");
        var option2 = new Option<bool>(new[] { "-v", "--verbose" }, "Verbose output");
        command.Add(option1);
        command.Add(option2);

        var names = CommandErrorHandler.GetAllOptionNames(command).ToList();

        Assert.Contains("-f", names);
        Assert.Contains("--file", names);
        Assert.Contains("-v", names);
        Assert.Contains("--verbose", names);
    }

    [Fact]
    public void GetAllOptionNames_EmptyCommand_ReturnsEmpty()
    {
        var command = new Command("test", "Test command");
        var names = CommandErrorHandler.GetAllOptionNames(command).ToList();
        Assert.Empty(names);
    }

    [Fact]
    public void GetAllCommandNames_ReturnsAllCommands()
    {
        var rootCommand = new RootCommand("Root");
        rootCommand.AddCommand(new Command("analyze", "Analyze"));
        rootCommand.AddCommand(new Command("audit", "Audit"));
        rootCommand.AddCommand(new Command("baseline", "Baseline"));

        var names = CommandErrorHandler.GetAllCommandNames(rootCommand).ToList();

        Assert.Contains("analyze", names);
        Assert.Contains("audit", names);
        Assert.Contains("baseline", names);
    }

    [Fact]
    public void GetAllCommandNames_EmptyRoot_ReturnsEmpty()
    {
        var rootCommand = new RootCommand("Root");
        var names = CommandErrorHandler.GetAllCommandNames(rootCommand).ToList();
        Assert.Empty(names);
    }

    [Fact]
    public void PrintSuggestionsForUnknownOptions_NullOption_DoesNotThrow()
    {
        var exception = Record.Exception(() =>
        {
            CommandErrorHandler.PrintSuggestionsForUnknownOptions(null, new[] { "--file", "--verbose" });
        });
        Assert.Null(exception);
    }

    [Fact]
    public void PrintSuggestionsForUnknownOptions_EmptyOption_DoesNotThrow()
    {
        var exception = Record.Exception(() =>
        {
            CommandErrorHandler.PrintSuggestionsForUnknownOptions("", new[] { "--file", "--verbose" });
        });
        Assert.Null(exception);
    }

    [Fact]
    public void PrintSuggestionsForUnknownOptions_WithValidOptions_DoesNotThrow()
    {
        var exception = Record.Exception(() =>
        {
            CommandErrorHandler.PrintSuggestionsForUnknownOptions("--fil", new[] { "--file", "--verbose", "--force" });
        });
        Assert.Null(exception);
    }
}

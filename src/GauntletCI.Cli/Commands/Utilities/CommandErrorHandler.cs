// SPDX-License-Identifier: Elastic-2.0
using System.CommandLine;
using System.CommandLine.Invocation;
using GauntletCI.Cli.Utilities;

namespace GauntletCI.Cli.Commands.Utilities;

/// <summary>
/// Helper to integrate CommandSuggester into System.CommandLine command handlers.
/// Provides suggestions when parsing fails due to unknown options/commands.
/// </summary>
public static class CommandErrorHandler
{
    /// <summary>
    /// Intercept parse errors and suggest close matches for unknown options.
    /// This should be called as a middleware in command processing.
    /// </summary>
    public static void PrintSuggestionsForUnknownOptions(string? unknownOption, IEnumerable<string> validOptions)
    {
        if (string.IsNullOrWhiteSpace(unknownOption))
            return;

        var suggestions = CommandSuggester.FindCloseMatches(
            unknownOption,
            validOptions.ToList(),
            maxDistance: 2,
            maxSuggestions: 3
        );

        if (suggestions.Count > 0)
        {
            var message = CommandSuggester.FormatSuggestionMessage(unknownOption, suggestions);
            Console.Error.WriteLine();
            Console.Error.WriteLine(message);
        }
    }

    /// <summary>
    /// Get all option names from a command (for suggestion matching).
    /// Extracts both long and short forms of all options.
    /// </summary>
    public static IEnumerable<string> GetAllOptionNames(Command command)
    {
        var optionNames = new HashSet<string>();

        foreach (var symbol in command.Children.OfType<Option>())
        {
            // Add all aliases for this option (--flag, -f, etc)
            foreach (var alias in symbol.Aliases)
            {
                optionNames.Add(alias);
            }
        }

        return optionNames;
    }

    /// <summary>
    /// Get all command names from a root command.
    /// </summary>
    public static IEnumerable<string> GetAllCommandNames(RootCommand rootCommand)
    {
        return rootCommand.Children
            .OfType<Command>()
            .Select(c => c.Name)
            .Where(n => !string.IsNullOrEmpty(n));
    }
}

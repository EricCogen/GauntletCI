// SPDX-License-Identifier: Elastic-2.0
using System.CommandLine;
using GauntletCI.Cli.Commands;

var rootCommand = new RootCommand("GauntletCI — deterministic pre-commit risk detection engine");

rootCommand.AddCommand(AnalyzeCommand.Create());
rootCommand.AddCommand(InitCommand.Create());
rootCommand.AddCommand(IgnoreCommand.Create());

return await rootCommand.InvokeAsync(args);

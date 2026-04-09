// SPDX-License-Identifier: Elastic-2.0
using System.CommandLine;
using GauntletCI.Cli.Commands;
using GauntletCI.Cli.Output;

bool noBanner = args.Contains("--no-banner");
bool ascii = args.Contains("--ascii");
Banner.Print(ascii: ascii, suppress: noBanner);

var rootCommand = new RootCommand("GauntletCI — deterministic pre-commit risk detection engine");

rootCommand.AddCommand(AnalyzeCommand.Create());
rootCommand.AddCommand(InitCommand.Create());

return await rootCommand.InvokeAsync(args);

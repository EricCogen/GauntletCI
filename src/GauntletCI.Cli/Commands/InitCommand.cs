// SPDX-License-Identifier: Elastic-2.0
using System.CommandLine;
using System.Text.Json;

namespace GauntletCI.Cli.Commands;

public static class InitCommand
{
    public static Command Create()
    {
        var outputOption = new Option<DirectoryInfo>(
            "--dir",
            () => new DirectoryInfo(Directory.GetCurrentDirectory()),
            "Directory to create .gauntletci.json in");

        var cmd = new Command("init", "Create a default .gauntletci.json configuration file")
        {
            outputOption,
        };

        cmd.SetHandler((dir) =>
        {
            var configPath = Path.Combine(dir.FullName, ".gauntletci.json");
            if (File.Exists(configPath))
            {
                Console.WriteLine($"Config already exists at {configPath}");
                return;
            }

            var config = new
            {
                version = 1,
                rules = BuildDefaultRules()
            };

            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(configPath, json);
            Console.WriteLine($"Created {configPath}");
        },
        outputOption);

        return cmd;
    }

    private static Dictionary<string, object> BuildDefaultRules()
    {
        var rules = new Dictionary<string, object>();
        for (int i = 1; i <= 20; i++)
        {
            rules[$"GCI{i:D4}"] = new { enabled = true };
        }
        return rules;
    }
}

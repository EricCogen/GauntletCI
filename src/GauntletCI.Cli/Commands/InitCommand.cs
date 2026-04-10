// SPDX-License-Identifier: Elastic-2.0
using System.CommandLine;
using System.Text.Json;
using GauntletCI.Cli.Resources;
using GauntletCI.Cli.Telemetry;

namespace GauntletCI.Cli.Commands;

public static class InitCommand
{
    public static Command Create()
    {
        var outputOption = new Option<DirectoryInfo>(
            "--dir",
            () => new DirectoryInfo(Directory.GetCurrentDirectory()),
            "Directory to create .gauntletci.json in");

        var forceOption = new Option<bool>("--force", "Overwrite existing hook files if present");
        var noTelemetryOption = new Option<bool>("--no-telemetry", "Skip telemetry prompt during init");

        var cmd = new Command("init", "Create a default .gauntletci.json configuration file and install pre-commit hooks")
        {
            outputOption,
            forceOption,
            noTelemetryOption,
        };

        cmd.SetHandler((System.CommandLine.Invocation.InvocationContext ctx) =>
        {
            var dir = ctx.ParseResult.GetValueForOption(outputOption)!;
            var force = ctx.ParseResult.GetValueForOption(forceOption);
            var noTelemetry = ctx.ParseResult.GetValueForOption(noTelemetryOption);

            var gitRoot = FindGitRoot(dir.FullName);
            if (gitRoot is null)
            {
                Console.Error.WriteLine("Error: current directory is not inside a Git repository.");
                ctx.ExitCode = 1;
                return;
            }

            EnsureConfig(dir);
            InstallHooks(gitRoot, force);

            if (!noTelemetry)
                TelemetryConsent.PromptIfNeeded();

            ctx.ExitCode = 0;
        });

        return cmd;
    }

    private static void EnsureConfig(DirectoryInfo dir)
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
    }

    private static void InstallHooks(string gitRoot, bool force)
    {
        var hooksTargetDir = Path.Combine(gitRoot, ".git", "hooks");
        Directory.CreateDirectory(hooksTargetDir);

        WriteHookFromResource("gauntletci-hook.sh", Path.Combine(hooksTargetDir, "pre-commit"), force, makeExecutable: true);
        WriteHookFromResource("gauntletci-hook.ps1", Path.Combine(hooksTargetDir, "pre-commit.ps1"), force, makeExecutable: false);
    }

    private static void WriteHookFromResource(string resourceFileName, string targetPath, bool force, bool makeExecutable)
    {
        if (File.Exists(targetPath) && !force)
        {
            Console.WriteLine($"Hook already exists at {targetPath}. Use --force to overwrite.");
            return;
        }

        var content = EmbeddedResources.ReadText(resourceFileName);
        File.WriteAllText(targetPath, content);

        if (makeExecutable && !OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                targetPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }

        Console.WriteLine($"Installed hook: {targetPath}");
    }

    private static string? FindGitRoot(string startDirectory)
    {
        var current = new DirectoryInfo(startDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, ".git")))
                return current.FullName;

            current = current.Parent;
        }

        return null;
    }

    private static Dictionary<string, object> BuildDefaultRules()
    {
        var rules = new Dictionary<string, object>();
        for (int i = 1; i <= 27; i++)
        {
            rules[$"GCI{i:D4}"] = new { enabled = true };
        }
        return rules;
    }
}

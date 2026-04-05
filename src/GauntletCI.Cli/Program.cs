using System.Text.Json;
using GauntletCI.Core.Evaluation;
using GauntletCI.Core.Gates;
using GauntletCI.Core.Infrastructure;
using GauntletCI.Core.Models;
using GauntletCI.Core.Telemetry;

CliOptions options = CliOptions.Parse(args);

if (options.ShowHelp)
{
	Console.WriteLine(CliOptions.HelpText);
	return;
}

if (options.Command == "install")
{
	int installExit = await InstallHookAsync(Environment.CurrentDirectory);
	Environment.Exit(installExit);
}

if (options.Command == "config")
{
	ShowConfigPath();
	return;
}

ICommandRunner commandRunner = new ProcessCommandRunner();
EvaluationEngine engine = new(
	new BranchCurrencyGate(commandRunner),
	new TestPassageGate(commandRunner),
	commandRunner,
	new ContextAssembler(),
	new PromptBuilder(),
	new FindingParser(),
	new NoOpLlmClient(),
	new TelemetryEmitter());

EvaluationRequest request = new(
	WorkingDirectory: Environment.CurrentDirectory,
	FullMode: options.FullMode,
	FastMode: options.FastMode,
	Rule: options.Rule,
	JsonOutput: options.JsonOutput,
	NoTelemetry: options.NoTelemetry,
	ExplicitTestCommand: options.TestCommandOverride);

EvaluationResult result = await engine.EvaluateAsync(request, CancellationToken.None);
RenderResult(result, options.JsonOutput);
Environment.Exit(result.ExitCode);

static void ShowConfigPath()
{
	string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
	string configPath = Path.Combine(home, ".gauntletci", "config.json");
	Console.WriteLine(configPath);
}

static void RenderResult(EvaluationResult result, bool jsonOutput)
{
	if (jsonOutput)
	{
		object payload = new
		{
			exit_code = result.ExitCode,
			model = result.Model,
			diff_trimmed = result.DiffTrimmed,
			branch_currency = result.BranchCurrencyGate,
			test_passage = result.TestPassageGate,
			error = result.ErrorMessage,
			findings = result.Findings,
		};
		Console.WriteLine(JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
		return;
	}

	Console.WriteLine("GauntletCI");
	Console.WriteLine($"- Branch currency: {RenderGate(result.BranchCurrencyGate)}");
	Console.WriteLine($"- Test passage: {RenderGate(result.TestPassageGate)}");
	Console.WriteLine($"- Findings: {result.Findings.Count}");

	if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
	{
		Console.WriteLine("Error:");
		Console.WriteLine(result.ErrorMessage);
	}

	foreach (Finding finding in result.Findings)
	{
		Console.WriteLine();
		Console.WriteLine($"[{finding.Severity.ToUpperInvariant()}] {finding.RuleId} {finding.RuleName}");
		Console.WriteLine($"Finding: {finding.FindingText}");
		Console.WriteLine($"Evidence: {finding.Evidence}");
		Console.WriteLine($"Why: {finding.WhyItMatters}");
		Console.WriteLine($"Action: {finding.SuggestedAction}");
		Console.WriteLine($"Confidence: {finding.Confidence}");
	}
}

static string RenderGate(GateResult? gate)
{
	if (gate is null)
	{
		return "not run";
	}

	return gate.Passed ? $"pass ({gate.Summary})" : $"fail ({gate.Summary})";
}

static async Task<int> InstallHookAsync(string workingDirectory)
{
	string hooksDir = Path.Combine(workingDirectory, ".git", "hooks");
	if (!Directory.Exists(hooksDir))
	{
		Console.Error.WriteLine("No .git directory found. Run inside a git repository.");
		return 2;
	}

	string hookPath = Path.Combine(hooksDir, "pre-commit");
	string script = "#!/usr/bin/env sh\ngauntletci\n";
	await File.WriteAllTextAsync(hookPath, script);
	Console.WriteLine($"Installed pre-commit hook at {hookPath}");
	return 0;
}

public sealed record CliOptions(
	string Command,
	bool FullMode,
	bool FastMode,
	string? Rule,
	bool JsonOutput,
	bool NoTelemetry,
	bool ShowHelp,
	string? TestCommandOverride)
{
	public static string HelpText =>
		"""
gauntletci              evaluates staged changes
gauntletci --full       evaluates all changes since last commit
gauntletci --fast       uses speed-tier model
gauntletci --rule FL005 runs a single rule only
gauntletci --format json machine-readable output
gauntletci --no-telemetry disable telemetry for this run
gauntletci install      installs git pre-commit hook
gauntletci config       prints user config path
""";

	public static CliOptions Parse(string[] args)
	{
		string command = "review";
		bool full = false;
		bool fast = false;
		string? rule = null;
		bool json = false;
		bool noTelemetry = false;
		bool help = false;
		string? testCommand = null;

		for (int i = 0; i < args.Length; i++)
		{
			string arg = args[i];
			switch (arg)
			{
				case "install":
				case "config":
					command = arg;
					break;
				case "--full":
					full = true;
					break;
				case "--fast":
					fast = true;
					break;
				case "--no-telemetry":
					noTelemetry = true;
					break;
				case "--help":
				case "-h":
					help = true;
					break;
				case "--rule" when i + 1 < args.Length:
					rule = args[++i];
					break;
				case "--format" when i + 1 < args.Length:
					json = string.Equals(args[++i], "json", StringComparison.OrdinalIgnoreCase);
					break;
				case "--test-command" when i + 1 < args.Length:
					testCommand = args[++i];
					break;
			}
		}

		return new CliOptions(command, full, fast, rule, json, noTelemetry, help, testCommand);
	}
}

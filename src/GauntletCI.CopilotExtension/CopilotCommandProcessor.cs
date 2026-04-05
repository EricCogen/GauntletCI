using System.Text;
using GauntletCI.Core.Configuration;
using GauntletCI.Core.Evaluation;
using GauntletCI.Core.Gates;
using GauntletCI.Core.Infrastructure;
using GauntletCI.Core.Models;
using GauntletCI.Core.Telemetry;

namespace GauntletCI.CopilotExtension;

public sealed class CopilotCommandProcessor
{
    public async Task<string> ExecuteAsync(string commandText, string workingDirectory, IChangesProvider changesProvider, CancellationToken cancellationToken)
    {
        CopilotCommand command = CopilotCommand.Parse(commandText);
        return command.Action switch
        {
            CopilotAction.Review => await RunReviewAsync(command, workingDirectory, changesProvider, cancellationToken).ConfigureAwait(false),
            CopilotAction.Explain => await ExplainRuleAsync(command, cancellationToken).ConfigureAwait(false),
            CopilotAction.Status => await GetStatusAsync(workingDirectory, cancellationToken).ConfigureAwait(false),
            _ => "Unsupported command.",
        };
    }

    private static async Task<string> RunReviewAsync(CopilotCommand command, string workingDirectory, IChangesProvider changesProvider, CancellationToken cancellationToken)
    {
        string diff = await changesProvider.GetChangesAsync(workingDirectory, command.Full, cancellationToken).ConfigureAwait(false);

        ConfigLoader configLoader = new();
        ICommandRunner commandRunner = new ProcessCommandRunner();
        EvaluationEngine engine = new(
            configLoader,
            new TestCommandResolver(),
            new BranchCurrencyGate(commandRunner),
            new TestPassageGate(commandRunner),
            commandRunner,
            new ContextAssembler(),
            new PromptBuilder(),
            new FindingParser(),
            new RulesTextProvider(),
            new ModelSelector(),
            new HttpLlmClient(new HttpClient { Timeout = TimeSpan.FromSeconds(120) }),
            new TelemetryEmitter(new HttpClient { Timeout = TimeSpan.FromSeconds(2) }));

        EvaluationRequest request = new(
            WorkingDirectory: workingDirectory,
            FullMode: command.Full,
            FastMode: command.Fast,
            Rule: command.Rule,
            JsonOutput: false,
            NoTelemetry: false,
            ProvidedDiff: diff);

        EvaluationResult result = await engine.EvaluateAsync(request, cancellationToken).ConfigureAwait(false);
        return CopilotRenderer.Render(result);
    }

    private static Task<string> ExplainRuleAsync(CopilotCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Rule))
        {
            return Task.FromResult("Usage: @gauntletci explain GCI007");
        }

        string rules = new RulesTextProvider().LoadRulesText();
        string marker = command.Rule.Trim().ToUpperInvariant();
        string[] lines = rules.Split('\n');
        StringBuilder sb = new();
        bool capturing = false;
        foreach (string line in lines)
        {
            if (line.StartsWith(marker + " ", StringComparison.OrdinalIgnoreCase))
            {
                capturing = true;
            }
            else if (capturing && line.StartsWith("GCI", StringComparison.OrdinalIgnoreCase) && line.Length >= 6 && char.IsDigit(line[3]))
            {
                break;
            }

            if (capturing)
            {
                sb.AppendLine(line.TrimEnd('\r'));
            }
        }

        return Task.FromResult(sb.Length == 0 ? $"Rule {marker} not found." : sb.ToString().Trim());
    }

    private static Task<string> GetStatusAsync(string workingDirectory, CancellationToken cancellationToken)
    {
        ConfigLoader configLoader = new();
        GauntletConfig config = configLoader.LoadEffective(workingDirectory);
        StringBuilder sb = new();
        sb.AppendLine("GauntletCI status");
        sb.AppendLine($"- Model: {config.Model}");
        sb.AppendLine($"- Telemetry enabled: {config.Telemetry}");
        sb.AppendLine($"- Telemetry consent recorded: {config.TelemetryConsentRecorded}");
        sb.AppendLine($"- Test command: {config.TestCommand}");
        sb.AppendLine($"- Blocking rules: {(config.BlockingRules.Count == 0 ? "none" : string.Join(", ", config.BlockingRules))}");
        return Task.FromResult(sb.ToString().Trim());
    }
}

public interface IChangesProvider
{
    Task<string> GetChangesAsync(string workingDirectory, bool fullMode, CancellationToken cancellationToken);
}

public sealed class GitChangesProvider : IChangesProvider
{
    private readonly ICommandRunner _runner = new ProcessCommandRunner();

    public async Task<string> GetChangesAsync(string workingDirectory, bool fullMode, CancellationToken cancellationToken)
    {
        string command = fullMode ? "git diff HEAD" : "git diff --staged";
        CommandResult result = await _runner.RunShellAsync(command, workingDirectory, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? result.StandardOutput : string.Empty;
    }
}

public static class CopilotRenderer
{
    public static string Render(EvaluationResult result)
    {
        string verdict = result.Findings.Any(static finding => finding.RuleId == "GCI017")
            ? "Needs Work"
            : (result.ExitCode == 0 ? "Ready" : "High Risk");

        StringBuilder sb = new();
        sb.AppendLine($"## GCI017 Verdict: {verdict}");

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            sb.AppendLine();
            sb.AppendLine("Error");
            sb.AppendLine(result.ErrorMessage);
            return sb.ToString().Trim();
        }

        foreach (Finding finding in result.Findings)
        {
            bool isHigh = finding.Severity.Equals("high", StringComparison.OrdinalIgnoreCase);
            sb.AppendLine();
            sb.AppendLine(isHigh
                ? $"<details open><summary>{finding.RuleId} {finding.RuleName} [{finding.Severity}]</summary>"
                : $"<details><summary>{finding.RuleId} {finding.RuleName} [{finding.Severity}]</summary>");
            sb.AppendLine();
            sb.AppendLine($"- finding: {finding.FindingText}");
            sb.AppendLine($"- evidence: {finding.Evidence}");
            sb.AppendLine($"- why_it_matters: {finding.WhyItMatters}");
            sb.AppendLine($"- suggested_action: {finding.SuggestedAction}");
            sb.AppendLine($"- confidence: {finding.Confidence}");
            sb.AppendLine("</details>");
        }

        if (result.Findings.Count == 0)
        {
            sb.AppendLine();
            sb.AppendLine("No findings.");
        }

        return sb.ToString().Trim();
    }
}

public enum CopilotAction
{
    Review,
    Explain,
    Status,
}

public sealed record CopilotCommand(CopilotAction Action, bool Full, bool Fast, string? Rule)
{
    public static CopilotCommand Parse(string input)
    {
        string normalized = input.Trim();
        if (normalized.StartsWith("@gauntletci explain", StringComparison.OrdinalIgnoreCase))
        {
            string[] parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string? rule = parts.Length >= 3 ? parts[2].Trim() : null;
            return new CopilotCommand(CopilotAction.Explain, false, false, rule);
        }

        if (normalized.StartsWith("@gauntletci status", StringComparison.OrdinalIgnoreCase))
        {
            return new CopilotCommand(CopilotAction.Status, false, false, null);
        }

        bool fast = normalized.Contains("--fast", StringComparison.OrdinalIgnoreCase);
        bool full = normalized.Contains("--full", StringComparison.OrdinalIgnoreCase);
        string? ruleArg = null;

        string[] tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < tokens.Length; i++)
        {
            if (tokens[i].Equals("--rule", StringComparison.OrdinalIgnoreCase) && i + 1 < tokens.Length)
            {
                ruleArg = tokens[i + 1];
                break;
            }
        }

        return new CopilotCommand(CopilotAction.Review, full, fast, ruleArg);
    }
}

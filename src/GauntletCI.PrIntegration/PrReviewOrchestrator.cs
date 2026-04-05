using System.Text;
using System.Text.RegularExpressions;
using GauntletCI.Core.Configuration;
using GauntletCI.Core.Evaluation;
using GauntletCI.Core.Gates;
using GauntletCI.Core.Infrastructure;
using GauntletCI.Core.Models;
using GauntletCI.Core.Telemetry;

namespace GauntletCI.PrIntegration;

public sealed class PrReviewOrchestrator(IGitHubClient gitHubClient)
{
    private static readonly Regex EvidenceRegex = new(@"(?<path>[A-Za-z0-9_\-/]+\.[A-Za-z0-9]+):(?<line>\d+)", RegexOptions.Compiled);

    public async Task<PrEvaluationSummary> EvaluateAndPublishAsync(PrEventContext context, CancellationToken cancellationToken)
    {
        string diff = await gitHubClient.GetPullRequestDiffAsync(context, cancellationToken).ConfigureAwait(false);

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
            WorkingDirectory: context.WorkingDirectory,
            FullMode: true,
            FastMode: false,
            Rule: null,
            JsonOutput: false,
            NoTelemetry: true,
            ExplicitTestCommand: null,
            ProvidedDiff: diff);

        EvaluationResult result = await engine.EvaluateAsync(request, cancellationToken).ConfigureAwait(false);
        string verdict = result.ExitCode == 0 ? "Ready" : (result.Findings.Any(static finding => finding.Severity.Equals("high", StringComparison.OrdinalIgnoreCase)) ? "High Risk" : "Needs Work");

        await gitHubClient.PublishStatusCheckAsync(context, verdict, result, cancellationToken).ConfigureAwait(false);
        await PublishAnchoredCommentsAsync(context, result.Findings, cancellationToken).ConfigureAwait(false);

        return new PrEvaluationSummary(verdict, result.Findings.Count, result.ExitCode == 0);
    }

    private async Task PublishAnchoredCommentsAsync(PrEventContext context, IReadOnlyList<Finding> findings, CancellationToken cancellationToken)
    {
        foreach (Finding finding in findings)
        {
            Match match = EvidenceRegex.Match(finding.Evidence);
            if (!match.Success)
            {
                continue;
            }

            string path = match.Groups["path"].Value;
            if (!int.TryParse(match.Groups["line"].Value, out int line))
            {
                continue;
            }

            StringBuilder body = new();
            body.AppendLine($"**{finding.RuleId} {finding.RuleName}** [{finding.Severity}]");
            body.AppendLine();
            body.AppendLine(finding.FindingText);
            body.AppendLine();
            body.AppendLine($"Why it matters: {finding.WhyItMatters}");
            body.AppendLine($"Suggested action: {finding.SuggestedAction}");

            await gitHubClient.PublishReviewCommentAsync(context, path, line, body.ToString().Trim(), cancellationToken).ConfigureAwait(false);
        }
    }
}

public sealed record PrEventContext(
    string Owner,
    string Repository,
    int PullRequestNumber,
    string HeadSha,
    string WorkingDirectory);

public sealed record PrEvaluationSummary(string Verdict, int FindingsCount, bool Passed);

using System.Diagnostics;
using GauntletCI.Core.Configuration;
using GauntletCI.Core.Gates;
using GauntletCI.Core.Infrastructure;
using GauntletCI.Core.Models;
using GauntletCI.Core.Telemetry;

namespace GauntletCI.Core.Evaluation;

public sealed class EvaluationEngine(
    ConfigLoader configLoader,
    TestCommandResolver testCommandResolver,
    BranchCurrencyGate branchCurrencyGate,
    TestPassageGate testPassageGate,
    ICommandRunner commandRunner,
    ContextAssembler contextAssembler,
    PromptBuilder promptBuilder,
    FindingParser findingParser,
    RulesTextProvider rulesTextProvider,
    ModelSelector modelSelector,
    ILlmClient llmClient,
    TelemetryEmitter telemetryEmitter)
{
    public async Task<EvaluationResult> EvaluateAsync(EvaluationRequest request, CancellationToken cancellationToken)
    {
        Stopwatch sw = Stopwatch.StartNew();
        GauntletConfig config = configLoader.LoadEffective(request.WorkingDirectory);
        string? configuredTestCommand = string.IsNullOrWhiteSpace(request.ExplicitTestCommand) ? config.TestCommand : request.ExplicitTestCommand;
        string testCommand = testCommandResolver.Resolve(request.WorkingDirectory, configuredTestCommand);

        GateResult branchResult = await branchCurrencyGate.ExecuteAsync(request.WorkingDirectory, cancellationToken).ConfigureAwait(false);
        if (!branchResult.Passed)
        {
            sw.Stop();
            return new EvaluationResult(1, branchResult, null, [], branchResult.Output ?? branchResult.Summary, false, config.Model, null, (int)sw.ElapsedMilliseconds);
        }

        GateResult testResult = await testPassageGate.ExecuteAsync(request.WorkingDirectory, testCommand!, cancellationToken).ConfigureAwait(false);
        if (!testResult.Passed)
        {
            sw.Stop();
            return new EvaluationResult(1, branchResult, testResult, [], testResult.Output ?? testResult.Summary, false, config.Model, null, (int)sw.ElapsedMilliseconds);
        }

        string diffText;
        if (!string.IsNullOrWhiteSpace(request.ProvidedDiff))
        {
            diffText = request.ProvidedDiff;
        }
        else
        {
            string diffCommand = request.FullMode ? "git diff HEAD" : "git diff --staged";
            CommandResult diffResult = await commandRunner.RunShellAsync(diffCommand, request.WorkingDirectory, cancellationToken).ConfigureAwait(false);
            if (!diffResult.IsSuccess)
            {
                sw.Stop();
                return new EvaluationResult(3, branchResult, testResult, [], "Unable to collect git diff for evaluation.", false, config.Model, null, (int)sw.ElapsedMilliseconds);
            }

            diffText = diffResult.StandardOutput;
        }

        CommandResult commitsResult = await commandRunner.RunProcessAsync("git", "log -n 3 --pretty=%s", request.WorkingDirectory, cancellationToken).ConfigureAwait(false);
        List<string> recentCommits = commitsResult.IsSuccess
            ? commitsResult.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
            : [];

        AssembledContext assembled = contextAssembler.Assemble(branchResult, testResult, diffText, config, recentCommits);

        ModelSelection selection = modelSelector.Select(config, request.FastMode);
        if (!selection.IsConfigured)
        {
            sw.Stop();
            return new EvaluationResult(2, branchResult, testResult, [], $"No API key configured. Set {selection.ApiKeyEnv} environment variable.", assembled.DiffTrimmed, selection.Model, assembled.Metadata, (int)sw.ElapsedMilliseconds);
        }

        string rulesText;
        try
        {
            rulesText = rulesTextProvider.LoadRulesText();
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new EvaluationResult(2, branchResult, testResult, [], $"Rules loading failed: {ex.Message}", assembled.DiffTrimmed, selection.Model, assembled.Metadata, (int)sw.ElapsedMilliseconds);
        }

        string systemPrompt = promptBuilder.BuildSystemPrompt(rulesText, request.Rule);
        string userPrompt = promptBuilder.BuildUserPrompt(assembled.Context);

        LlmResponse modelResponse = await llmClient.EvaluateAsync(selection.Model, systemPrompt, userPrompt, selection.ApiKey!, cancellationToken).ConfigureAwait(false);
        if (!modelResponse.Success)
        {
            sw.Stop();
            return new EvaluationResult(3, branchResult, testResult, [], modelResponse.ErrorMessage ?? "Model call failed.", assembled.DiffTrimmed, selection.Model, assembled.Metadata, (int)sw.ElapsedMilliseconds);
        }

        IReadOnlyList<Finding> findings;
        try
        {
            findings = findingParser.Parse(modelResponse.RawResponse);
        }
        catch (FormatException ex)
        {
            sw.Stop();
            return new EvaluationResult(3, branchResult, testResult, [], ex.Message, assembled.DiffTrimmed, selection.Model, assembled.Metadata, (int)sw.ElapsedMilliseconds);
        }

        IReadOnlyList<Finding> filtered = ApplyRuleScopeAndDisabledRules(findings, request.Rule, config.DisabledRules);
        int exitCode = ComputeExitCode(filtered, config.BlockingRules);

        sw.Stop();
        EvaluationResult evaluationResult = new(exitCode, branchResult, testResult, filtered, null, assembled.DiffTrimmed, selection.Model, assembled.Metadata, (int)sw.ElapsedMilliseconds);
        if (config.ShouldEmitTelemetry(request.NoTelemetry))
        {
            await telemetryEmitter.EmitAsync(evaluationResult, config, cancellationToken).ConfigureAwait(false);
        }

        return evaluationResult;
    }

    private static IReadOnlyList<Finding> ApplyRuleScopeAndDisabledRules(IReadOnlyList<Finding> findings, string? requestedRule, IReadOnlyList<string> disabledRules)
    {
        HashSet<string> disabled = [.. disabledRules.Select(static rule => rule.Trim()).Where(static rule => !string.IsNullOrWhiteSpace(rule))];

        IEnumerable<Finding> query = findings.Where(finding => !disabled.Contains(finding.RuleId));
        if (!string.IsNullOrWhiteSpace(requestedRule))
        {
            query = query.Where(finding => string.Equals(finding.RuleId, requestedRule, StringComparison.OrdinalIgnoreCase));
        }

        return [.. query];
    }

    private static int ComputeExitCode(IReadOnlyList<Finding> findings, IReadOnlyList<string> blockingRules)
    {
        if (findings.Any(static finding => finding.Severity.Equals("high", StringComparison.OrdinalIgnoreCase)))
        {
            return 1;
        }

        if (blockingRules.Count == 0)
        {
            return 0;
        }

        HashSet<string> blocked = new(blockingRules, StringComparer.OrdinalIgnoreCase);
        return findings.Any(finding => blocked.Contains(finding.RuleId)) ? 1 : 0;
    }
}
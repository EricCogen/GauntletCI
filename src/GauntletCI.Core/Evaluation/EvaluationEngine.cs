using GauntletCI.Core.Gates;
using GauntletCI.Core.Infrastructure;
using GauntletCI.Core.Models;
using GauntletCI.Core.Telemetry;

namespace GauntletCI.Core.Evaluation;

public sealed class EvaluationEngine(
    BranchCurrencyGate branchCurrencyGate,
    TestPassageGate testPassageGate,
    ICommandRunner commandRunner,
    ContextAssembler contextAssembler,
    PromptBuilder promptBuilder,
    FindingParser findingParser,
    ILlmClient llmClient,
    TelemetryEmitter telemetryEmitter)
{
    public async Task<EvaluationResult> EvaluateAsync(EvaluationRequest request, CancellationToken cancellationToken)
    {
        GauntletConfig config = GauntletConfig.Load(request.WorkingDirectory);
        string testCommand = string.IsNullOrWhiteSpace(request.ExplicitTestCommand) ? config.TestCommand : request.ExplicitTestCommand;

        GateResult branchResult = await branchCurrencyGate.ExecuteAsync(request.WorkingDirectory, cancellationToken).ConfigureAwait(false);
        if (!branchResult.Passed)
        {
            return new EvaluationResult(1, branchResult, null, [], branchResult.Output ?? branchResult.Summary, false, config.Model);
        }

        GateResult testResult = await testPassageGate.ExecuteAsync(request.WorkingDirectory, testCommand!, cancellationToken).ConfigureAwait(false);
        if (!testResult.Passed)
        {
            return new EvaluationResult(1, branchResult, testResult, [], testResult.Output ?? testResult.Summary, false, config.Model);
        }

        string diffCommand = request.FullMode ? "git diff HEAD" : "git diff --staged";
        CommandResult diffResult = await commandRunner.RunShellAsync(diffCommand, request.WorkingDirectory, cancellationToken).ConfigureAwait(false);
        if (!diffResult.IsSuccess)
        {
            return new EvaluationResult(3, branchResult, testResult, [], "Unable to collect git diff for evaluation.", false, config.Model);
        }

        CommandResult commitsResult = await commandRunner.RunProcessAsync("git", "log -n 3 --pretty=%s", request.WorkingDirectory, cancellationToken).ConfigureAwait(false);
        List<string> recentCommits = commitsResult.IsSuccess
            ? commitsResult.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
            : [];

        (string assembledContext, bool trimmed) = contextAssembler.Assemble(branchResult, testResult, diffResult.StandardOutput, config, recentCommits);
        string rulesText = "Full rule text should be loaded from gauntletci-rules.md at build time.";
        string systemPrompt = promptBuilder.BuildSystemPrompt(rulesText);
        string userPrompt = promptBuilder.BuildUserPrompt(assembledContext);

        LlmResponse modelResponse = await llmClient.EvaluateAsync(config.Model, systemPrompt, userPrompt, request.FastMode, cancellationToken).ConfigureAwait(false);
        if (!modelResponse.Success)
        {
            return new EvaluationResult(3, branchResult, testResult, [], modelResponse.ErrorMessage ?? "Model call failed.", trimmed, config.Model);
        }

        IReadOnlyList<Finding> findings;
        try
        {
            findings = findingParser.Parse(modelResponse.RawResponse);
        }
        catch (FormatException ex)
        {
            return new EvaluationResult(3, branchResult, testResult, [], ex.Message, trimmed, config.Model);
        }

        int exitCode = findings.Any(static finding => finding.Severity.Equals("high", StringComparison.OrdinalIgnoreCase)) ? 1 : 0;
        EvaluationResult evaluationResult = new(exitCode, branchResult, testResult, findings, null, trimmed, config.Model);
        if (!request.NoTelemetry)
        {
            await telemetryEmitter.EmitAsync(evaluationResult, config, cancellationToken).ConfigureAwait(false);
        }

        return evaluationResult;
    }
}
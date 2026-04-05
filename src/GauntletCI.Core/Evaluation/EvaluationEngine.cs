// SPDX-License-Identifier: Elastic-2.0
// Copyright (c) Eric Cogen. All rights reserved.

using System.Diagnostics;
using System.Text;
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
    DeterministicAnalysisRunner deterministicAnalysisRunner,
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

        // CLI --local flag overrides base_url from config file
        if (!string.IsNullOrWhiteSpace(request.LocalEndpoint))
        {
            config = config with { BaseUrl = request.LocalEndpoint };
        }

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
        IReadOnlyList<Finding> deterministicFindings = ApplyRuleScopeAndDisabledRules(
            deterministicAnalysisRunner.Analyze(diffText, assembled.Metadata),
            request.Rule,
            config.DisabledRules);

        if (deterministicFindings.Count == 0)
        {
            sw.Stop();
            EvaluationResult cleanResult = new(
                ExitCode: 0,
                BranchCurrencyGate: branchResult,
                TestPassageGate: testResult,
                Findings: [],
                ErrorMessage: null,
                DiffTrimmed: assembled.DiffTrimmed,
                Model: config.Model,
                DiffMetadata: assembled.Metadata,
                EvaluationDurationMs: (int)sw.ElapsedMilliseconds,
                ModelStepSkipped: true);
            return await FinalizeResultAsync(cleanResult, config, request, cancellationToken).ConfigureAwait(false);
        }

        ModelSelection selection = modelSelector.Select(config, request.FastMode);
        if (!selection.IsConfigured)
        {
            sw.Stop();
            if (config.ModelRequired)
            {
                return new EvaluationResult(
                    ExitCode: 1,
                    BranchCurrencyGate: branchResult,
                    TestPassageGate: testResult,
                    Findings: deterministicFindings,
                    ErrorMessage: BuildStrictModelError(selection.ApiKeyEnv),
                    DiffTrimmed: assembled.DiffTrimmed,
                    Model: selection.Model,
                    DiffMetadata: assembled.Metadata,
                    EvaluationDurationMs: (int)sw.ElapsedMilliseconds);
            }

            int deterministicExit = ComputeExitCode(deterministicFindings, config.BlockingRules);
            EvaluationResult deterministicResult = new(
                ExitCode: deterministicExit,
                BranchCurrencyGate: branchResult,
                TestPassageGate: testResult,
                Findings: deterministicFindings,
                ErrorMessage: null,
                DiffTrimmed: assembled.DiffTrimmed,
                Model: selection.Model,
                DiffMetadata: assembled.Metadata,
                EvaluationDurationMs: (int)sw.ElapsedMilliseconds,
                WarningMessage: "Model unavailable. Showing deterministic findings only.",
                ModelStepSkipped: true);
            return await FinalizeResultAsync(deterministicResult, config, request, cancellationToken).ConfigureAwait(false);
        }

        string rulesText;
        try
        {
            rulesText = rulesTextProvider.LoadRulesText();
        }
        catch (Exception ex)
        {
            sw.Stop();
            if (config.ModelRequired)
            {
                return new EvaluationResult(
                    ExitCode: 1,
                    BranchCurrencyGate: branchResult,
                    TestPassageGate: testResult,
                    Findings: deterministicFindings,
                    ErrorMessage: BuildStrictModelError(selection.ApiKeyEnv, $"Rules loading failed: {ex.Message}"),
                    DiffTrimmed: assembled.DiffTrimmed,
                    Model: selection.Model,
                    DiffMetadata: assembled.Metadata,
                    EvaluationDurationMs: (int)sw.ElapsedMilliseconds);
            }

            int deterministicExit = ComputeExitCode(deterministicFindings, config.BlockingRules);
            EvaluationResult deterministicResult = new(
                ExitCode: deterministicExit,
                BranchCurrencyGate: branchResult,
                TestPassageGate: testResult,
                Findings: deterministicFindings,
                ErrorMessage: null,
                DiffTrimmed: assembled.DiffTrimmed,
                Model: selection.Model,
                DiffMetadata: assembled.Metadata,
                EvaluationDurationMs: (int)sw.ElapsedMilliseconds,
                WarningMessage: "Model enrichment failed. Showing deterministic findings only.",
                ModelStepSkipped: true);
            return await FinalizeResultAsync(deterministicResult, config, request, cancellationToken).ConfigureAwait(false);
        }

        string systemPrompt = promptBuilder.BuildSystemPrompt(rulesText, request.Rule);
        string userPrompt = promptBuilder.BuildUserPrompt(assembled.Context);

        LlmResponse modelResponse = await llmClient.EvaluateAsync(selection.Model, systemPrompt, userPrompt, selection.ApiKey ?? "", cancellationToken, selection.BaseUrl).ConfigureAwait(false);
        if (!modelResponse.Success)
        {
            sw.Stop();
            if (config.ModelRequired)
            {
                return new EvaluationResult(
                    ExitCode: 1,
                    BranchCurrencyGate: branchResult,
                    TestPassageGate: testResult,
                    Findings: deterministicFindings,
                    ErrorMessage: BuildStrictModelError(selection.ApiKeyEnv, modelResponse.ErrorMessage ?? "Model call failed."),
                    DiffTrimmed: assembled.DiffTrimmed,
                    Model: selection.Model,
                    DiffMetadata: assembled.Metadata,
                    EvaluationDurationMs: (int)sw.ElapsedMilliseconds);
            }

            int deterministicExit = ComputeExitCode(deterministicFindings, config.BlockingRules);
            EvaluationResult deterministicResult = new(
                ExitCode: deterministicExit,
                BranchCurrencyGate: branchResult,
                TestPassageGate: testResult,
                Findings: deterministicFindings,
                ErrorMessage: null,
                DiffTrimmed: assembled.DiffTrimmed,
                Model: selection.Model,
                DiffMetadata: assembled.Metadata,
                EvaluationDurationMs: (int)sw.ElapsedMilliseconds,
                WarningMessage: "Model unavailable. Showing deterministic findings only.",
                ModelStepSkipped: true);
            return await FinalizeResultAsync(deterministicResult, config, request, cancellationToken).ConfigureAwait(false);
        }

        IReadOnlyList<Finding> modelFindings;
        try
        {
            modelFindings = findingParser.Parse(modelResponse.RawResponse);
        }
        catch (FormatException ex)
        {
            sw.Stop();
            if (config.ModelRequired)
            {
                return new EvaluationResult(
                    ExitCode: 1,
                    BranchCurrencyGate: branchResult,
                    TestPassageGate: testResult,
                    Findings: deterministicFindings,
                    ErrorMessage: BuildStrictModelError(selection.ApiKeyEnv, ex.Message),
                    DiffTrimmed: assembled.DiffTrimmed,
                    Model: selection.Model,
                    DiffMetadata: assembled.Metadata,
                    EvaluationDurationMs: (int)sw.ElapsedMilliseconds);
            }

            int deterministicExit = ComputeExitCode(deterministicFindings, config.BlockingRules);
            EvaluationResult deterministicResult = new(
                ExitCode: deterministicExit,
                BranchCurrencyGate: branchResult,
                TestPassageGate: testResult,
                Findings: deterministicFindings,
                ErrorMessage: null,
                DiffTrimmed: assembled.DiffTrimmed,
                Model: selection.Model,
                DiffMetadata: assembled.Metadata,
                EvaluationDurationMs: (int)sw.ElapsedMilliseconds,
                WarningMessage: "Model response could not be parsed. Showing deterministic findings only.",
                ModelStepSkipped: true);
            return await FinalizeResultAsync(deterministicResult, config, request, cancellationToken).ConfigureAwait(false);
        }

        IReadOnlyList<Finding> filtered = ApplyRuleScopeAndDisabledRules(modelFindings, request.Rule, config.DisabledRules);
        int exitCode = ComputeExitCode(filtered, config.BlockingRules);

        sw.Stop();
        EvaluationResult evaluationResult = new(exitCode, branchResult, testResult, filtered, null, assembled.DiffTrimmed, selection.Model, assembled.Metadata, (int)sw.ElapsedMilliseconds);
        return await FinalizeResultAsync(evaluationResult, config, request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<EvaluationResult> FinalizeResultAsync(EvaluationResult result, GauntletConfig config, EvaluationRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(result.ErrorMessage) && config.ShouldEmitTelemetry(request.NoTelemetry))
        {
            await telemetryEmitter.EmitAsync(result, config, cancellationToken).ConfigureAwait(false);
        }

        return result;
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

    private static string BuildStrictModelError(string apiKeyEnv, string? detail = null)
    {
        StringBuilder builder = new();
        builder.AppendLine("Model enrichment is required by configuration, but no model provider is available.");
        builder.Append($"Set {apiKeyEnv}, configure a compatible local endpoint, or disable model-required mode.");
        if (!string.IsNullOrWhiteSpace(detail))
        {
            builder.AppendLine();
            builder.Append("Details: ");
            builder.Append(detail.Trim());
        }

        return builder.ToString();
    }
}

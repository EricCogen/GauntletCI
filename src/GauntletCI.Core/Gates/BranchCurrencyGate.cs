// SPDX-License-Identifier: Elastic-2.0
// Copyright (c) Eric Cogen. All rights reserved.

using GauntletCI.Core.Infrastructure;
using GauntletCI.Core.Models;

namespace GauntletCI.Core.Gates;

public sealed class BranchCurrencyGate(ICommandRunner commandRunner)
{
    public async Task<GateResult> ExecuteAsync(string workingDirectory, CancellationToken cancellationToken)
    {
        // Check whether an upstream is configured before attempting any network call.
        CommandResult upstreamCheck = await commandRunner.RunProcessAsync("git", "rev-parse --abbrev-ref --symbolic-full-name @{upstream}", workingDirectory, cancellationToken).ConfigureAwait(false);
        if (!upstreamCheck.IsSuccess)
        {
            return GateResult.Pass("Branch Currency", "No upstream configured; branch currency check skipped.");
        }

        string upstreamRef = upstreamCheck.StandardOutput.Trim();

        // Attempt a fetch to refresh remote refs. A network failure is non-fatal:
        // we fall through and compare against whatever refs are cached locally.
        CommandResult fetchResult = await commandRunner.RunProcessAsync("git", "fetch", workingDirectory, cancellationToken).ConfigureAwait(false);
        if (!fetchResult.IsSuccess)
        {
            string fetchOutput = string.Join(Environment.NewLine, [fetchResult.StandardOutput, fetchResult.StandardError]).Trim();
            Console.Error.WriteLine($"Warning: git fetch failed; branch currency check uses cached refs. ({fetchOutput})");
        }

        CommandResult countResult = await commandRunner.RunProcessAsync("git", "rev-list HEAD..@{upstream} --count", workingDirectory, cancellationToken).ConfigureAwait(false);
        if (!countResult.IsSuccess)
        {
            string output = string.Join(Environment.NewLine, [countResult.StandardOutput, countResult.StandardError]).Trim();
            return GateResult.Fail("Branch Currency", "Unable to determine commits behind upstream. Ensure upstream is configured.", output);
        }

        if (!int.TryParse(countResult.StandardOutput.Trim(), out int commitsBehind))
        {
            return GateResult.Fail("Branch Currency", "Failed to parse branch currency result.", countResult.StandardOutput);
        }

        if (commitsBehind > 0)
        {
            return GateResult.Fail("Branch Currency", $"Branch is {commitsBehind} commit(s) behind {upstreamRef}. Pull latest changes before running GauntletCI.");
        }

        return GateResult.Pass("Branch Currency", "Branch is up to date with upstream.");
    }
}

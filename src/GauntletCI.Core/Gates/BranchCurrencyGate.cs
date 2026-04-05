using GauntletCI.Core.Infrastructure;
using GauntletCI.Core.Models;

namespace GauntletCI.Core.Gates;

public sealed class BranchCurrencyGate(ICommandRunner commandRunner)
{
    public async Task<GateResult> ExecuteAsync(string workingDirectory, CancellationToken cancellationToken)
    {
        CommandResult fetchResult = await commandRunner.RunProcessAsync("git", "fetch --dry-run", workingDirectory, cancellationToken).ConfigureAwait(false);
        if (!fetchResult.IsSuccess)
        {
            string output = string.Join(Environment.NewLine, [fetchResult.StandardOutput, fetchResult.StandardError]).Trim();
            return GateResult.Fail("Branch Currency", "Unable to refresh upstream refs for branch currency validation.", output);
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
            CommandResult upstreamName = await commandRunner.RunProcessAsync("git", "rev-parse --abbrev-ref --symbolic-full-name @{upstream}", workingDirectory, cancellationToken).ConfigureAwait(false);
            string upstream = upstreamName.IsSuccess ? upstreamName.StandardOutput.Trim() : "upstream";
            return GateResult.Fail("Branch Currency", $"Branch is {commitsBehind} commit(s) behind {upstream}. Pull latest changes before running GauntletCI.");
        }

        return GateResult.Pass("Branch Currency", "Branch is up to date with upstream.");
    }
}

using GauntletCI.Core.Infrastructure;
using GauntletCI.Core.Models;

namespace GauntletCI.Core.Gates;

public sealed class TestPassageGate(ICommandRunner commandRunner)
{
    public async Task<GateResult> ExecuteAsync(string workingDirectory, string testCommand, CancellationToken cancellationToken)
    {
        CommandResult result = await commandRunner.RunShellAsync(testCommand, workingDirectory, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            return GateResult.Pass("Test Passage", $"Test command passed: {testCommand}");
        }

        string output = string.Join(Environment.NewLine, [result.StandardOutput, result.StandardError]).Trim();
        return GateResult.Fail("Test Passage", $"Test command failed: {testCommand}", output);
    }
}

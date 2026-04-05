using GauntletCI.Core.Infrastructure;
using GauntletCI.Core.Models;

namespace GauntletCI.Core.Gates;

public sealed class TestPassageGate(ICommandRunner commandRunner)
{
    public async Task<GateResult> ExecuteAsync(string workingDirectory, string testCommand, CancellationToken cancellationToken)
    {
        (string fileName, string arguments) = SplitCommand(testCommand);
        CommandResult result = await commandRunner.RunProcessAsync(fileName, arguments, workingDirectory, cancellationToken).ConfigureAwait(false);
        string output = string.Join(Environment.NewLine, [result.StandardOutput, result.StandardError]).Trim();
        if (result.IsSuccess)
        {
            return new GateResult("Test Passage", true, $"Test command passed: {testCommand}", output);
        }

        return GateResult.Fail("Test Passage", $"Test command failed: {testCommand}", output);
    }

    /// <summary>
    /// Splits a command string into (executable, arguments) without invoking a shell,
    /// eliminating shell injection via user-supplied test commands.
    /// Handles basic quoting for arguments that contain spaces.
    /// </summary>
    internal static (string FileName, string Arguments) SplitCommand(string command)
    {
        command = command.Trim();
        if (string.IsNullOrEmpty(command))
        {
            return (string.Empty, string.Empty);
        }

        // Find the end of the first token (the executable), respecting quotes
        int i = 0;
        string fileName;
        if (command[0] == '"')
        {
            int end = command.IndexOf('"', 1);
            fileName = end >= 0 ? command[1..end] : command[1..];
            i = end >= 0 ? end + 1 : command.Length;
        }
        else
        {
            int space = command.IndexOf(' ');
            if (space < 0)
            {
                return (command, string.Empty);
            }

            fileName = command[..space];
            i = space;
        }

        string arguments = command[i..].TrimStart();
        return (fileName, arguments);
    }
}

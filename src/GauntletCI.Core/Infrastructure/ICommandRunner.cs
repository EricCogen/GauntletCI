namespace GauntletCI.Core.Infrastructure;

public interface ICommandRunner
{
    Task<CommandResult> RunProcessAsync(string fileName, string arguments, string workingDirectory, CancellationToken cancellationToken);

    Task<CommandResult> RunShellAsync(string command, string workingDirectory, CancellationToken cancellationToken);
}
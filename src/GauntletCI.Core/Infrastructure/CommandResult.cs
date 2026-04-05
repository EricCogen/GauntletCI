namespace GauntletCI.Core.Infrastructure;

public sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool IsSuccess => ExitCode == 0;
}
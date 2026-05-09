// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Core.Diff;

/// <summary>
/// Thrown when a git process exits with a non-zero exit code.
/// Carries the command, exit code, and stderr output for diagnostics.
/// </summary>
public sealed class GitProcessException : Exception
{
    public int ExitCode { get; }
    public string StdErr { get; }
    public string Command { get; }

    public GitProcessException(string command, int exitCode, string stderr)
        : base($"git process failed for '{command}' (exit {exitCode}): {stderr.Trim()}")
    {
        Command = command;
        ExitCode = exitCode;
        StdErr = stderr;
        Data["Command"] = command;
    }
}

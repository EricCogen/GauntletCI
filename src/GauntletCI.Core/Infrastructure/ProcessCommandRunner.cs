// SPDX-License-Identifier: Elastic-2.0
// Copyright (c) Eric Cogen. All rights reserved.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace GauntletCI.Core.Infrastructure;

public sealed class ProcessCommandRunner : ICommandRunner
{
    public async Task<CommandResult> RunProcessAsync(string fileName, string arguments, string workingDirectory, CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using Process process = new() { StartInfo = startInfo };
        StringBuilder stdOut = new();
        StringBuilder stdErr = new();

        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                stdOut.AppendLine(args.Data);
            }
        };

        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                stdErr.AppendLine(args.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return new CommandResult(process.ExitCode, stdOut.ToString().TrimEnd(), stdErr.ToString().TrimEnd());
    }

    public Task<CommandResult> RunShellAsync(string command, string workingDirectory, CancellationToken cancellationToken)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return RunProcessAsync("pwsh", $"-NoProfile -Command \"{command.Replace("\"", "\\\"")}\"", workingDirectory, cancellationToken);
        }

        return RunProcessAsync("/bin/bash", $"-lc \"{command.Replace("\"", "\\\"")}\"", workingDirectory, cancellationToken);
    }
}
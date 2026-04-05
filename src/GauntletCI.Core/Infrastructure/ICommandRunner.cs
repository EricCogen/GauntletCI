// SPDX-License-Identifier: Elastic-2.0
// Copyright (c) Eric Cogen. All rights reserved.

namespace GauntletCI.Core.Infrastructure;

public interface ICommandRunner
{
    Task<CommandResult> RunProcessAsync(string fileName, string arguments, string workingDirectory, CancellationToken cancellationToken);

    Task<CommandResult> RunShellAsync(string command, string workingDirectory, CancellationToken cancellationToken);
}
// SPDX-License-Identifier: Elastic-2.0
// Copyright (c) Eric Cogen. All rights reserved.

namespace GauntletCI.Core.Configuration;

public sealed class TestCommandResolver
{
    public string Resolve(string workingDirectory, string? configuredCommand)
    {
        if (!string.IsNullOrWhiteSpace(configuredCommand))
        {
            return configuredCommand;
        }

        if (Directory.EnumerateFiles(workingDirectory, "*.sln*", SearchOption.TopDirectoryOnly).Any())
        {
            return "dotnet test";
        }

        if (File.Exists(Path.Combine(workingDirectory, "package.json")))
        {
            return "npm test";
        }

        if (File.Exists(Path.Combine(workingDirectory, "pyproject.toml")) ||
            File.Exists(Path.Combine(workingDirectory, "pytest.ini")) ||
            File.Exists(Path.Combine(workingDirectory, "requirements.txt")))
        {
            return "pytest";
        }

        return "dotnet test";
    }
}

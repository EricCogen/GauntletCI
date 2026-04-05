// SPDX-License-Identifier: Elastic-2.0
// Copyright (c) Eric Cogen. All rights reserved.

namespace GauntletCI.Core.Evaluation;

public sealed class DiffParser
{
    public IReadOnlyList<ChangeBlock> Parse(string diffText)
    {
        List<ChangeBlock> blocks = [];
        string? currentFile = null;
        List<string> currentHunk = [];

        foreach (string line in diffText.Split('\n'))
        {
            if (line.StartsWith("diff --git ", StringComparison.Ordinal))
            {
                if (currentFile is not null && currentHunk.Count > 0)
                {
                    blocks.Add(new ChangeBlock(currentFile, [.. currentHunk]));
                    currentHunk.Clear();
                }

                string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                currentFile = parts.Length >= 4 ? parts[3].TrimStart('b', '/') : "unknown";
                continue;
            }

            if (currentFile is not null)
            {
                currentHunk.Add(line.TrimEnd('\r'));
            }
        }

        if (currentFile is not null && currentHunk.Count > 0)
        {
            blocks.Add(new ChangeBlock(currentFile, [.. currentHunk]));
        }

        return blocks;
    }
}

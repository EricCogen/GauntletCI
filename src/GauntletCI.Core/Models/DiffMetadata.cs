// SPDX-License-Identifier: Elastic-2.0
// Copyright (c) Eric Cogen. All rights reserved.

namespace GauntletCI.Core.Models;

public sealed record DiffMetadata(
    int LinesAdded,
    int LinesRemoved,
    int FilesChanged,
    bool TestFilesTouched,
    IReadOnlyList<string> Languages,
    bool DiffTrimmed,
    int EstimatedTokens);

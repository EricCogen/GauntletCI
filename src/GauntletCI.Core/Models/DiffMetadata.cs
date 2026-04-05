// SPDX-License-Identifier: Elastic-2.0
// Copyright (c) Eric Cogen. All rights reserved.

namespace GauntletCI.Core.Models;

public sealed record DiffMetadata(
    int LinesAdded,
    int LinesRemoved,
    int FilesChanged,
    bool TestFilesTouched,
    int TestFilesChanged,
    int TestFilesWithContentChanges,
    int TestFilesRenameOnly,
    int TestLinesAdded,
    int TestLinesRemoved,
    int TestAssertionLinesAdded,
    int TestSetupLinesAdded,
    bool TestsChangedWithoutAssertions,
    bool TestChangesAreRenameOrSetupChurn,
    IReadOnlyList<string> Languages,
    bool DiffTrimmed,
    int EstimatedTokens);

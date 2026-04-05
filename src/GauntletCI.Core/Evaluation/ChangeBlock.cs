// SPDX-License-Identifier: Elastic-2.0
// Copyright (c) Eric Cogen. All rights reserved.

namespace GauntletCI.Core.Evaluation;

public sealed record ChangeBlock(string FilePath, IReadOnlyList<string> HunkLines);

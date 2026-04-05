// SPDX-License-Identifier: Elastic-2.0
// Copyright (c) Eric Cogen. All rights reserved.

namespace GauntletCI.Core.Models;

public sealed record EvaluationRequest(
    string WorkingDirectory,
    bool FullMode,
    bool FastMode,
    string? Rule,
    bool JsonOutput,
    bool NoTelemetry,
    string? ExplicitTestCommand = null,
    string? ProvidedDiff = null,
    string? LocalEndpoint = null);

// SPDX-License-Identifier: Elastic-2.0
// Copyright (c) Eric Cogen. All rights reserved.

namespace GauntletCI.Core.Evaluation;

public interface ILlmClient
{
    Task<LlmResponse> EvaluateAsync(string model, string systemPrompt, string userPrompt, string apiKey, CancellationToken cancellationToken, string? baseUrl = null);
}

public sealed record LlmResponse(bool Success, string RawResponse, string? ErrorMessage = null);

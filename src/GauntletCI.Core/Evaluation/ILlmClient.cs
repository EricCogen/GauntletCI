namespace GauntletCI.Core.Evaluation;

public interface ILlmClient
{
    Task<LlmResponse> EvaluateAsync(string model, string systemPrompt, string userPrompt, bool fastMode, CancellationToken cancellationToken);
}

public sealed record LlmResponse(bool Success, string RawResponse, string? ErrorMessage = null);

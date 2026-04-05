namespace GauntletCI.Core.Evaluation;

public sealed class NoOpLlmClient : ILlmClient
{
    public Task<LlmResponse> EvaluateAsync(string model, string systemPrompt, string userPrompt, bool fastMode, CancellationToken cancellationToken)
    {
        return Task.FromResult(new LlmResponse(true, "[]"));
    }
}

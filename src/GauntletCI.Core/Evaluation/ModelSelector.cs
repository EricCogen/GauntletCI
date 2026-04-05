using GauntletCI.Core.Models;

namespace GauntletCI.Core.Evaluation;

public sealed class ModelSelector
{
    public ModelSelection Select(GauntletConfig config, bool fastMode)
    {
        List<(string Model, string KeyEnv)> preferred =
            fastMode
                ?
                [
                    ("claude-haiku-4-5", "ANTHROPIC_API_KEY"),
                    ("gpt-4o-mini", "OPENAI_API_KEY"),
                ]
                :
                [
                    ("claude-sonnet-4-5", "ANTHROPIC_API_KEY"),
                    ("gpt-4o", "OPENAI_API_KEY"),
                ];

        if (!string.IsNullOrWhiteSpace(config.Model))
        {
            string inferred = InferKeyEnvFromModel(config.Model);
            preferred.Insert(0, (config.Model, string.IsNullOrWhiteSpace(config.ApiKeyEnv) ? inferred : config.ApiKeyEnv));
        }

        foreach ((string model, string keyEnv) in preferred.Distinct())
        {
            string effectiveKeyEnv = string.IsNullOrWhiteSpace(keyEnv) ? InferKeyEnvFromModel(model) : keyEnv;
            string? apiKey = Environment.GetEnvironmentVariable(effectiveKeyEnv);
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                return new ModelSelection(model, effectiveKeyEnv, apiKey);
            }
        }

        return new ModelSelection(preferred[0].Model, preferred[0].KeyEnv, null);
    }

    private static string InferKeyEnvFromModel(string model)
    {
        return model.StartsWith("claude", StringComparison.OrdinalIgnoreCase)
            ? "ANTHROPIC_API_KEY"
            : "OPENAI_API_KEY";
    }
}

public sealed record ModelSelection(string Model, string ApiKeyEnv, string? ApiKey)
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}

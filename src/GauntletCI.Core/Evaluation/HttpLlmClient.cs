using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace GauntletCI.Core.Evaluation;

public sealed class HttpLlmClient(HttpClient httpClient) : ILlmClient
{
    public async Task<LlmResponse> EvaluateAsync(string model, string systemPrompt, string userPrompt, string apiKey, CancellationToken cancellationToken, string? baseUrl = null)
    {
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            return await CallOpenAiCompatibleAsync(model, systemPrompt, userPrompt, apiKey, baseUrl, cancellationToken).ConfigureAwait(false);
        }

        if (model.StartsWith("claude", StringComparison.OrdinalIgnoreCase))
        {
            return await CallAnthropicAsync(model, systemPrompt, userPrompt, apiKey, cancellationToken).ConfigureAwait(false);
        }

        return await CallOpenAiAsync(model, systemPrompt, userPrompt, apiKey, cancellationToken).ConfigureAwait(false);
    }

    private async Task<LlmResponse> CallOpenAiCompatibleAsync(string model, string systemPrompt, string userPrompt, string apiKey, string baseUrl, CancellationToken cancellationToken)
    {
        string endpoint = baseUrl.TrimEnd('/') + "/chat/completions";
        using HttpRequestMessage request = new(HttpMethod.Post, endpoint);
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        var payload = new
        {
            model,
            temperature = 0,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt },
            },
        };

        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return new LlmResponse(false, "", $"Local endpoint call failed ({(int)response.StatusCode}): {body}");
        }

        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;
        if (!root.TryGetProperty("choices", out JsonElement choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
        {
            return new LlmResponse(false, "", "Local endpoint response missing choices.");
        }

        JsonElement first = choices[0];
        if (!first.TryGetProperty("message", out JsonElement message) || !message.TryGetProperty("content", out JsonElement content))
        {
            return new LlmResponse(false, "", "Local endpoint response missing message content.");
        }

        return new LlmResponse(true, content.GetString() ?? "[]");
    }

    private async Task<LlmResponse> CallAnthropicAsync(string model, string systemPrompt, string userPrompt, string apiKey, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        var payload = new
        {
            model,
            max_tokens = 3000,
            temperature = 0,
            system = systemPrompt,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = userPrompt,
                },
            },
        };

        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return new LlmResponse(false, "", $"Anthropic call failed ({(int)response.StatusCode}): {body}");
        }

        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;
        if (!root.TryGetProperty("content", out JsonElement content) || content.ValueKind != JsonValueKind.Array)
        {
            return new LlmResponse(false, "", "Anthropic response missing content array.");
        }

        StringBuilder output = new();
        foreach (JsonElement item in content.EnumerateArray())
        {
            if (item.TryGetProperty("text", out JsonElement textElement))
            {
                output.AppendLine(textElement.GetString());
            }
        }

        return new LlmResponse(true, output.ToString().Trim());
    }

    private async Task<LlmResponse> CallOpenAiAsync(string model, string systemPrompt, string userPrompt, string apiKey, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var payload = new
        {
            model,
            temperature = 0,
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = systemPrompt,
                },
                new
                {
                    role = "user",
                    content = userPrompt,
                },
            },
        };

        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return new LlmResponse(false, "", $"OpenAI call failed ({(int)response.StatusCode}): {body}");
        }

        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;
        if (!root.TryGetProperty("choices", out JsonElement choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
        {
            return new LlmResponse(false, "", "OpenAI response missing choices.");
        }

        JsonElement first = choices[0];
        if (!first.TryGetProperty("message", out JsonElement message) || !message.TryGetProperty("content", out JsonElement content))
        {
            return new LlmResponse(false, "", "OpenAI response missing message content.");
        }

        return new LlmResponse(true, content.GetString() ?? "[]");
    }
}

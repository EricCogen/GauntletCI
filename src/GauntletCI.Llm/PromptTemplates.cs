// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Llm;

/// <summary>
/// Prompt templates for Phi-3 Mini.
/// Phi-3 uses &lt;|user|&gt;, &lt;|end|&gt;, &lt;|assistant|&gt; chat tokens.
/// </summary>
public static class PromptTemplates
{
    private const string UserStart = "<|user|>\n";
    private const string UserEnd = "<|end|>\n";
    private const string AssistantStart = "<|assistant|>\n";

    /// <summary>Builds a prompt to enrich a single finding with a one-sentence explanation.</summary>
    public static string EnrichFinding(string ruleId, string ruleName, string summary, string evidence) =>
        $"{UserStart}" +
        $"You are a code review assistant. A rule called \"{ruleName}\" ({ruleId}) flagged this issue:\n\n" +
        $"Summary: {summary}\n" +
        $"Evidence: {evidence}\n\n" +
        $"Provide a single sentence (max 30 words) explaining WHY this is risky in plain English for a developer. " +
        $"Do not repeat the summary. Be direct and specific." +
        $"{UserEnd}" +
        $"{AssistantStart}";

    /// <summary>Builds a prompt to summarize a full set of findings into one paragraph.</summary>
    public static string SummarizeReport(IEnumerable<string> findingSummaries) =>
        $"{UserStart}" +
        $"You are a code review assistant. A pull request was analysed and produced these findings:\n\n" +
        string.Join("\n", findingSummaries.Select((s, i) => $"{i + 1}. {s}")) +
        $"\n\nWrite one paragraph (max 60 words) summarising the overall risk level and the top concern. " +
        $"Be concise and direct." +
        $"{UserEnd}" +
        $"{AssistantStart}";
}

// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Llm;

/// <summary>
/// Prompt templates for Phi-3 Mini.
/// Phi-3 uses &lt;|user|&gt;, &lt;|end|&gt;, &lt;|assistant|&gt; chat tokens.
/// </summary>
public static class PromptTemplates
{
    private const string SystemStart = "<|system|>\n";
    private const string UserStart = "<|user|>\n";
    private const string UserEnd = "<|end|>\n";
    private const string AssistantStart = "<|assistant|>\n";

    private const string AntiHallucinationSystem =
        "You are a precise code review assistant. Strict rules:\n" +
        "1. Only state facts directly supported by the evidence provided.\n" +
        "2. Do not invent technical details, CVE numbers, error types, or outcomes not visible in the code.\n" +
        "3. Do not lie or fabricate information to sound more authoritative.\n" +
        "4. If you are uncertain about a specific detail, omit it rather than guess.\n" +
        "5. Do not repeat or rephrase the summary you were given.\n" +
        "6. Output exactly one sentence. Maximum 30 words.";

    /// <summary>Builds a prompt to enrich a single finding with a one-sentence explanation.</summary>
    /// <param name="ruleId">The rule identifier (e.g., <c>GCI0001</c>) for context.</param>
    /// <param name="ruleName">Human-readable rule name shown to the model.</param>
    /// <param name="summary">Short description of what was flagged.</param>
    /// <param name="evidence">The code snippet or diff evidence the rule matched on.</param>
    public static string EnrichFinding(string ruleId, string ruleName, string summary, string evidence) =>
        $"{UserStart}" +
        $"You are a code review assistant. A rule called \"{ruleName}\" ({ruleId}) flagged this issue:\n\n" +
        $"Summary: {summary}\n" +
        $"Evidence: {evidence}\n\n" +
        $"Provide a single sentence (max 30 words) explaining WHY this is risky in plain English for a developer. " +
        $"Do not repeat the summary. Be direct and specific." +
        $"{UserEnd}" +
        $"{AssistantStart}";

    /// <summary>
    /// Builds a finding enrichment prompt with strict anti-hallucination system instructions.
    /// Uses Phi-3's <c>&lt;|system|&gt;</c> token to inject rules before the user turn.
    /// </summary>
    public static string EnrichFindingConstrained(string ruleId, string ruleName, string summary, string evidence) =>
        $"{SystemStart}{AntiHallucinationSystem}{UserEnd}" +
        $"{UserStart}" +
        $"A rule called \"{ruleName}\" ({ruleId}) flagged this issue:\n\n" +
        $"Summary: {summary}\n" +
        $"Evidence: {evidence}\n\n" +
        $"Explain WHY this is risky in plain English for a developer." +
        $"{UserEnd}" +
        $"{AssistantStart}";

    /// <param name="findingSummaries">One summary string per finding, in any order.</param>
    public static string SummarizeReport(IEnumerable<string> findingSummaries) =>
        $"{UserStart}" +
        $"You are a code review assistant. A pull request was analysed and produced these findings:\n\n" +
        string.Join("\n", findingSummaries.Select((s, i) => $"{i + 1}. {s}")) +
        $"\n\nWrite one paragraph (max 60 words) summarising the overall risk level and the top concern. " +
        $"Be concise and direct." +
        $"{UserEnd}" +
        $"{AssistantStart}";

    /// <summary>
    /// Extracts a single-sentence expert fact from a PR or issue body for LLM distillation.
    /// </summary>
    /// <param name="title">Issue or PR title used to orient the model.</param>
    /// <param name="body">Issue or PR body text; truncated to 2000 characters to stay within token limits.</param>
    public static string ExtractExpertFact(string title, string body)
    {
        var truncatedBody = body.Length > 2000 ? body[..2000] + "…" : body;
        return
            $"{UserStart}" +
            $"From this GitHub discussion:\n\nTitle: {title}\n\nContent:\n{truncatedBody}\n\n" +
            $"Extract one single-sentence expert fact about .NET performance, concurrency, or resource management. " +
            $"The fact must be specific, actionable, and describe exact behavior or a concrete risk. " +
            $"Do not start with 'The' or 'This'. Output ONLY the fact sentence, nothing else." +
            $"{UserEnd}" +
            $"{AssistantStart}";
    }
}

namespace GauntletCI.Core.Evaluation;

public sealed class PromptBuilder
{
    public string BuildSystemPrompt(string fullRulesText)
    {
        return $"""
GauntletCI is a pre-commit review system. You must evaluate the provided changeset using the full rule definitions exactly as authored.

Rules:
{fullRulesText}

Return only a JSON array of finding objects. Omit rules with no findings entirely.
Each finding must cite specific evidence (file, symbol, or line-level indicator).
""";
    }

    public string BuildUserPrompt(string assembledContext) => assembledContext;
}

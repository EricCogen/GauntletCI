namespace GauntletCI.Core.Evaluation;

public sealed class RulesTextProvider
{
    private const string RulesRelativePath = "Rules/gauntletci-rules.txt";

    public string LoadRulesText()
    {
        string baseDir = AppContext.BaseDirectory;
        string rulesPath = Path.Combine(baseDir, RulesRelativePath);
        if (!File.Exists(rulesPath))
        {
            throw new FileNotFoundException($"Rules file was not found at {rulesPath}");
        }

        return File.ReadAllText(rulesPath);
    }
}

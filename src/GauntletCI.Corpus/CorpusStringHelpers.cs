// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Corpus;

internal static class CorpusStringHelpers
{
    internal static string GuessLanguage(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".cs"   => "C#",
            ".ts"   => "TypeScript",
            ".js"   => "JavaScript",
            ".py"   => "Python",
            ".go"   => "Go",
            ".java" => "Java",
            ".rs"   => "Rust",
            ".rb"   => "Ruby",
            _       => "",
        };
    }
}

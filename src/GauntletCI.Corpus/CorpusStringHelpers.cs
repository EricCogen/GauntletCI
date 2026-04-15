// SPDX-License-Identifier: Elastic-2.0
using System.Net;

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

    internal static bool IsRateLimited(HttpResponseMessage resp)
    {
        if (resp.StatusCode == HttpStatusCode.TooManyRequests) return true;
        if (resp.StatusCode == HttpStatusCode.Forbidden &&
            resp.Headers.TryGetValues("x-ratelimit-remaining", out var vals) &&
            vals.FirstOrDefault() == "0") return true;
        return false;
    }
}

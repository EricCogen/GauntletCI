// SPDX-License-Identifier: Elastic-2.0
using System.Net;
using System.Net.Http;
using GauntletCI.Corpus;
using GauntletCI.Corpus.Labeling;

namespace GauntletCI.Tests.Corpus;

public class CorpusStringHelpersTests
{
    // ── GuessLanguage ─────────────────────────────────────────────────────────

    [Fact]
    public void GuessLanguage_CsExtension_ReturnsCSharp()
    {
        Assert.Equal("C#", CorpusStringHelpers.GuessLanguage("foo.cs"));
    }

    [Fact]
    public void GuessLanguage_TsExtension_ReturnsTypeScript()
    {
        Assert.Equal("TypeScript", CorpusStringHelpers.GuessLanguage("foo.ts"));
    }

    [Fact]
    public void GuessLanguage_JsExtension_ReturnsJavaScript()
    {
        Assert.Equal("JavaScript", CorpusStringHelpers.GuessLanguage("foo.js"));
    }

    [Fact]
    public void GuessLanguage_PyExtension_ReturnsPython()
    {
        Assert.Equal("Python", CorpusStringHelpers.GuessLanguage("foo.py"));
    }

    [Fact]
    public void GuessLanguage_GoExtension_ReturnsGo()
    {
        Assert.Equal("Go", CorpusStringHelpers.GuessLanguage("foo.go"));
    }

    [Fact]
    public void GuessLanguage_JavaExtension_ReturnsJava()
    {
        Assert.Equal("Java", CorpusStringHelpers.GuessLanguage("foo.java"));
    }

    [Fact]
    public void GuessLanguage_RsExtension_ReturnsRust()
    {
        Assert.Equal("Rust", CorpusStringHelpers.GuessLanguage("foo.rs"));
    }

    [Fact]
    public void GuessLanguage_RbExtension_ReturnsRuby()
    {
        Assert.Equal("Ruby", CorpusStringHelpers.GuessLanguage("foo.rb"));
    }

    [Fact]
    public void GuessLanguage_UnknownExtension_ReturnsEmpty()
    {
        Assert.Equal("", CorpusStringHelpers.GuessLanguage("foo.xml"));
    }

    [Fact]
    public void GuessLanguage_NoExtension_ReturnsEmpty()
    {
        Assert.Equal("", CorpusStringHelpers.GuessLanguage("Makefile"));
    }

    [Fact]
    public void GuessLanguage_UppercaseExtension_ReturnsCSharp()
    {
        // Extension is lowercased internally: ".CS" → ".cs" → "C#"
        Assert.Equal("C#", CorpusStringHelpers.GuessLanguage("foo.CS"));
    }

    [Fact]
    public void GuessLanguage_FullPath_ReturnsCorrectLanguage()
    {
        Assert.Equal("TypeScript", CorpusStringHelpers.GuessLanguage("src/Services/MyService.ts"));
    }

    // ── IsRateLimited ─────────────────────────────────────────────────────────

    [Fact]
    public void IsRateLimited_TooManyRequests_ReturnsTrue()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        Assert.True(CorpusStringHelpers.IsRateLimited(response));
    }

    [Fact]
    public void IsRateLimited_ForbiddenWithZeroRemaining_ReturnsTrue()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.Forbidden);
        response.Headers.Add("x-ratelimit-remaining", "0");
        Assert.True(CorpusStringHelpers.IsRateLimited(response));
    }

    [Fact]
    public void IsRateLimited_ForbiddenWithNonZeroRemaining_ReturnsFalse()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.Forbidden);
        response.Headers.Add("x-ratelimit-remaining", "5");
        Assert.False(CorpusStringHelpers.IsRateLimited(response));
    }

    // ── NormalizeOllamaUrls ────────────────────────────────────────────────────

    [Fact]
    public void NormalizeOllamaUrls_NullInput_ReturnsEmpty()
    {
        Assert.Empty(OllamaUrlNormalizer.Normalize(null));
    }

    [Fact]
    public void NormalizeOllamaUrls_EmptyInput_ReturnsEmpty()
    {
        Assert.Empty(OllamaUrlNormalizer.Normalize([]));
    }

    [Fact]
    public void NormalizeOllamaUrls_SingleUrl_ReturnsTrimmedEntry()
    {
        var result = OllamaUrlNormalizer.Normalize(["http://localhost:11434/"]);
        Assert.Single(result);
        Assert.Equal("http://localhost:11434", result[0]);
    }

    [Fact]
    public void NormalizeOllamaUrls_CommaSeparated_SplitsIntoMultipleEntries()
    {
        var result = OllamaUrlNormalizer.Normalize(["http://a:11434,http://b:11434"]);
        Assert.Equal(2, result.Count);
        Assert.Contains("http://a:11434", result);
        Assert.Contains("http://b:11434", result);
    }

    [Fact]
    public void NormalizeOllamaUrls_DuplicatesCaseInsensitive_Deduplicates()
    {
        var result = OllamaUrlNormalizer.Normalize(
            ["http://Localhost:11434", "http://localhost:11434"]);
        Assert.Single(result);
    }

    [Fact]
    public void NormalizeOllamaUrls_BlankEntries_Filtered()
    {
        var result = OllamaUrlNormalizer.Normalize(["  ", "", "http://x:11434"]);
        Assert.Single(result);
        Assert.Equal("http://x:11434", result[0]);
    }

    [Fact]
    public void IsRateLimited_ForbiddenWithNoRateLimitHeader_ReturnsFalse()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.Forbidden);
        Assert.False(CorpusStringHelpers.IsRateLimited(response));
    }

    [Fact]
    public void IsRateLimited_OkResponse_ReturnsFalse()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK);
        Assert.False(CorpusStringHelpers.IsRateLimited(response));
    }

    [Fact]
    public void IsRateLimited_NotFound_ReturnsFalse()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.NotFound);
        Assert.False(CorpusStringHelpers.IsRateLimited(response));
    }
}

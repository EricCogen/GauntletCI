// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Cli.Utilities;

namespace GauntletCI.Cli.Tests;

/// <summary>
/// Tests for the CommandSuggester fuzzy matching utility.
/// Validates Levenshtein distance calculation and suggestion generation.
/// </summary>
public class CommandSuggesterTests
{
    [Fact]
    public void LevenshteinDistance_IdenticalStrings_ReturnsZero()
    {
        var distance = CommandSuggester.LevenshteinDistance("hello", "hello");
        Assert.Equal(0, distance);
    }

    [Fact]
    public void LevenshteinDistance_EmptyStrings_ReturnsZero()
    {
        var distance = CommandSuggester.LevenshteinDistance("", "");
        Assert.Equal(0, distance);
    }

    [Fact]
    public void LevenshteinDistance_OneEmptyString_ReturnsLength()
    {
        var distance1 = CommandSuggester.LevenshteinDistance("hello", "");
        Assert.Equal(5, distance1);

        var distance2 = CommandSuggester.LevenshteinDistance("", "world");
        Assert.Equal(5, distance2);
    }

    [Fact]
    public void LevenshteinDistance_SingleCharDifference_ReturnsOne()
    {
        // Single substitution
        var distance = CommandSuggester.LevenshteinDistance("cat", "bat");
        Assert.Equal(1, distance);
    }

    [Fact]
    public void LevenshteinDistance_SingleCharInsertion_ReturnsOne()
    {
        // Insertion
        var distance = CommandSuggester.LevenshteinDistance("cat", "cats");
        Assert.Equal(1, distance);
    }

    [Fact]
    public void LevenshteinDistance_SingleCharDeletion_ReturnsOne()
    {
        // Deletion
        var distance = CommandSuggester.LevenshteinDistance("cats", "cat");
        Assert.Equal(1, distance);
    }

    [Fact]
    public void LevenshteinDistance_SingleCharTransposition_ReturnsTwo()
    {
        // Transposition (no single operation, requires 2 edits in standard Levenshtein)
        var distance = CommandSuggester.LevenshteinDistance("ab", "ba");
        Assert.Equal(2, distance);
    }

    [Fact]
    public void LevenshteinDistance_MultipleEdits_CalculatedCorrectly()
    {
        // "kitten" -> "sitting": 3 edits (substitution of k->s, of e->i, insertion of g)
        var distance = CommandSuggester.LevenshteinDistance("kitten", "sitting");
        Assert.Equal(3, distance);
    }

    [Fact]
    public void LevenshteinDistance_CaseSensitive_TreatsUpperAndLowerAsDifferent()
    {
        // Before normalization, uppercase and lowercase are different
        var distance = CommandSuggester.LevenshteinDistance("Hello", "hello");
        Assert.Equal(1, distance);
    }

    [Fact]
    public void FindClosestMatch_ExactMatch_ReturnsMatch()
    {
        var candidates = new[] { "analyze", "audit", "baseline" };
        var result = CommandSuggester.FindClosestMatch("analyze", candidates);
        Assert.Equal("analyze", result);
    }

    [Fact]
    public void FindClosestMatch_TypoOneCharOff_ReturnsSuggestion()
    {
        var candidates = new[] { "analyze", "audit", "baseline" };
        var result = CommandSuggester.FindClosestMatch("analysie", candidates);
        Assert.Equal("analyze", result);
    }

    [Fact]
    public void FindClosestMatch_TypoTwoCharsOff_ReturnsSuggestion()
    {
        var candidates = new[] { "analyze", "audit", "baseline" };
        var result = CommandSuggester.FindClosestMatch("analize", candidates);
        // "analize" (7 letters) vs "analyze" (7 letters), 1 char difference
        Assert.Equal("analyze", result);
    }

    [Fact]
    public void FindClosestMatch_TooManyDifferences_ReturnsNull()
    {
        var candidates = new[] { "analyze", "audit", "baseline" };
        var result = CommandSuggester.FindClosestMatch("foobar", candidates, maxDistance: 2);
        Assert.Null(result);
    }

    [Fact]
    public void FindClosestMatch_CaseInsensitive_MatchesIgnoringCase()
    {
        var candidates = new[] { "analyze", "audit", "baseline" };
        var result = CommandSuggester.FindClosestMatch("ANALYZE", candidates);
        Assert.Equal("analyze", result);
    }

    [Fact]
    public void FindClosestMatch_MissingDashes_FindsFlagWithDashes()
    {
        var candidates = new[] { "--diff", "--staged", "--verbose" };
        var result = CommandSuggester.FindClosestMatch("--dif", candidates);
        Assert.Equal("--diff", result);
    }

    [Fact]
    public void FindClosestMatch_ExtraCharacter_FindsCorrectOption()
    {
        var candidates = new[] { "--diff", "--staged", "--verbose" };
        var result = CommandSuggester.FindClosestMatch("--difff", candidates);
        Assert.Equal("--diff", result);
    }

    [Fact]
    public void FindCloseMatches_MultipleMatches_ReturnsSorted()
    {
        var candidates = new[] { "analyze", "analyse", "audit", "baseline" };
        var results = CommandSuggester.FindCloseMatches("analyz", candidates, maxDistance: 2);
        
        Assert.NotEmpty(results);
        // Both "analyze" and "analyse" are distance 1 away, should both be included
        Assert.Contains("analyze", results);
    }

    [Fact]
    public void FindCloseMatches_RespectMaxSuggestions_LimitResults()
    {
        var candidates = new[] { "a", "ab", "abc", "abcd", "abcde" };
        var results = CommandSuggester.FindCloseMatches("abcdef", candidates, maxDistance: 3, maxSuggestions: 2);
        
        // Should return at most 2 suggestions (closest matches)
        Assert.True(results.Count <= 2);
    }

    [Fact]
    public void FindCloseMatches_NoCloseMatches_ReturnsEmpty()
    {
        var candidates = new[] { "analyze", "audit", "baseline" };
        var results = CommandSuggester.FindCloseMatches("foobar", candidates, maxDistance: 2);
        
        Assert.Empty(results);
    }

    [Fact]
    public void FormatSuggestionMessage_NoSuggestions_ReturnsSimpleMessage()
    {
        var message = CommandSuggester.FormatSuggestionMessage("--invalid", new List<string>());
        Assert.Equal("Unknown option or command: --invalid", message);
    }

    [Fact]
    public void FormatSuggestionMessage_OneSuggestion_ReturnsDidYouMean()
    {
        var message = CommandSuggester.FormatSuggestionMessage("--dif", new List<string> { "--diff" });
        Assert.Contains("Did you mean: --diff?", message);
        Assert.Contains("Unknown option or command: --dif", message);
    }

    [Fact]
    public void FormatSuggestionMessage_MultipleSuggestions_ReturnsFormattedList()
    {
        var suggestions = new List<string> { "--diff", "--staged", "--verbose" };
        var message = CommandSuggester.FormatSuggestionMessage("--did", suggestions);
        
        Assert.Contains("Did you mean one of these?", message);
        Assert.Contains("--diff", message);
        Assert.Contains("--staged", message);
        Assert.Contains("--verbose", message);
    }

    [Fact]
    public void CommandSuggester_RealWorldScenario_AnalyzeCommand()
    {
        var analyzeOptions = new[]
        {
            "--diff", "--commit", "--staged", "--unstaged", "--all-changes",
            "--codebase", "--repo", "--output", "--no-llm", "--with-llm",
            "--ascii", "--no-banner", "--verbose", "--severity", "--sensitivity",
            "--no-baseline", "--show-context"
        };

        // User typos "--difff"
        var result1 = CommandSuggester.FindClosestMatch("--difff", analyzeOptions);
        Assert.Equal("--diff", result1);

        // User typos "--ouput"
        var result2 = CommandSuggester.FindClosestMatch("--ouput", analyzeOptions);
        Assert.Equal("--output", result2);

        // User typos "--verbos"
        var result3 = CommandSuggester.FindClosestMatch("--verbos", analyzeOptions);
        Assert.Equal("--verbose", result3);
    }

    [Fact]
    public void CommandSuggester_RealWorldScenario_MultipleCommands()
    {
        var commands = new[] { "analyze", "audit", "baseline", "doctor", "init", "ignore" };

        // User typos "analyz"
        var result1 = CommandSuggester.FindClosestMatch("analyz", commands);
        Assert.Equal("analyze", result1);

        // User typos "audet"
        var result2 = CommandSuggester.FindClosestMatch("audet", commands);
        Assert.Equal("audit", result2);

        // User typos "baseline" with "basline"
        var result3 = CommandSuggester.FindClosestMatch("basline", commands);
        Assert.Equal("baseline", result3);
    }

    [Fact]
    public void CommandSuggester_NoSuggestion_WhenTooDisimilar()
    {
        var options = new[] { "--diff", "--staged", "--verbose" };
        
        // "xyz" is too different from all options
        var result = CommandSuggester.FindClosestMatch("xyz", options, maxDistance: 2);
        Assert.Null(result);
    }

    [Fact]
    public void CommandSuggester_AllCharactersWrong_ReturnsNull()
    {
        var options = new[] { "--analyze", "--audit" };
        
        // "-------" has 7 characters, "--analyze" also has 9, lots of mismatches
        var result = CommandSuggester.FindClosestMatch("-------", options, maxDistance: 1);
        Assert.Null(result);
    }

    [Fact]
    public void CommandSuggester_PartialMatch_WithDashes()
    {
        var options = new[] { "--with-llm", "--with-coverage", "--with-ticket-context" };
        
        // User types "--with-lm" (missing one 'l')
        var result = CommandSuggester.FindClosestMatch("--with-lm", options);
        Assert.Equal("--with-llm", result);
    }
}

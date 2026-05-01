// SPDX-License-Identifier: Elastic-2.0
using System.IO;
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// Context signal analyzer for behavioral changes with semantic understanding.
/// Provides confidence boost signals based on file path, commit message, and test changes.
/// </summary>
internal class BehavioralChangeContextAnalyzer
{
    // File paths that indicate security-critical components
    private static readonly string[] SecurityCriticalPaths = [
        "Http2", "Kestrel", "TLS", "SSL", "Crypto", "Auth",
        "Uri", "Parsing", "Validation", "Security", "Hmac", "Hash",
        "Decrypt", "Encrypt", "Certificate", "Token", "Key"
    ];

    // Commit message keywords indicating security-focused changes
    private static readonly string[] SecurityKeywords = [
        "CVE", "security", "vulnerability", "fix", "DoS", "infinite",
        "loop", "exhaustion", "exception", "error", "RFC", "compliance",
        "boundary", "validation", "attack", "malicious", "payload", "regression"
    ];

    // Test patterns indicating security-focused test additions
    private static readonly string[] SecurityTestPatterns = [
        "Error", "Exception", "Timeout", "Exhaustion", "Attack",
        "Craft", "Malicious", "Payload", "CVE", "Boundary", "Validation"
    ];

    public double CalculateContextBoost(DiffContext diff)
    {
        double boost = 0.0;

        // Signal 1: Security-critical file path (+0.20)
        if (diff.Files.Any(f => IsSecurityCriticalPath(f.NewPath ?? f.OldPath)))
            boost += 0.20;

        // Signal 2: Security-related commit message (+0.15)
        if (!string.IsNullOrEmpty(diff.CommitMessage) && HasSecurityKeywords(diff.CommitMessage))
            boost += 0.15;

        // Signal 3: Test changes with security patterns (+0.15)
        if (HasSecurityTestChanges(diff))
            boost += 0.15;

        return Math.Min(boost, 0.50); // Cap at +0.50 for maximum confidence boost
    }

    private static bool IsSecurityCriticalPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        return SecurityCriticalPaths.Any(p => 
            path.Contains(p, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasSecurityKeywords(string commitMessage)
    {
        var lowerMessage = commitMessage.ToLowerInvariant();
        return SecurityKeywords.Any(k => 
            lowerMessage.Contains(k.ToLowerInvariant(), StringComparison.Ordinal));
    }

    private static bool HasSecurityTestChanges(DiffContext diff)
    {
        var testFiles = diff.Files.Where(f => 
            WellKnownPatterns.IsTestFile(f.NewPath ?? f.OldPath)).ToList();

        if (testFiles.Count == 0) return false;

        return testFiles.Any(f =>
        {
            var testContent = string.Join(" ", f.AddedLines.Select(l => l.Content));
            return SecurityTestPatterns.Any(p =>
                testContent.Contains(p, StringComparison.OrdinalIgnoreCase));
        });
    }
}

/// <summary>
/// GCI0003, Behavioral Change Detection
/// Detects removed logic lines, changed method signatures, and cryptographic boundary changes.
/// Enhanced with context signals for improved confidence scoring.
/// </summary>
public class GCI0003_BehavioralChangeDetection : RuleBase
{
    public override string Id => "GCI0003";
    public override string Name => "Behavioral Change Detection";

    // Narrower keyword set: "else", "&&", "||" appear in virtually every C# file so
    // counting them as "logic" drives massive false-positive rates.
    private static readonly string[] LogicKeywords = ["return ", "throw ", "if (", "if("];
    private static readonly string[] AccessModifiers = ["public ", "private ", "protected ", "internal "];

    // Cryptographic methods where argument changes represent behavioral/security boundaries
    private static readonly string[] CryptographicMethods = [
        "ComputeHmac", "ComputeHash", "Encrypt", "Decrypt",
        "Sign", "Verify", "GetHashCode", "EncryptionAsync", "DecryptionAsync"
    ];

    private static readonly BehavioralChangeContextAnalyzer ContextAnalyzer = new();

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var diff = context.Diff;
        var findings = new List<Finding>();

        CheckLogicRemovedWithoutTests(diff, findings);
        CheckMethodSignatureChanges(diff, findings);
        CheckCryptographicBoundaryChanges(context, findings);

        // Apply context-based confidence boosts to all findings
        var contextBoost = ContextAnalyzer.CalculateContextBoost(diff);
        if (contextBoost > 0.0)
        {
            ApplyContextBoost(findings, contextBoost);
        }

        return Task.FromResult(findings);
    }

    private static void ApplyContextBoost(List<Finding> findings, double boost)
    {
        foreach (var finding in findings)
        {
            // Boost confidence levels based on context signals
            var newConfidence = finding.Confidence switch
            {
                Confidence.Low => boost >= 0.30 ? Confidence.Medium : Confidence.Low,
                Confidence.Medium => boost >= 0.40 ? Confidence.High : Confidence.Medium,
                Confidence.High => Confidence.High, // Already high confidence
                _ => finding.Confidence
            };

            // Update the finding's confidence if it changed
            if (newConfidence != finding.Confidence)
            {
                finding.Confidence = newConfidence;
                if (string.IsNullOrEmpty(finding.Evidence))
                    finding.Evidence = $"Context signals (+{boost:P0}): security-critical path, keywords, or test changes detected.";
                else
                    finding.Evidence += $" [Context boost +{boost:P0}]";
            }
        }
    }

    private void CheckLogicRemovedWithoutTests(DiffContext diff, List<Finding> findings)
    {
        // Only count logic removals from production files: skip test and generated files.
        var removedLogicLines = diff.Files
            .Where(f => !WellKnownPatterns.IsTestFile(f.NewPath) && !WellKnownPatterns.IsGeneratedFile(f.NewPath))
            .SelectMany(f => f.RemovedLines)
            .Where(l => !l.Content.TrimStart().StartsWith("//", StringComparison.Ordinal)
                     && LogicKeywords.Any(k => l.Content.Contains(k, StringComparison.Ordinal)))
            .ToList();

        // Threshold of 15: small refactors routinely remove 5-10 lines of control flow.
        // Only a large-scale logic deletion (whole method body stripped, significant function
        // rewrite) should trigger without accompanying test changes.
        if (removedLogicLines.Count < 15) return;

        bool hasTestChanges = diff.Files.Any(f =>
            f.NewPath.Contains("Test", StringComparison.OrdinalIgnoreCase) ||
            f.NewPath.Contains("Spec", StringComparison.OrdinalIgnoreCase));

        if (!hasTestChanges)
        {
            var examples = removedLogicLines
                .Take(3)
                .Select(l => l.Content.Trim());

            findings.Add(CreateFinding(
                summary: $"{removedLogicLines.Count} logic line(s) removed with no corresponding test changes.",
                evidence: $"Removed logic: {string.Join(" | ", examples)}",
                whyItMatters: "Removing control-flow logic without updating tests may silently break behaviour that was previously covered.",
                suggestedAction: "Add or update tests to verify the removed logic paths are intentionally no longer needed.",
                confidence: Confidence.Low));
        }
    }

    private void CheckMethodSignatureChanges(DiffContext diff, List<Finding> findings)
    {
        // Accumulate per-file results; cross-file dedup prevents explosion on wide diffs.
        var fileIncompatible = new List<(DiffFile File, List<(string Name, DiffLine Removed, DiffLine Added)> Items)>();
        var fileCompatible   = new List<(DiffFile File, List<(string Name, DiffLine Removed, DiffLine Added)> Items)>();

        foreach (var file in diff.Files)
        {
            if (WellKnownPatterns.IsTestFile(file.NewPath) || WellKnownPatterns.IsGeneratedFile(file.NewPath)) continue;

            var removedSigs = file.RemovedLines
                .Where(l => { var t = l.Content.TrimStart(); return HasAccessModifier(t) && l.Content.Contains('('); })
                .ToList();

            var addedSigs = file.AddedLines
                .Where(l => { var t = l.Content.TrimStart(); return HasAccessModifier(t) && l.Content.Contains('('); })
                .ToList();

            var incompatible = new List<(string Name, DiffLine RemovedLine, DiffLine AddedLine)>();
            var compatible   = new List<(string Name, DiffLine RemovedLine, DiffLine AddedLine)>();

            foreach (var removed in removedSigs)
            {
                if (removed.Content.TrimStart().StartsWith("private ", StringComparison.Ordinal)) continue;

                var removedName = ExtractMethodName(removed.Content);
                if (removedName is null) continue;

                var matchingAdded = addedSigs.FirstOrDefault(a => ExtractMethodName(a.Content) == removedName);
                if (matchingAdded is not null && NormalizeSignature(removed.Content) != NormalizeSignature(matchingAdded.Content))
                {
                    if (WellKnownPatterns.IsBackwardCompatibleExtension(removed.Content, matchingAdded.Content))
                        compatible.Add((removedName, removed, matchingAdded));
                    else
                        incompatible.Add((removedName, removed, matchingAdded));
                }
            }

            if (incompatible.Count > 0) fileIncompatible.Add((file, incompatible));
            if (compatible.Count > 0)   fileCompatible.Add((file, compatible));
        }

        EmitSigFindings(findings, fileIncompatible,
            single1Summary:  (name, file) => $"Method signature changed: '{name}' in {file.NewPath}",
            singleNSummary:  (count, file) => $"{count} method signatures changed (incompatible) in {file.NewPath}",
            crossSummary:    (total, fcount) => $"{total} method signatures changed (incompatible) across {fcount} files",
            whyItMatters:    "Signature changes can break callers that haven't been updated.",
            suggestedAction: "Verify all callers are updated and consider adding an overload for backward compatibility.",
            confidence:      Confidence.Medium);

        EmitSigFindings(findings, fileCompatible,
            single1Summary:  (name, file) => $"Backward-compatible signature extension: '{name}' in {file.NewPath}",
            singleNSummary:  (count, file) => $"{count} backward-compatible signature extensions in {file.NewPath}",
            crossSummary:    (total, fcount) => $"{total} backward-compatible signature extensions across {fcount} files",
            whyItMatters:    "New parameters have default values (backward-compatible), but callers using positional arguments may need review.",
            suggestedAction: "Confirm all existing callers still compile and behave correctly with the new defaults.",
            confidence:      Confidence.Low);
    }

    private void EmitSigFindings(
        List<Finding> findings,
        List<(DiffFile File, List<(string Name, DiffLine Removed, DiffLine Added)> Items)> perFile,
        Func<string, DiffFile, string> single1Summary,
        Func<int, DiffFile, string>    singleNSummary,
        Func<int, int, string>         crossSummary,
        string whyItMatters,
        string suggestedAction,
        Confidence confidence)
    {
        if (perFile.Count == 0) return;

        if (perFile.Count <= 3)
        {
            foreach (var (file, items) in perFile)
            {
                // Guard: Reduce confidence if this file has patterns suggesting test code with non-critical changes
                var adjustedConfidence = AdjustConfidenceForContext(confidence, file);

                var names = FormatNames(items.Select(c => c.Name));
                var (_, firstRemoved, firstAdded) = items[0];
                var summary = items.Count == 1
                    ? single1Summary(items[0].Name, file)
                    : singleNSummary(items.Count, file);
                var evidence = items.Count == 1
                    ? $"Was: {firstRemoved.Content.Trim()} | Now: {firstAdded.Content.Trim()}"
                    : $"Changed: {names} | e.g. Was: {firstRemoved.Content.Trim()} | Now: {firstAdded.Content.Trim()}";
                findings.Add(CreateFinding(file, summary, evidence, whyItMatters, suggestedAction, adjustedConfidence, firstAdded));
            }
        }
        else
        {
            int total = perFile.Sum(x => x.Items.Count);
            var fileList = FormatFileList(perFile.Select(x => (x.File, x.Items.Count)));
            findings.Add(CreateFinding(
                summary:         crossSummary(total, perFile.Count),
                evidence:        $"Files: {fileList}",
                whyItMatters:    whyItMatters,
                suggestedAction: suggestedAction,
                confidence:      confidence));
        }
    }

    private static string FormatNames(IEnumerable<string> names)
    {
        var list = names.ToList();
        var preview = string.Join(", ", list.Take(3).Select(n => $"'{n}'"));
        return preview + (list.Count > 3 ? $" (+{list.Count - 3} more)" : "");
    }

    private static string FormatFileList(IEnumerable<(DiffFile File, int Count)> files)
    {
        var list = files.ToList();
        var preview = string.Join(", ", list.Take(3)
                        .Select(x => $"{Path.GetFileName(x.File.NewPath ?? x.File.OldPath)} ({x.Count})"));
        return preview + (list.Count > 3 ? $" (+{list.Count - 3} more files)" : "");
    }

    private static string? ExtractMethodName(string line)
    {
        var parenIdx = line.IndexOf('(');
        if (parenIdx <= 0) return null;
        var before = line[..parenIdx].TrimEnd();
        var lastSpace = before.LastIndexOf(' ');
        if (lastSpace < 0) return null;
        return before[(lastSpace + 1)..];
    }

    private static string NormalizeSignature(string sig)
    {
        var s = sig.Replace("async ", "", StringComparison.Ordinal).Trim();
        var open = s.IndexOf('(');
        if (open < 0) return s;

        // Find the matching closing paren with string-literal-aware depth tracking
        // so default values like string s = ")" don't cause early termination.
        int depth = 0;
        bool inString = false;
        char delim = '"';
        int closeIdx = -1;
        for (int i = open; i < s.Length; i++)
        {
            char c = s[i];
            if (inString)
            {
                if (c == '\\') { i++; continue; } // skip escaped char in regular string
                if (c == delim) inString = false;
                continue;
            }
            if (c is '"' or '\'') { inString = true; delim = c; continue; }
            if (c == '(') depth++;
            else if (c == ')') { depth--; if (depth == 0) { closeIdx = i; break; } }
        }
        if (closeIdx < 0) return s;

        // Include where-clauses (which precede the body) but drop method body (=> or {).
        for (int i = closeIdx + 1; i < s.Length; i++)
        {
            if (s[i] == '{') return s[..i].TrimEnd();
            if (s[i] == '=' && i + 1 < s.Length && s[i + 1] == '>') return s[..i].TrimEnd();
        }
        return s;
    }

    // Returns true when the trimmed line starts with a C# access modifier,
    // accounting for optional attribute prefix(es) like [Obsolete].
    private static bool HasAccessModifier(string trimmedLine)
    {
        int idx = 0;
        while (idx < trimmedLine.Length && trimmedLine[idx] == '[')
        {
            int depth = 0;
            while (idx < trimmedLine.Length)
            {
                char c = trimmedLine[idx++];
                if (c == '[') depth++;
                else if (c == ']') { if (--depth == 0) break; }
            }
            while (idx < trimmedLine.Length && trimmedLine[idx] == ' ') idx++;
        }
        var rest = trimmedLine[idx..];
        foreach (var m in AccessModifiers)
            if (rest.StartsWith(m, StringComparison.Ordinal)) return true;
        return false;
    }

    private void CheckCryptographicBoundaryChanges(AnalysisContext context, List<Finding> findings)
    {
        var diff = context.Diff;

        foreach (var file in diff.Files)
        {
            if (WellKnownPatterns.IsTestFile(file.NewPath) || WellKnownPatterns.IsGeneratedFile(file.NewPath)) continue;

            // Extract all cryptographic method calls from removed and added lines
            var removedCalls = ExtractCryptoMethodCalls(file.RemovedLines.ToList());
            var addedCalls = ExtractCryptoMethodCalls(file.AddedLines.ToList());

            // For each cryptographic method name, check if arguments differ between removed and added
            foreach (var methodName in CryptographicMethods)
            {
                if (!removedCalls.TryGetValue(methodName, out var removedArgs)) continue;
                if (!addedCalls.TryGetValue(methodName, out var addedArgs)) continue;

                // Same method called with different arguments = behavioral/security boundary change
                if (removedArgs != addedArgs)
                {
                    findings.Add(CreateFinding(
                        file,
                        summary: $"Cryptographic method '{methodName}' called with different arguments: potential security boundary change.",
                        evidence: $"Was: {removedArgs} | Now: {addedArgs}",
                        whyItMatters: "Changes to cryptographic method arguments can alter trust boundaries, validation scope, or data protection. For example, changing ComputeHash(ciphertext) to ComputeHash(ciphertext.Skip(16).ToArray()) leaves parts of the payload unvalidated, breaking authentication guarantees.",
                        suggestedAction: "Verify the argument change is intentional and review its security implications. Document why the boundary change is safe.",
                        confidence: Confidence.High));
                }
            }
        }
    }

    // Extracts cryptographic method calls from a list of lines.
    // Returns a dictionary: methodName -> argumentString for calls found.
    private static Dictionary<string, string> ExtractCryptoMethodCalls(List<DiffLine> lines)
    {
        var result = new Dictionary<string, string>();

        foreach (var line in lines)
        {
            var content = line.Content;

            foreach (var method in CryptographicMethods)
            {
                var pattern = $"{method}(";
                var idx = content.IndexOf(pattern, StringComparison.Ordinal);
                if (idx < 0) continue;

                var argsStart = idx + pattern.Length;
                var closeParen = FindMatchingCloseParen(content, argsStart - 1);
                if (closeParen < 0) continue;

                var args = content[argsStart..closeParen].Trim();
                // Store the first occurrence of each method (or could store all and compare)
                if (!result.ContainsKey(method))
                {
                    result[method] = args;
                }
            }
        }

        return result;
    }

    // Finds the matching closing parenthesis, accounting for nested parens and string literals.
    private static int FindMatchingCloseParen(string content, int openParenIdx)
    {
        if (openParenIdx < 0 || openParenIdx >= content.Length) return -1;

        int depth = 0;
        bool inString = false;
        char delim = '"';

        for (int i = openParenIdx; i < content.Length; i++)
        {
            char c = content[i];

            if (inString)
            {
                if (c == '\\') { i++; continue; }
                if (c == delim) inString = false;
                continue;
            }

            if (c is '"' or '\'') { inString = true; delim = c; continue; }
            if (c == '(') depth++;
            else if (c == ')') { depth--; if (depth == 0) return i; }
        }

        return -1;
    }

    // Guard: Adjust confidence based on file context
    // Reduces confidence for test files or patterns that suggest non-critical changes
    private static Confidence AdjustConfidenceForContext(Confidence baseConfidence, DiffFile file)
    {
        // Check if the file contains test-related patterns in the path or content
        var filePath = file.NewPath ?? file.OldPath;
        
        // Patterns from corpus analysis showing high FP rates in test code
        var testIndicators = new[] { 
            "/test/", ".test/", "/tests/", ".tests/", 
            ".Tests/", "/Tests/", "/UnitTests/", 
            "Mock", "Fake", "Builder", "Factory" 
        };

        bool isTestRelated = testIndicators.Any(indicator => 
            filePath.Contains(indicator, StringComparison.OrdinalIgnoreCase));

        // Patterns indicating refactored test helpers (low behavioral risk)
        var testHelperPatterns = new[] { "Helper", "Extension", "Utility", "TestDouble" };
        bool isTestHelper = testHelperPatterns.Any(pattern =>
            filePath.Contains(pattern, StringComparison.OrdinalIgnoreCase));

        // If this appears to be test code with helper/extension patterns, reduce confidence
        if (isTestRelated && isTestHelper && baseConfidence == Confidence.Medium)
            return Confidence.Low;

        return baseConfidence;
    }
}

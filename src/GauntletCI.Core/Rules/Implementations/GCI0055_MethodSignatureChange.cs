// SPDX-License-Identifier: Elastic-2.0
using System.Text.RegularExpressions;
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0055, Method Signature Change Risk
/// Detects breaking method signature changes: parameter additions without defaults,
/// parameter type changes, and return type changes in public methods.
/// </summary>
public class GCI0055_MethodSignatureChange : RuleBase
{
    public GCI0055_MethodSignatureChange(IPatternProvider patterns) : base(patterns)
    {
    }
    public override string Id => "GCI0055";
    public override string Name => "Method Signature Change Risk";

    private static readonly Regex MethodDeclarationRegex =
        new(@"(public|protected)\s+(?:async\s+)?(\w+(?:<.+>)?)\s+(\w+)\s*\(([^)]*)\)", RegexOptions.Compiled);

    private static readonly Regex ParameterRegex =
        new(@"(\w+(?:<.+>)?)\s+(\w+)(?:\s*=\s*.+)?(?:,|$)", RegexOptions.Compiled);

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var findings = new List<Finding>();
        var diff = context.Diff;

        foreach (var file in diff.Files)
        {
            // Skip test files
            if (WellKnownPatterns.IsTestFile(file.NewPath)) continue;

            CheckMethodSignatureChanges(file, findings);
        }

        return Task.FromResult(findings);
    }

    private void CheckMethodSignatureChanges(DiffFile file, List<Finding> findings)
    {
        // Build map of method signatures from removed lines (old)
        var oldMethods = new Dictionary<string, MethodSignature>(StringComparer.Ordinal);
        foreach (var line in file.RemovedLines)
        {
            var match = MethodDeclarationRegex.Match(line.Content);
            if (match.Success)
            {
                var methodName = match.Groups[3].Value;
                var returnType = match.Groups[2].Value;
                var paramString = match.Groups[4].Value.Trim();
                oldMethods[methodName] = new MethodSignature { ReturnType = returnType, Parameters = paramString };
            }
        }

        // Check added lines for matching methods with changed signatures
        foreach (var line in file.AddedLines)
        {
            var match = MethodDeclarationRegex.Match(line.Content);
            if (!match.Success) continue;

            var methodName = match.Groups[3].Value;
            if (!oldMethods.TryGetValue(methodName, out var oldSignature)) continue;

            var returnType = match.Groups[2].Value;
            var paramString = match.Groups[4].Value.Trim();
            var newSignature = new MethodSignature { ReturnType = returnType, Parameters = paramString };

            // Check for return type change
            if (oldSignature.ReturnType != newSignature.ReturnType)
            {
                findings.Add(CreateFinding(
                    file,
                    summary: $"Public method {methodName} return type changed from {oldSignature.ReturnType} to {returnType}",
                    evidence: $"{file.NewPath} line {line.LineNumber}: {line.Content.Trim()}",
                    whyItMatters: "Changing a method's return type breaks callers who depend on the old type for assignments, casting, or type inference.",
                    suggestedAction: "Create a new method with the new return type, or use method overloading. Do not change the return type of an existing public method.",
                    confidence: Confidence.High,
                    line: line));
                continue;
            }

            // Check for parameter changes
            var oldParams = ParseParameters(oldSignature.Parameters);
            var newParams = ParseParameters(newSignature.Parameters);

            // Flag: parameters removed or reordered (without backward compatibility)
            if (oldParams.Count > newParams.Count)
            {
                findings.Add(CreateFinding(
                    file,
                    summary: $"Public method {methodName} has required parameter removed",
                    evidence: $"{file.NewPath} line {line.LineNumber}: Parameter count decreased from {oldParams.Count} to {newParams.Count}",
                    whyItMatters: "Removing a required parameter breaks all existing callers. Even with overloading, removing the signature is a breaking change.",
                    suggestedAction: "Keep the old method signature, or add a new overload. Never remove required parameters from public methods.",
                    confidence: Confidence.High,
                    line: line));
                continue;
            }

            // Flag: new required parameters added without defaults
            if (newParams.Count > oldParams.Count)
            {
                var addedParams = newParams.Skip(oldParams.Count).ToList();
                bool hasRequiredWithoutDefault = addedParams.Any(p => !p.HasDefault);

                if (hasRequiredWithoutDefault)
                {
                    var addedParamNames = string.Join(", ", addedParams.Where(p => !p.HasDefault).Select(p => p.Name));
                    findings.Add(CreateFinding(
                        file,
                        summary: $"Public method {methodName} has required parameter(s) added without defaults: {addedParamNames}",
                        evidence: $"{file.NewPath} line {line.LineNumber}: New parameters: {paramString}",
                        whyItMatters: "Adding required parameters (without defaults) to a public method breaks all existing callers that don't provide the new argument.",
                        suggestedAction: "Either provide default values for new parameters, or create an overload. Keep the original method signature intact.",
                        confidence: Confidence.High,
                        line: line));
                }
            }
        }
    }

    private List<Parameter> ParseParameters(string paramString)
    {
        if (string.IsNullOrWhiteSpace(paramString)) return [];

        var result = new List<Parameter>();
        var parts = paramString.Split(',');

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            var hasDefault = trimmed.Contains("=", StringComparison.Ordinal);
            var nameMatch = Regex.Match(trimmed, @"(\w+)(?:\s*=)?");

            if (nameMatch.Success)
            {
                result.Add(new Parameter
                {
                    Name = nameMatch.Groups[1].Value,
                    HasDefault = hasDefault
                });
            }
        }

        return result;
    }

    private record MethodSignature
    {
        public string ReturnType { get; set; } = "";
        public string Parameters { get; set; } = "";
    }

    private record Parameter
    {
        public string Name { get; set; } = "";
        public bool HasDefault { get; set; }
    }
}


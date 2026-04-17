// SPDX-License-Identifier: Elastic-2.0
using System.Text.RegularExpressions;
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0045 – Complexity Control
/// Detects over-engineering: single-use interfaces, abstract classes without abstract members,
/// and passive delegation wrappers.
/// </summary>
public class GCI0045_ComplexityControl : RuleBase
{
    public override string Id => "GCI0045";
    public override string Name => "Complexity Control";

    private static readonly Regex InterfaceDefRegex =
        new(@"\binterface\s+(I\w+)\b", RegexOptions.Compiled);

    private static readonly Regex AbstractClassRegex =
        new(@"\babstract\s+class\b", RegexOptions.Compiled);

    private static readonly Regex AbstractMemberRegex =
        new(@"\babstract\s+(?:(?:public|protected|internal|private)\s+)?(?!class\b)\w", RegexOptions.Compiled);

    private static readonly Regex DelegationCallRegex =
        new(@"return\s+_\w+\.\w+\(", RegexOptions.Compiled);

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var findings = new List<Finding>();

        CheckSingleUseInterface(context.Diff, findings);
        CheckAbstractClassWithNoAbstractMembers(context.Diff, findings);
        CheckPassiveDelegationWrapper(context.Diff, findings);

        return Task.FromResult(findings);
    }

    private void CheckSingleUseInterface(DiffContext diff, List<Finding> findings)
    {
        // Collect all interface names added across all files
        var interfaceDefinitions = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var file in diff.Files)
        {
            foreach (var line in file.AddedLines)
            {
                var match = InterfaceDefRegex.Match(line.Content);
                if (match.Success)
                    interfaceDefinitions[match.Groups[1].Value] = file.NewPath;
            }
        }

        foreach (var (interfaceName, sourcePath) in interfaceDefinitions)
        {
            // Count files that add a class implementing this interface
            int implCount = 0;
            string? implFile = null;

            foreach (var file in diff.Files)
            {
                bool hasImpl = file.AddedLines.Any(l =>
                    l.Content.Contains(interfaceName, StringComparison.Ordinal) &&
                    !InterfaceDefRegex.IsMatch(l.Content));

                if (hasImpl)
                {
                    implCount++;
                    implFile ??= file.NewPath;
                }
            }

            if (implCount != 1) continue;

            findings.Add(CreateFinding(
                summary: $"Interface {interfaceName} has exactly one implementing class in this diff",
                evidence: $"Interface defined in {Path.GetFileName(sourcePath)}; single implementor in {Path.GetFileName(implFile ?? sourcePath)}",
                whyItMatters: "An interface with a single implementation adds indirection without enabling polymorphism or testability. It is often premature abstraction.",
                suggestedAction: "Consider using a concrete class directly. Add the interface only when a second implementation or mocking boundary is needed.",
                confidence: Confidence.Low));
        }
    }

    private void CheckAbstractClassWithNoAbstractMembers(DiffContext diff, List<Finding> findings)
    {
        foreach (var file in diff.Files)
        {
            var addedLines = file.AddedLines.ToList();

            bool hasAbstractClass = addedLines.Any(l => AbstractClassRegex.IsMatch(l.Content));
            if (!hasAbstractClass) continue;

            bool hasAbstractMember = addedLines.Any(l =>
                AbstractMemberRegex.IsMatch(l.Content) &&
                !AbstractClassRegex.IsMatch(l.Content));

            if (hasAbstractMember) continue;

            findings.Add(CreateFinding(
                file,
                summary: $"Abstract class in {Path.GetFileName(file.NewPath)} has no abstract members in this diff",
                evidence: "abstract class added without any abstract method or property declarations",
                whyItMatters: "An abstract class with no abstract members is functionally equivalent to a regular base class. The abstract keyword implies a contract that isn't present.",
                suggestedAction: "Add at least one abstract member to enforce the contract, or change the class to non-abstract if extension without override is the intent.",
                confidence: Confidence.Low));
        }
    }

    private void CheckPassiveDelegationWrapper(DiffContext diff, List<Finding> findings)
    {
        foreach (var file in diff.Files)
        {
            var delegatingMethods = file.AddedLines
                .Where(l => DelegationCallRegex.IsMatch(l.Content))
                .ToList();

            if (delegatingMethods.Count < 3) continue;

            var evidence = delegatingMethods.Take(3)
                .Select(l => $"Line {l.LineNumber}: {l.Content.Trim()}");

            findings.Add(CreateFinding(
                file,
                summary: $"{Path.GetFileName(file.NewPath)} may be a passive delegation wrapper ({delegatingMethods.Count} forwarding methods)",
                evidence: string.Join("; ", evidence),
                whyItMatters: "A class that only forwards calls to another object adds complexity without behavior. This is often unnecessary indirection.",
                suggestedAction: "Expose the inner object directly, or use composition with actual value-adding behavior. Remove the wrapper if it only delegates.",
                confidence: Confidence.Low));
        }
    }

}

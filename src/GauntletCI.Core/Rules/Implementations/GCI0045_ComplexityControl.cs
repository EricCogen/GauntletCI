// SPDX-License-Identifier: Elastic-2.0
using System.Text.RegularExpressions;
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0045, Complexity Control
/// Detects over-engineering introduced in the diff:
/// - newly introduced single-use interfaces (exactly one visible implementor),
/// - abstract classes without abstract members,
/// - passive delegation wrappers.
/// </summary>
public class GCI0045_ComplexityControl : RuleBase
{
    public GCI0045_ComplexityControl(IPatternProvider patterns) : base(patterns)
    {
    }
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
        // Collect all interface names added across non-test files
        var interfaceDefinitions = new Dictionary<string, (string path, DiffLine line)>(StringComparer.Ordinal);

        foreach (var file in diff.Files)
        {
            if (WellKnownPatterns.IsTestFile(file.NewPath))
            {
                continue;
            }

            foreach (var line in file.AddedLines)
            {
                var match = InterfaceDefRegex.Match(line.Content);
                if (match.Success)
                {
                    interfaceDefinitions[match.Groups[1].Value] = (file.NewPath, line);
                }
            }
        }

        foreach (var (interfaceName, (sourcePath, _)) in interfaceDefinitions)
        {
            int implCount = 0;
            int referenceCount = 0;
            string? implFile = null;

            foreach (var file in diff.Files)
            {
                bool hasExplicitImpl = file.AddedLines.Any(l =>
                    (l.Content.Contains($": {interfaceName}", StringComparison.Ordinal) ||
                     l.Content.Contains($": {interfaceName},", StringComparison.Ordinal) ||
                     l.Content.Contains($", {interfaceName}", StringComparison.Ordinal)) &&
                    !InterfaceDefRegex.IsMatch(l.Content));

                if (hasExplicitImpl)
                {
                    implCount++;
                    implFile ??= file.NewPath;
                }

                bool hasReference = file.AddedLines.Any(l =>
                    l.Content.Contains(interfaceName, StringComparison.Ordinal) &&
                    !InterfaceDefRegex.IsMatch(l.Content) &&
                    !hasExplicitImpl);

                if (hasReference)
                {
                    referenceCount++;
                }
            }

            // Narrow scope to newly introduced single-use interfaces only.
            // Skip no-implementor cases (often boundary contracts expanded in later diffs)
            // and skip broader usage with multiple references.
            if (implCount != 1)
            {
                continue;
            }

            if (referenceCount > 1)
            {
                continue;
            }

            findings.Add(CreateFinding(
                summary: $"Interface {interfaceName} has exactly one implementing class in this diff",
                evidence: $"Interface defined in {Path.GetFileName(sourcePath)}; single implementor in {Path.GetFileName(implFile!)}",
                whyItMatters: "A newly introduced interface with one implementation often adds indirection without clear polymorphic value.",
                suggestedAction: "Use the concrete type for now; extract or keep the interface when a second implementation or stronger boundary need is demonstrated.",
                confidence: Confidence.Low));
        }
    }

    private void CheckAbstractClassWithNoAbstractMembers(DiffContext diff, List<Finding> findings)
    {
        foreach (var file in diff.Files)
        {
            var addedLines = file.AddedLines.ToList();

            bool hasAbstractClass = addedLines.Any(l => AbstractClassRegex.IsMatch(l.Content));
            if (!hasAbstractClass)
            {
                continue;
            }

            // Skip when the abstract class declaration includes a base type or interface (`: SomeBase`).
            // In those cases the contract is defined by the ancestor, not by abstract members here.
            bool classHasBaseType = addedLines.Any(l =>
                AbstractClassRegex.IsMatch(l.Content) &&
                l.Content.Contains(':'));

            if (classHasBaseType)
            {
                continue;
            }

            // Check all visible hunk lines (not just added): abstract members may be in context.
            var allVisible = file.Hunks.SelectMany(h => h.Lines)
                .Where(l => l.Kind != DiffLineKind.Removed)
                .ToList();

            bool hasAbstractMember = allVisible.Any(l =>
                AbstractMemberRegex.IsMatch(l.Content) &&
                !AbstractClassRegex.IsMatch(l.Content));

            if (hasAbstractMember)
            {
                continue;
            }

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
            if (WellKnownPatterns.IsTestFile(file.NewPath))
            {
                continue;
            }

            var addedLines = file.AddedLines.ToList();

            // Detect delegation pattern - methods that forward calls to internal field
            var delegatingMethods = addedLines
                .Where(l => DelegationCallRegex.IsMatch(l.Content))
                .ToList();

            if (delegatingMethods.Count < 2)
            {
                continue;
            }

            // Check if this file stores a dependency (field or property assignment)
            bool hasStoredDependency = addedLines.Any(l =>
                (l.Content.Contains("private readonly", StringComparison.Ordinal) ||
                 l.Content.Contains("private IOrder", StringComparison.Ordinal) ||
                 l.Content.Contains("private I", StringComparison.Ordinal)) &&
                l.Content.Contains("_", StringComparison.Ordinal));

            // Flag if we have delegation methods
            // Either: explicit stored dependency + 2+ delegating methods
            // Or: 3+ delegating methods (strong signal of wrapper)
            if ((hasStoredDependency && delegatingMethods.Count >= 2) || delegatingMethods.Count >= 3)
            {
                var evidence = delegatingMethods.Take(3)
                    .Select(l => $"Line {l.LineNumber}: {l.Content.Trim()}");

                findings.Add(CreateFinding(
                    file,
                    summary: $"{Path.GetFileName(file.NewPath)} appears to be a passive delegation wrapper ({delegatingMethods.Count} forwarding methods)",
                    evidence: string.Join("; ", evidence),
                    whyItMatters: "A class that only forwards calls to another object adds complexity without behavior. This is often unnecessary indirection.",
                    suggestedAction: "Expose the inner object directly, or use composition with actual value-adding behavior. Remove the wrapper if it only delegates.",
                    confidence: Confidence.Low));
            }
        }
    }

}


// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.StaticAnalysis;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI0006, Edge Case Handling
/// Detects potential null dereferences and missing validation in added code.
/// Boundary with GCI0043 (Nullability and Type Safety): GCI0043 detects as-casts without null checks
/// but suppresses when the same line also has a .Value access, deferring to GCI0006 as the
/// authoritative reporter for that combined pattern.
/// </summary>
public class GCI0006_EdgeCaseHandling : RuleBase
{
    public GCI0006_EdgeCaseHandling(IPatternProvider patterns) : base(patterns)
    {
    }
    public override string Id => "GCI0006";
    public override string Name => "Edge Case Handling";

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var diff = context.Diff;
        var findings = new List<Finding>();

        CheckNullDereferences(context, findings);
        CheckMissingParameterValidation(diff, findings);
        AddRoslynFindings(context.StaticAnalysis, findings);

        return Task.FromResult(findings);
    }

    private void CheckNullDereferences(AnalysisContext context, List<Finding> findings)
    {
        var diff = context.Diff;
        foreach (var file in diff.Files)
        {
            if (WellKnownPatterns.IsTestFile(file.NewPath) || WellKnownPatterns.IsGeneratedFile(file.NewPath)) continue;

            var addedLines = file.AddedLines.ToList();
            var fileContent = string.Join("\n", addedLines.Select(l => l.Content));
            var isNrtEnabled = WellKnownPatterns.IsNullableReferenceTypeEnabled(fileContent);

            for (int i = 0; i < addedLines.Count; i++)
            {
                var line = addedLines[i];
                var content = line.Content;
                if (!TryGetUnsafeValueAccessIndex(content, out int valueIndex)) continue;

                // Skip comment lines: .Value in a comment is not executable code
                if (WellKnownPatterns.GuardPatterns.IsCommentLine(content)) continue;

                if (!RegexEvidencePromotion.PassesCodeCandidateValidation(
                        context,
                        file.NewPath,
                        line,
                        valueIndex,
                        confirmSemantic: syntax =>
                            syntax.IsConfirmedMemberAccess(file.NewPath, line.LineNumber, "Value", valueIndex),
                        allowWhenNoSyntaxTree: c => !WellKnownPatterns.GuardPatterns.IsCommentLine(c)))
                    continue;

                // Skip expression-bodied property/method declarations: the .Value access IS
                // the declaration body (e.g. public override object? Value => _inner.Value;)
                if (WellKnownPatterns.GuardPatterns.IsExpressionBodied(content)) continue;

                // Skip KeyValuePair / Dictionary iteration: .Key and .Value together
                // means this is safe dict-entry access, not a Nullable<T>.Value dereference
                if (WellKnownPatterns.GuardPatterns.IsKeyValuePairAccess(content)) continue;

                // Skip when .Value is part of a LINQ projection (.Select(x => x.Value), etc.)
                // LINQ projections are intentionally mapping nullable to non-nullable
                if (WellKnownPatterns.GuardPatterns.IsLinqValueMapping(content)) continue;

                // Skip when .Value itself is null-checked inline or when HasValue guards it
                if (WellKnownPatterns.GuardPatterns.HasValueNullCheck(content) || WellKnownPatterns.GuardPatterns.HasHasValueGuard(content)) continue;

                // Skip IOptions<T>.Value / IOptionsSnapshot<T>.Value / IOptionsMonitor<T>.Value
                // These are DI-injected configuration wrappers, not Nullable<T>
                if (WellKnownPatterns.GuardPatterns.IsIOptionsValue(content)) continue;

                // NRT-aware: Skip Nullable<T>.Value when T is a non-nullable reference type in NRT context
                // In NRT-enabled files, Nullable<string> where string is non-nullable is safe (always has value)
                if (isNrtEnabled && WellKnownPatterns.IsNullableOfNonNullableType(content))
                    continue;

                // Check preceding lines for null guard
                int start = Math.Max(0, i - 5);
                bool hasGuard = addedLines[start..i]
                    .Any(l => l.Content.Contains("null", StringComparison.Ordinal) ||
                               l.Content.Contains("HasValue", StringComparison.Ordinal) ||
                               l.Content.Contains("is not null", StringComparison.Ordinal) ||
                               l.Content.Contains("!= null", StringComparison.Ordinal) ||
                               IsSuccessGuardFor(content, l.Content));

                if (!hasGuard)
                {
                    findings.Add(CreateFinding(
                        file,
                        summary: $"Potential null dereference via .Value access in {file.NewPath}",
                        evidence: $"Line {addedLines[i].LineNumber}: {content.Trim()}",
                        whyItMatters: "Accessing .Value on a nullable without a null check will throw InvalidOperationException at runtime.",
                        suggestedAction: "Add a null check or use ?.Value with null-coalescing before accessing .Value.",
                        confidence: Confidence.Medium,
                        line: addedLines[i]));
                    break; // one finding per file
                }
            }
        }
    }

    private void CheckMissingParameterValidation(DiffContext diff, List<Finding> findings)
    {
        foreach (var file in diff.Files)
        {
            // Test helpers do not need null guards: skip test files entirely
            if (WellKnownPatterns.IsTestFile(file.NewPath)) continue;

            var addedLines = file.AddedLines
                .Where(l => !WellKnownPatterns.HasDevOnlyMarker(l.Content)) // Skip dev-only code
                .ToList();

            var fileContent = string.Join("\n", addedLines.Select(l => l.Content));
            var isNrtEnabled = WellKnownPatterns.IsNullableReferenceTypeEnabled(fileContent);

            for (int i = 0; i < addedLines.Count; i++)
            {
                var content = addedLines[i].Content;
                // Only flag public or protected methods: private/internal callers are controlled
                if (!IsPublicOrProtectedSignature(content)) continue;

                // Override and sealed methods cannot change the parameter contract declared by the base
                // class or interface: enforcing null validation here is incorrect
                if (WellKnownPatterns.GuardPatterns.IsOverrideOrSealedMethod(content)) continue;

                // Abstract methods, delegate declarations, and partial stubs have no body
                if (WellKnownPatterns.GuardPatterns.IsAbstractOrDelegateOrPartial(content)) continue;

                // Constructors have no return type: skip them
                // A method signature has: <accessModifier> <returnType> <name>(<params>)
                // A constructor has:       <accessModifier> <name>(<params>)
                // Detect constructors by checking for a return-type token before the name
                if (!HasReturnType(content)) continue;

                // Check "string" or "object" in the parameter section, not just the return type
                var parenIdx = content.IndexOf('(');
                var closeIdx = parenIdx >= 0 ? content.IndexOf(')', parenIdx) : -1;
                var paramSection = (parenIdx >= 0 && closeIdx > parenIdx) ? content[parenIdx..(closeIdx + 1)] : "";
                if (!WellKnownPatterns.HasNullableReferenceParam(paramSection)) continue;

                // Guard: Skip if NRT (Nullable Reference Type) is enabled for non-nullable params
                // In NRT-enabled files, `string` param is explicitly non-nullable, so no validation needed
                if (isNrtEnabled && WellKnownPatterns.HasNonNullableParams(paramSection)) continue;

                // Guard: Skip if file is auto-generated (reduces FP in generated code)
                // Auto-generated files (.g.cs, .Designer.cs, migrations, API clients) have predictable patterns
                if (IsAutoGeneratedCodeFile(file.NewPath)) continue;

                // Guard: Skip if method parameters have default values or null-coalescing
                // Parameters with defaults (e.g., string param = "default") or nullable annotation
                // already provide fallback values, so validation is not needed
                if (HasParameterDefaults(paramSection)) continue;

                // Check next 5 lines for null/range validation
                int end = Math.Min(addedLines.Count, i + 6);
                bool hasValidation = addedLines[(i + 1)..end]
                    .Any(l => HasNullValidationPattern(l.Content));

                if (!hasValidation)
                {
                    findings.Add(CreateFinding(
                        file,
                        summary: $"New method parameter(s) added without apparent null/range validation in {file.NewPath}",
                        evidence: $"Line {addedLines[i].LineNumber}: {content.Trim()}",
                        whyItMatters: "Unvalidated parameters can lead to NullReferenceException or incorrect behaviour deeper in the call stack.",
                        suggestedAction: "Add ArgumentNullException.ThrowIfNull() or similar guard at the top of the method.",
                        confidence: Confidence.Medium,
                        line: addedLines[i]));
                    break;
                }
            }
        }
    }

    // Returns true when the line has an explicit return type: i.e., it is a method, not a constructor.
    // Constructors look like: public ClassName(: one identifier between access modifiers and '('
    // Methods look like:      public ReturnType MethodName(: two identifiers before '('
    private static bool HasReturnType(string line)
    {
        var t = line.TrimStart();
        // Strip known access/modifier keywords left-to-right
        foreach (var mod in new[] { "public ", "protected ", "static ", "async ", "virtual ",
                                    "sealed ", "new ", "internal ", "extern " })
        {
            while (t.StartsWith(mod, StringComparison.Ordinal))
                t = t[mod.Length..];
        }
        // After modifiers, a method has a return type token FOLLOWED BY a space and then the name.
        // A constructor has only the class name followed directly by '('.
        int firstParen = t.IndexOf('(');
        int firstSpace = t.IndexOf(' ');
        // A return type exists when there is a space before the first '(' (return-type<space>name<paren>)
        return firstSpace > 0 && (firstParen < 0 || firstSpace < firstParen);
    }

    // Returns true only when the .Success check in guardLine refers to the same root
    // identifier as the .Value access in valueLine (e.g. "match.Success" guards "match.Groups[1].Value").
    private static bool IsSuccessGuardFor(string valueLine, string guardLine)
    {
        int valIdx = valueLine.IndexOf(".Value", StringComparison.Ordinal);
        if (valIdx <= 0) return false;

        // Walk backward from the dot to collect the expression chain (e.g. "match.Groups[1]")
        int start = valIdx - 1;
        while (start > 0 && valueLine[start - 1] is char pc &&
               (char.IsLetterOrDigit(pc) || pc is '_' or '.' or '[' or ']'))
            start--;

        var expr = valueLine[start..valIdx]; // e.g. "match.Groups[1]"

        // Extract the root identifier: the first segment before '.' or '['
        int boundary = expr.IndexOfAny(['.', '[']);
        var root = boundary > 0 ? expr[..boundary] : expr;
        root = new string(root.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());

        return root.Length > 0 && guardLine.Contains(root + ".Success", StringComparison.Ordinal);
    }

    // Returns true when the line contains a .Value access that is NOT already made safe by:
    //   .Value!   -- null-forgiving operator (developer asserted non-null)
    //   ?.Value   -- null-conditional access preceding .Value
    //   .Value?.  -- null-conditional after .Value (developer handles null result)
    //   .Values   -- a different property; word-boundary check excludes .Values, .ValueOrDefault, etc.
    private static bool TryGetUnsafeValueAccessIndex(string content, out int matchIndex)
    {
        matchIndex = -1;
        int pos = 0;
        while (pos < content.Length)
        {
            int idx = content.IndexOf(".Value", pos, StringComparison.Ordinal);
            if (idx < 0) return false;

            int afterIdx = idx + 6;

            // Word-boundary: exclude .Values, .ValueOrDefault, etc.
            if (afterIdx < content.Length &&
                (char.IsLetterOrDigit(content[afterIdx]) || content[afterIdx] == '_'))
            {
                pos = afterIdx;
                continue;
            }

            // .Value! -- developer has asserted non-null with the null-forgiving operator
            if (afterIdx < content.Length && content[afterIdx] == '!')
            {
                pos = afterIdx;
                continue;
            }

            // .Value? -- null-conditional after (e.g. reader.Value?.ToString())
            if (afterIdx < content.Length && content[afterIdx] == '?')
            {
                pos = afterIdx;
                continue;
            }

            // ?.Value -- null-conditional before (e.g. reader?.Value)
            if (idx > 0 && content[idx - 1] == '?')
            {
                pos = afterIdx;
                continue;
            }

            // .Value = (LHS assignment) -- writing to a Value property, not reading a Nullable<T>
            // Exclude .Value == (comparison) and .Value => (lambda expression body)
            int assignPos = afterIdx;
            while (assignPos < content.Length && content[assignPos] == ' ') assignPos++;
            if (assignPos < content.Length && content[assignPos] == '=' &&
                (assignPos + 1 >= content.Length ||
                 (content[assignPos + 1] != '=' && content[assignPos + 1] != '>')))
            {
                pos = afterIdx;
                continue;
            }

            matchIndex = idx + 1;
            return true;
        }

        return false;
    }

    private static bool IsPublicOrProtectedSignature(string line)
    {
        var t = line.Trim();
        return t.Contains('(') && t.Contains(')') &&
               (t.StartsWith("public ", StringComparison.Ordinal) ||
                t.StartsWith("protected ", StringComparison.Ordinal));
    }

    private static void AddRoslynFindings(AnalyzerResult? staticAnalysis, List<Finding> findings)
    {
        if (staticAnalysis is null) return;
        var ca1062 = staticAnalysis.Diagnostics.Where(d => d.Id == "CA1062");
        foreach (var diag in ca1062)
        {
            findings.Add(new Finding
            {
                RuleId = "GCI0006",
                RuleName = "Edge Case Handling",
                Summary = $"CA1062: {diag.Message}",
                Evidence = $"{diag.FilePath}:{diag.Line}",
                WhyItMatters = "Roslyn detected a parameter that is not validated before use.",
                SuggestedAction = "Validate all reference parameters before use.",
                Confidence = Confidence.Medium,
            });
        }
    }

    // Guard: Check if this file is auto-generated code (reduces FP for GCI0006 in generated files)
    private static bool IsAutoGeneratedCodeFile(string filePath)
    {
        // Standard code generator suffixes (.g.cs, .Designer.cs, etc.)
        if (filePath.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) ||
            filePath.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase) ||
            filePath.EndsWith(".pb.cs", StringComparison.OrdinalIgnoreCase) ||
            filePath.EndsWith(".grpc.cs", StringComparison.OrdinalIgnoreCase))
            return true;

        // Generated code directory patterns
        var normalized = filePath.Replace('\\', '/');
        if (normalized.Contains("/Generated/", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("/generated/", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase))
            return true;

        // Entity Framework migration patterns
        if (normalized.Contains("/Migrations/", StringComparison.OrdinalIgnoreCase) &&
            (filePath.EndsWith("Migration.cs", StringComparison.OrdinalIgnoreCase) ||
             filePath.EndsWith("Migrations.cs", StringComparison.OrdinalIgnoreCase)))
            return true;

        // Compiler/AssemblyInfo generated files
        if (filePath.EndsWith("AssemblyInfo.cs", StringComparison.OrdinalIgnoreCase) ||
            filePath.EndsWith(".assemblyinfo.cs", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    // Guard: Check if any parameters have default values (which implicitly handle null)
    // Parameters with defaults like 'string param = "default"' or 'int count = 0' don't need validation
    private static bool HasParameterDefaults(string paramSection)
    {
        return paramSection.Contains('=', StringComparison.Ordinal);
    }

    // Guard: Recognize common null-validation patterns
    private static bool HasNullValidationPattern(string content)
    {
        // Direct null checks
        if (content.Contains("null", StringComparison.Ordinal)) return true;
        if (content.Contains("ArgumentNull", StringComparison.Ordinal)) return true;
        if (content.Contains("ArgumentException", StringComparison.Ordinal)) return true;
        if (content.Contains("throw", StringComparison.Ordinal)) return true;

        // Common null-check patterns
        if (content.Contains("ThrowIfNull", StringComparison.Ordinal)) return true;
        if (content.Contains("is null", StringComparison.Ordinal)) return true;
        if (content.Contains("is not null", StringComparison.Ordinal)) return true;
        if (content.Contains("== null", StringComparison.Ordinal)) return true;
        if (content.Contains("!= null", StringComparison.Ordinal)) return true;
        if (content.Contains("?.", StringComparison.Ordinal)) return true; // null-conditional operator
        if (content.Contains("??", StringComparison.Ordinal)) return true;  // null-coalescing operator (?? or ??=)

        // Guard clauses and early returns
        if (content.Contains("guard", StringComparison.OrdinalIgnoreCase)) return true;
        if (content.Contains("return", StringComparison.Ordinal)) return true;

        return false;
    }
}


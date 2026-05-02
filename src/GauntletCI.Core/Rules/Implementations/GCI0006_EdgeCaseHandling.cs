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
    public override string Id => "GCI0006";
    public override string Name => "Edge Case Handling";

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var diff = context.Diff;
        var findings = new List<Finding>();

        CheckNullDereferences(diff, findings);
        CheckMissingParameterValidation(diff, findings);
        CheckLoopBoundaryChanges(diff, findings);
        AddRoslynFindings(context.StaticAnalysis, findings);

        return Task.FromResult(findings);
    }

    private void CheckNullDereferences(DiffContext diff, List<Finding> findings)
    {
        foreach (var file in diff.Files)
        {
            if (WellKnownPatterns.IsTestFile(file.NewPath) || WellKnownPatterns.IsGeneratedFile(file.NewPath)) continue;

            var addedLines = file.AddedLines.ToList();
            for (int i = 0; i < addedLines.Count; i++)
            {
                var content = addedLines[i].Content;
                if (!HasUnsafeValueAccess(content)) continue;

                // Skip comment lines: .Value in a comment is not executable code
                if (content.TrimStart().StartsWith("//")) continue;

                // Skip expression-bodied property/method declarations: the .Value access IS
                // the declaration body (e.g. public override object? Value => _inner.Value;)
                var trimmed = content.TrimStart();
                if ((trimmed.StartsWith("public ", StringComparison.Ordinal) ||
                     trimmed.StartsWith("protected ", StringComparison.Ordinal)) &&
                    content.Contains("=>", StringComparison.Ordinal)) continue;

                // Skip KeyValuePair / Dictionary iteration: .Key (not .Keys) and .Value on the same line
                // means this is safe dict-entry access, not a Nullable<T>.Value dereference
                if (HasDotKeyAccess(content)) continue;

                // Skip when .Value is part of a LINQ expression (.Select(x => x.Value), etc.)
                // LINQ projections are intentionally mapping nullable to non-nullable
                if (IsLinqValueProjection(content)) continue;

                // Skip when .Value itself is null-checked inline (e.g. `x.Value == null`,
                // `x.Value is not null`) or when HasValue guards the same access.
                // Avoid broad Contains("== null") which suppresses `_other != null` on the same line.
                if (System.Text.RegularExpressions.Regex.IsMatch(
                        content, @"\.Value\s*(is not null|is null|==\s*null|!=\s*null)") ||
                    content.Contains("HasValue", StringComparison.Ordinal)) continue;

                // Skip IOptions<T>.Value / IOptionsSnapshot<T>.Value / IOptionsMonitor<T>.Value
                // These are DI-injected configuration wrappers, not Nullable<T>
                if (content.Contains("IOptions", StringComparison.Ordinal)) continue;

                // NRT-aware: Skip Nullable<T>.Value when T is a non-nullable reference type in NRT context
                // In NRT-enabled files, Nullable<string> where string is non-nullable is safe (always has value)
                if (IsNullableReferenceTypeEnabled(file) && IsNullableOfNonNullableType(content))
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

            var addedLines = file.AddedLines.ToList();
            for (int i = 0; i < addedLines.Count; i++)
            {
                var content = addedLines[i].Content;
                // Only flag public or protected methods: private/internal callers are controlled
                if (!IsPublicOrProtectedSignature(content)) continue;

                // Override methods cannot change the parameter contract declared by the base
                // class or interface: enforcing null validation here is incorrect
                if (content.Contains(" override ", StringComparison.Ordinal)) continue;

                // Abstract methods, delegate declarations, and partial stubs have no body
                if (content.Contains(" abstract ", StringComparison.Ordinal) ||
                    content.Contains(" delegate ", StringComparison.Ordinal) ||
                    content.Contains(" partial ", StringComparison.Ordinal)) continue;

                // Constructors have no return type: skip them
                // A method signature has: <accessModifier> <returnType> <name>(<params>)
                // A constructor has:       <accessModifier> <name>(<params>)
                // Detect constructors by checking for a return-type token before the name
                if (!HasReturnType(content)) continue;

                // Check "string" or "object" in the parameter section, not just the return type
                var parenIdx = content.IndexOf('(');
                var closeIdx = parenIdx >= 0 ? content.IndexOf(')', parenIdx) : -1;
                var paramSection = (parenIdx >= 0 && closeIdx > parenIdx) ? content[parenIdx..(closeIdx + 1)] : "";
                if (!HasNullableReferenceParam(paramSection)) continue;

                // Guard: Skip if NRT (Nullable Reference Type) is enabled for non-nullable params
                // In NRT-enabled files, `string` param is explicitly non-nullable, so no validation needed
                if (IsNullableReferenceTypeEnabled(file) && HasNonNullableParams(paramSection)) continue;

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

    private static bool HasNullableReferenceParam(string paramSection)
    {
        // Walk character by character, tracking generic depth so we skip type arguments
        // like Dictionary<string?, int> and only match top-level parameters.
        int angleDepth = 0;
        for (int i = 0; i < paramSection.Length; i++)
        {
            char c = paramSection[i];
            if (c == '<') { angleDepth++; continue; }
            if (c == '>') { angleDepth = Math.Max(0, angleDepth - 1); continue; }
            if (angleDepth > 0) continue;

            foreach (var keyword in new[] { "string?", "object?" })
            {
                if (i + keyword.Length > paramSection.Length) continue;
                if (!paramSection.AsSpan(i).StartsWith(keyword, StringComparison.Ordinal)) continue;

                // Leading boundary: must be preceded by a non-identifier char
                bool leadOk = i == 0 || paramSection[i - 1] is ' ' or '(' or ',' or '<';
                if (!leadOk) continue;

                // Trailing boundary: must be followed by a non-identifier char
                int after = i + keyword.Length;
                bool trailOk = after >= paramSection.Length ||
                               paramSection[after] is ' ' or '[' or ',' or ')' or '<';
                if (trailOk) return true;
            }
        }
        return false;
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
    private static bool HasUnsafeValueAccess(string content)
    {
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

            return true;
        }
        return false;
    }

    // Returns true when the line contains a `.Key` word-boundary access (not `.Keys`, `.KeyValues`, etc.)
    // Used to detect KeyValuePair/Dictionary iterations where `.Value` is the entry value, not Nullable<T>.
    private static bool HasDotKeyAccess(string content)
    {
        int pos = 0;
        while (pos < content.Length)
        {
            int idx = content.IndexOf(".Key", pos, StringComparison.Ordinal);
            if (idx < 0) return false;
            int after = idx + 4;
            // Ensure what follows is NOT a word character (avoids matching .Keys, .KeyValues, etc.)
            if (after >= content.Length ||
                (!char.IsLetterOrDigit(content[after]) && content[after] != '_'))
                return true;
            pos = after;
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

    private void CheckLoopBoundaryChanges(DiffContext diff, List<Finding> findings)
    {
        foreach (var file in diff.Files)
        {
            if (WellKnownPatterns.IsTestFile(file.NewPath) || WellKnownPatterns.IsGeneratedFile(file.NewPath)) continue;

            foreach (var hunk in file.Hunks)
            {
                // Pattern 1: Detect new break statements added to loops
                DetectNewBreakInLoop(this, file, hunk, findings);

                // Pattern 2: Detect new max iteration checks added
                DetectNewIterationLimit(this, file, hunk, findings);

                // Pattern 3: Detect loop condition strengthening
                DetectLoopConditionStrengthening(this, file, hunk, findings);
            }
        }
    }

    private static void DetectNewBreakInLoop(GCI0006_EdgeCaseHandling rule, DiffFile file, DiffHunk hunk, List<Finding> findings)
    {
        var lines = hunk.Lines.ToList();
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.Kind != DiffLineKind.Added) continue;

            var content = line.Content.Trim();
            if (!content.StartsWith("break", StringComparison.Ordinal)) continue;

            // Check surrounding context for loop keywords
            int start = Math.Max(0, i - 10);
            int end = Math.Min(lines.Count, i + 5);

            bool inLoopContext = false;
            for (int j = start; j < end; j++)
            {
                var l = lines[j].Content;
                if (l.Contains("while (", StringComparison.Ordinal) ||
                    l.Contains("for (", StringComparison.Ordinal) ||
                    l.Contains("for(", StringComparison.Ordinal) ||
                    l.Contains("foreach (", StringComparison.Ordinal))
                {
                    inLoopContext = true;
                    break;
                }
            }

            if (inLoopContext)
            {
                findings.Add(rule.CreateFinding(
                    file,
                    summary: $"New break statement added to loop in {file.NewPath} (possible loop boundary fix)",
                    evidence: $"Added: {content}",
                    whyItMatters: "Adding loop termination conditions may indicate fixing unbounded loops that could cause denial of service.",
                    suggestedAction: "Verify that this loop boundary change is intentional and correctly implements termination conditions.",
                    confidence: Confidence.Medium,
                    line: line));
                break; // one finding per hunk
            }
        }
    }

    private static void DetectNewIterationLimit(GCI0006_EdgeCaseHandling rule, DiffFile file, DiffHunk hunk, List<Finding> findings)
    {
        var lines = hunk.Lines.ToList();
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.Kind != DiffLineKind.Added) continue;

            var content = line.Content.Trim();
            // Look for patterns like: i < MAX, count < 1000, iterations < limit
            if ((content.Contains("< ") || content.Contains("<= ")) &&
                (content.Contains("count", StringComparison.OrdinalIgnoreCase) ||
                 content.Contains("i ", StringComparison.OrdinalIgnoreCase) ||
                 content.Contains("iterations", StringComparison.OrdinalIgnoreCase) ||
                 content.Contains("MAX", StringComparison.Ordinal) ||
                 content.Contains("limit", StringComparison.OrdinalIgnoreCase)))
            {
                // Check for loop context
                int start = Math.Max(0, i - 10);
                int end = Math.Min(lines.Count, i + 5);

                bool inLoopContext = false;
                for (int j = start; j < end; j++)
                {
                    var l = lines[j].Content;
                    if (l.Contains("while (", StringComparison.Ordinal) ||
                        l.Contains("for (", StringComparison.Ordinal) ||
                        l.Contains("for(", StringComparison.Ordinal))
                    {
                        inLoopContext = true;
                        break;
                    }
                }

                if (inLoopContext)
                {
                    findings.Add(rule.CreateFinding(
                        file,
                        summary: $"New iteration limit added to loop in {file.NewPath} (possible loop boundary fix)",
                        evidence: $"Added: {content}",
                        whyItMatters: "Adding iteration limits prevents infinite loops that could cause denial of service through resource exhaustion.",
                        suggestedAction: "Verify the iteration limit is appropriate and correctly prevents unbounded execution.",
                        confidence: Confidence.Medium,
                        line: line));
                    break; // one finding per hunk
                }
            }
        }
    }

    private static void DetectLoopConditionStrengthening(GCI0006_EdgeCaseHandling rule, DiffFile file, DiffHunk hunk, List<Finding> findings)
    {
        var lines = hunk.Lines.ToList();

        // Look for removed and added while/for conditions to compare
        for (int i = 0; i < lines.Count; i++)
        {
            var removedLine = lines[i];
            if (removedLine.Kind != DiffLineKind.Removed) continue;

            var removedContent = removedLine.Content.Trim();
            if (!removedContent.StartsWith("while (", StringComparison.Ordinal) &&
                !removedContent.StartsWith("for (", StringComparison.Ordinal)) continue;

            // Look for a corresponding added line
            for (int j = i + 1; j < Math.Min(lines.Count, i + 10); j++)
            {
                var addedLine = lines[j];
                if (addedLine.Kind != DiffLineKind.Added) continue;

                var addedContent = addedLine.Content.Trim();
                if (!addedContent.StartsWith("while (", StringComparison.Ordinal) &&
                    !addedContent.StartsWith("for (", StringComparison.Ordinal)) continue;

                // Check if the new condition is more restrictive (added AND clause)
                if (addedContent.Contains(" && ") && !removedContent.Contains(" && "))
                {
                    findings.Add(rule.CreateFinding(
                        file,
                        summary: $"Loop condition strengthened in {file.NewPath} (possible loop boundary fix)",
                        evidence: $"Old: {removedContent.Substring(0, Math.Min(60, removedContent.Length))}...\nNew: {addedContent.Substring(0, Math.Min(60, addedContent.Length))}...",
                        whyItMatters: "Strengthening loop exit conditions may indicate fixing unbounded loops that could cause denial of service.",
                        suggestedAction: "Verify that the new loop condition correctly prevents unbounded iteration.",
                        confidence: Confidence.Low,
                        line: addedLine));
                    return; // one finding per file
                }
            }
        }
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

    // Guard: Check if NRT (Nullable Reference Type) is enabled for this file
    // NRT can be enabled via: #nullable enable directive, project-wide settings, or modern .NET versions
    private static bool IsNullableReferenceTypeEnabled(DiffFile file)
    {
        var fileContent = string.Join("\n", file.AddedLines.Select(l => l.Content));
        
        // Explicit NRT directive: #nullable enable or #nullable restore
        if (fileContent.Contains("#nullable enable", StringComparison.OrdinalIgnoreCase) ||
            fileContent.Contains("#nullable restore", StringComparison.OrdinalIgnoreCase))
            return true;

        // Explicit NRT disable: #nullable disable indicates NRT is not active
        if (fileContent.Contains("#nullable disable", StringComparison.OrdinalIgnoreCase))
            return false;

        // Heuristic: Modern .NET projects (net5+) typically have NRT enabled in csproj
        // Look for patterns that indicate modern C# (nullable annotations, record types, init accessors)
        // These are common in NRT-enabled projects
        if (fileContent.Contains(" record ", StringComparison.Ordinal) ||
            fileContent.Contains(" record ", StringComparison.Ordinal) ||
            fileContent.Contains("{ init; }", StringComparison.Ordinal) ||
            fileContent.Contains("{ get; init; }", StringComparison.Ordinal))
            return true;

        // Look for the pattern: non-nullable string used in method signatures
        // This is stronger evidence of NRT enablement than just presence of 'string'
        // Pattern: public/protected method with 'string' param not followed by '?'
        if (System.Text.RegularExpressions.Regex.IsMatch(
                fileContent, @"(public|protected)\s+\w+\s+\w+\s*\(\s*string\s+\w+"))
            return true;

        // Default: assume NRT disabled (conservative approach - will validate parameters)
        return false;
    }

    // Guard: Check if the parameter list contains explicitly non-nullable parameters
    // In NRT-enabled context, 'string' is non-nullable and doesn't need validation
    private static bool HasNonNullableParams(string paramSection)
    {
        // Look for 'string' not followed by '?' (indicating non-nullable in NRT context)
        int angleDepth = 0;
        for (int i = 0; i < paramSection.Length; i++)
        {
            char c = paramSection[i];
            if (c == '<') { angleDepth++; continue; }
            if (c == '>') { angleDepth = Math.Max(0, angleDepth - 1); continue; }
            if (angleDepth > 0) continue;

            // Match "string" not followed by "?"
            if (i + 6 < paramSection.Length && paramSection.AsSpan(i, 6).SequenceEqual("string"))
            {
                if (i + 6 < paramSection.Length && paramSection[i + 6] != '?')
                {
                    // Check boundary
                    bool leadOk = i == 0 || paramSection[i - 1] is ' ' or '(' or ',' or '<';
                    bool trailOk = i + 6 >= paramSection.Length || paramSection[i + 6] is ' ' or '[' or ',' or ')';
                    if (leadOk && trailOk) return true;
                }
            }
        }
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

    // Guard: Detect Nullable<T> where T is a non-nullable type in NRT context
    // In NRT-enabled code, Nullable<string> where 'string' is non-nullable means the value is guaranteed to exist
    // Pattern: var x = new Nullable<string>(...) - In NRT, string is non-nullable, so Nullable<string> always has value
    // This detects patterns like: Nullable<int>, Nullable<string>, etc.
    private static bool IsNullableOfNonNullableType(string content)
    {
        // Look for Nullable<T> or Nullable<...> patterns
        var match = System.Text.RegularExpressions.Regex.Match(content, @"Nullable<(\w+(?:<[^>]+>)?)>");
        if (!match.Success) return false;

        var typeParam = match.Groups[1].Value;
        
        // If T is a value type (int, bool, DateTime, etc.), Nullable<T> always has a value in NRT context
        // Common value types (not exhaustive, but covers most cases)
        var valueTypes = new[] 
        { 
            "int", "long", "short", "byte", "double", "float", "decimal", "bool", 
            "uint", "ulong", "ushort", "ubyte", "char",
            "DateTime", "TimeSpan", "DateOnly", "TimeOnly", "Guid",
            "DateTimeOffset", "DateTimeKind"
        };

        // Also check for custom structs (heuristic: if it's PascalCase and not a built-in type)
        bool isValueType = valueTypes.Contains(typeParam);
        bool isCustomStruct = char.IsUpper(typeParam[0]) && !valueTypes.Contains(typeParam);

        // In NRT context, both value types and reference types in Nullable<T> can be safely accessed
        // if they came from a non-nullable context
        return isValueType || isCustomStruct;
    }

    // Guard: Detect LINQ expressions where .Value is intentionally mapped (safe projection)
    // Pattern: .Select(x => x.Value), .Where(x => x.Value != null), etc.
    // In these cases, the developer is explicitly handling the mapping, so it's not an unsafe dereference
    private static bool IsLinqValueProjection(string content)
    {
        // Look for LINQ method patterns where .Value is used as a projection
        // Common patterns:
        //   .Select(... => ....Value)
        //   .SelectMany(... => ....Value)
        //   .Where(... => ....Value)
        //   .OrderBy(... => ....Value)
        //   .GroupBy(... => ....Value)
        //   .All(x => x.Value != null)
        //   .Any(x => x.Value != null)

        // Check for LINQ method calls followed by lambda with .Value access
        var linqMethods = new[] { "Select", "SelectMany", "Where", "OrderBy", "OrderByDescending", 
                                  "GroupBy", "All", "Any", "First", "FirstOrDefault", 
                                  "Last", "LastOrDefault", "Single", "SingleOrDefault" };

        foreach (var method in linqMethods)
        {
            // Pattern: .MethodName(... => ....Value...)
            var pattern = @"\." + method + @"\s*\([^)]*=>.*\.Value";
            if (System.Text.RegularExpressions.Regex.IsMatch(content, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                return true;
        }

        return false;
    }
}

// SPDX-License-Identifier: Elastic-2.0
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace GauntletCI.Core.StaticAnalysis;

/// <summary>
/// Analyses a single C# file using Roslyn and returns diagnostics on changed lines.
/// Not a full project compilation — intentionally lightweight for speed.
/// </summary>
public class RoslynAnalyzer
{
    private static readonly CSharpCompilationOptions DefaultOptions =
        new(OutputKind.DynamicallyLinkedLibrary, reportSuppressedDiagnostics: false);

    public async Task<AnalyzerResult> AnalyzeFileAsync(
        string filePath,
        string sourceCode,
        IEnumerable<int>? changedLineNumbers = null,
        CancellationToken ct = default)
    {
        try
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(
                SourceText.From(sourceCode), path: filePath, cancellationToken: ct);

            var compilation = CSharpCompilation.Create(
                assemblyName: "GauntletCI.Analysis",
                syntaxTrees: [syntaxTree],
                references: GetBasicReferences(),
                options: DefaultOptions);

            var diagnostics = compilation.GetDiagnostics(ct)
                .Where(d => d.Severity >= DiagnosticSeverity.Warning)
                .ToList();

            var changedLines = changedLineNumbers?.ToHashSet();
            var filtered = diagnostics
                .Where(d =>
                {
                    if (changedLines == null || changedLines.Count == 0) return true;
                    var lineSpan = d.Location.GetLineSpan();
                    return changedLines.Contains(lineSpan.StartLinePosition.Line + 1);
                })
                .Select(d => new AnalyzerDiagnostic
                {
                    Id = d.Id,
                    Message = d.GetMessage(),
                    FilePath = filePath,
                    Line = d.Location.GetLineSpan().StartLinePosition.Line + 1,
                    Column = d.Location.GetLineSpan().StartLinePosition.Character + 1,
                    Severity = d.Severity.ToString(),
                    Category = d.Descriptor.Category
                })
                .ToList();

            return new AnalyzerResult
            {
                AnalyzedFile = filePath,
                Diagnostics = filtered,
                Success = true
            };
        }
        catch (Exception ex)
        {
            return new AnalyzerResult
            {
                AnalyzedFile = filePath,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private static IEnumerable<MetadataReference> GetBasicReferences()
    {
        var runtimeDir = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
        var refs = new List<MetadataReference>();

        foreach (var dll in new[] { "System.Runtime.dll", "System.Private.CoreLib.dll", "netstandard.dll" })
        {
            var path = Path.Combine(runtimeDir, dll);
            if (File.Exists(path))
                refs.Add(MetadataReference.CreateFromFile(path));
        }

        return refs;
    }
}

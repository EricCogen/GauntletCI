import json
from pathlib import Path
from graphify.extract import collect_files, extract

# Chunk 5-8 files (core analysis + tests + docs + config)
chunk_files = [
    # Chunk 5 - Core analyzers and tests
    "src/GauntletCI.Core/Analyzers/IncrementalAnalyzer.cs",
    "src/GauntletCI.Core/Core/Abstractions/IGauntletAnalyzer.cs",
    "src/GauntletCI.Core/Core/Abstractions/IGauntletRule.cs",
    "src/GauntletCI.Core/Core/Abstractions/IGauntletDiagnostic.cs",
    "src/GauntletCI.Core/Core/Abstractions/IProblemLocation.cs",
    "src/GauntletCI.Core/Tests/UtilityTests/AstNormalizerTests.cs",
    "src/GauntletCI.Core/Tests/UtilityTests/GlyphConversionTests.cs",
    "src/GauntletCI.Core/Tests/UtilityTests/WellKnownPatternsTests.cs",
    "src/GauntletCI.Core/Tests/Analyzers/RoslynDiagnosticAnalyzerTests.cs",
    "src/GauntletCI.Core/Tests/Analyzers/SymbolAnalyzerTests.cs",
    "src/GauntletCI.Core/Tests/Analyzers/SymbolCachingAnalyzerTests.cs",
    "src/GauntletCI.Core/Tests/Analyzers/SyntaxAnalyzerTests.cs",
    "src/GauntletCI.Core/Tests/Analyzers/SemanticAnalyzerTests.cs",
    "src/GauntletCI.Core/Tests/Analyzers/IncrementalAnalyzerTests.cs",
    "src/GauntletCI.Core/Tests/Core/CodeAnalyzerBaseTests.cs",
    "src/GauntletCI.Core/Tests/Core/DiagnosticCoreTests.cs",
    "src/GauntletCI.Core/Tests/Core/GuardedPatternsTests.cs",
    "src/GauntletCI.Core/Tests/Core/RuleBaseTests.cs",
    "src/GauntletCI.Core/Tests/Core/RuleRegistryTests.cs",
    "src/GauntletCI.Core/Tests/Core/GauntletEngineTests.cs",
    "src/GauntletCI.Core/Tests/Core/ProgramTests.cs",
    "src/GauntletCI.Core/Utility/ProblemLocation.cs",
    
    # Chunk 6 - Diagnostic + docs
    "src/GauntletCI.Core/Core/Core/DiagnosticSummary.cs",
    "src/GauntletCI.Core/Tests/DiagnosticSummaryTests.cs",
    "docs/API.md",
    "docs/ARCHITECTURE.md",
    "docs/CASE_STUDIES.md",
    "docs/CONTRIBUTING.md",
    "docs/RULES.md",
    "docs/SECURITY.md",
    "docs/VERSION_HISTORY.md",
    "README.md",
    "CHARTER.md",
    "STORY.md",
    "COMPARE.md",
    "HISTORY.md",
    "SECURITY.md",
    "SUPPORT.md",
    "CONTRIBUTING.md",
    "LICENSE",
    "action.yml",
    "worker/docker-entrypoint.sh",
    "worker/Dockerfile",
    "worker/Dockerfile.Windows",
    "packaging/nuget/GauntletCI.nuspec",
    
    # Chunk 7 - Schemas & engineering docs
    "schemas/gauntletci-schema.json",
    ".misc/ENGINEERING_STANDARDS.md",
    ".misc/PHASE18_COMPLETION_SUMMARY.md",
    ".misc/PHASE18_ENGINEERING_INVARIANTS.md",
    ".misc/PHASE18_ARCHITECTURE.md",
    ".misc/best-practices.json",
    ".misc/MANIFEST.json",
    ".misc/consolidated_engineering_rules.md",
    
    # Chunk 8 - Data & config
    "data/baseline-corpus-results.json",
    "deploy.yml",
    "GauntletCI.slnx",
    "src/GauntletCI.Core/GauntletCI.Core.csproj",
]

# Filter to files that exist
existing_files = [f for f in chunk_files if Path(f).exists()]
print(f"Processing {len(existing_files)} existing files from chunks 5-8")

# Run AST extraction on code files
code_files = [f for f in existing_files if Path(f).suffix in {'.cs', '.py', '.json', '.yml', '.yaml'}]
if code_files:
    code_paths = [Path(f) for f in code_files]
    result = extract(code_paths)
    with open('graphify-out/chunks_ast.json', 'w') as f:
        json.dump(result, f, indent=2)
    print(f"AST extraction: {len(result['nodes'])} nodes, {len(result['edges'])} edges")

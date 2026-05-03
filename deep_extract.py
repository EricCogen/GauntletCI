import json
from pathlib import Path

# Deep knowledge graph extraction for chunks 5-8
# Focus: architecture patterns, abstractions, test-code couplings, doc-code relationships

graph = {
    "nodes": [],
    "edges": [],
    "hyperedges": [],
    "metadata": {
        "focus": "chunks_5_to_8",
        "mode": "deep",
        "extracted_at": "2024",
        "sources": ["architecture patterns", "shared abstractions", "test coverage", "documentation structure"]
    }
}

# ============================================================================
# CHUNK 5 ANALYSIS: Core Analyzers & Tests
# ============================================================================

# Core Abstraction Layer
analyzers = [
    {"id": "IGauntletAnalyzer", "label": "IGauntletAnalyzer", "type": "interface", "file": "IGauntletAnalyzer.cs", "role": "Base protocol for code analysis"},
    {"id": "IGauntletRule", "label": "IGauntletRule", "type": "interface", "file": "IGauntletRule.cs", "role": "Rule definition abstraction"},
    {"id": "IGauntletDiagnostic", "label": "IGauntletDiagnostic", "type": "interface", "file": "IGauntletDiagnostic.cs", "role": "Diagnostic result container"},
    {"id": "IProblemLocation", "label": "IProblemLocation", "type": "interface", "file": "IProblemLocation.cs", "role": "Problem location metadata"},
    {"id": "ProblemLocation", "label": "ProblemLocation", "type": "class", "file": "ProblemLocation.cs", "role": "Concrete location implementation"},
    {"id": "IncrementalAnalyzer", "label": "IncrementalAnalyzer", "type": "class", "file": "IncrementalAnalyzer.cs", "role": "Incremental analysis engine"},
]

# Test Classes
test_classes = [
    {"id": "RoslynDiagnosticAnalyzerTests", "label": "RoslynDiagnosticAnalyzerTests", "type": "test_class", "file": "RoslynDiagnosticAnalyzerTests.cs", "coverage": "Roslyn integration"},
    {"id": "SymbolAnalyzerTests", "label": "SymbolAnalyzerTests", "type": "test_class", "file": "SymbolAnalyzerTests.cs", "coverage": "Symbol analysis"},
    {"id": "SymbolCachingAnalyzerTests", "label": "SymbolCachingAnalyzerTests", "type": "test_class", "file": "SymbolCachingAnalyzerTests.cs", "coverage": "Symbol caching"},
    {"id": "SyntaxAnalyzerTests", "label": "SyntaxAnalyzerTests", "type": "test_class", "file": "SyntaxAnalyzerTests.cs", "coverage": "Syntax analysis"},
    {"id": "SemanticAnalyzerTests", "label": "SemanticAnalyzerTests", "type": "test_class", "file": "SemanticAnalyzerTests.cs", "coverage": "Semantic analysis"},
    {"id": "IncrementalAnalyzerTests", "label": "IncrementalAnalyzerTests", "type": "test_class", "file": "IncrementalAnalyzerTests.cs", "coverage": "Incremental analysis"},
    {"id": "AstNormalizerTests", "label": "AstNormalizerTests", "type": "test_class", "file": "AstNormalizerTests.cs", "coverage": "AST normalization"},
    {"id": "GlyphConversionTests", "label": "GlyphConversionTests", "type": "test_class", "file": "GlyphConversionTests.cs", "coverage": "Glyph conversion"},
    {"id": "WellKnownPatternsTests", "label": "WellKnownPatternsTests", "type": "test_class", "file": "WellKnownPatternsTests.cs", "coverage": "Pattern matching"},
    {"id": "CodeAnalyzerBaseTests", "label": "CodeAnalyzerBaseTests", "type": "test_class", "file": "CodeAnalyzerBaseTests.cs", "coverage": "Base analyzer"},
    {"id": "DiagnosticCoreTests", "label": "DiagnosticCoreTests", "type": "test_class", "file": "DiagnosticCoreTests.cs", "coverage": "Diagnostic core"},
    {"id": "GuardedPatternsTests", "label": "GuardedPatternsTests", "type": "test_class", "file": "GuardedPatternsTests.cs", "coverage": "Guard patterns"},
    {"id": "RuleBaseTests", "label": "RuleBaseTests", "type": "test_class", "file": "RuleBaseTests.cs", "coverage": "Rule base"},
    {"id": "RuleRegistryTests", "label": "RuleRegistryTests", "type": "test_class", "file": "RuleRegistryTests.cs", "coverage": "Rule registry"},
    {"id": "GauntletEngineTests", "label": "GauntletEngineTests", "type": "test_class", "file": "GauntletEngineTests.cs", "coverage": "Engine"},
    {"id": "ProgramTests", "label": "ProgramTests", "type": "test_class", "file": "ProgramTests.cs", "coverage": "Program entry"},
]

# ============================================================================
# CHUNK 6 ANALYSIS: DiagnosticSummary + Documentation
# ============================================================================

# Core diagnostic aggregation
chunk6_nodes = [
    {"id": "DiagnosticSummary", "label": "DiagnosticSummary", "type": "class", "file": "DiagnosticSummary.cs", "role": "Aggregates diagnostic results"},
    {"id": "DiagnosticSummaryTests", "label": "DiagnosticSummaryTests", "type": "test_class", "file": "DiagnosticSummaryTests.cs", "coverage": "Diagnostic aggregation"},
]

# Documentation nodes
docs = [
    {"id": "API.md", "label": "API Documentation", "type": "doc", "file": "docs/API.md", "section": "Public API reference"},
    {"id": "ARCHITECTURE.md", "label": "Architecture Guide", "type": "doc", "file": "docs/ARCHITECTURE.md", "section": "System design and patterns"},
    {"id": "RULES.md", "label": "Rules Documentation", "type": "doc", "file": "docs/RULES.md", "section": "Rule definitions"},
    {"id": "SECURITY.md.docs", "label": "Security Documentation", "type": "doc", "file": "docs/SECURITY.md", "section": "Security practices"},
    {"id": "README", "label": "Project README", "type": "doc", "file": "README.md", "section": "Overview and getting started"},
    {"id": "CHARTER", "label": "Project Charter", "type": "doc", "file": "CHARTER.md", "section": "Vision and values"},
    {"id": "STORY", "label": "Project Story", "type": "doc", "file": "STORY.md", "section": "Historical narrative"},
]

# Configuration and deployment
config_nodes = [
    {"id": "action.yml", "label": "GitHub Action Config", "type": "config", "file": "action.yml", "purpose": "GH Actions integration"},
    {"id": "Dockerfile", "label": "Docker Configuration", "type": "config", "file": "worker/Dockerfile", "purpose": "Container setup"},
    {"id": "GauntletCI.nuspec", "label": "NuGet Spec", "type": "config", "file": "packaging/nuget/GauntletCI.nuspec", "purpose": "NuGet packaging"},
]

# ============================================================================
# CHUNK 7 ANALYSIS: Engineering Standards & Schemas
# ============================================================================

engineering_docs = [
    {"id": "ENGINEERING_STANDARDS", "label": "Engineering Standards", "type": "engineering_doc", "file": ".misc/ENGINEERING_STANDARDS.md", "domain": "Development practices"},
    {"id": "PHASE18_COMPLETION", "label": "Phase 18 Completion Summary", "type": "engineering_doc", "file": ".misc/PHASE18_COMPLETION_SUMMARY.md", "domain": "Completion milestone"},
    {"id": "PHASE18_INVARIANTS", "label": "Engineering Invariants", "type": "engineering_doc", "file": ".misc/PHASE18_ENGINEERING_INVARIANTS.md", "domain": "System invariants"},
    {"id": "PHASE18_ARCHITECTURE", "label": "Architecture Phase 18", "type": "engineering_doc", "file": ".misc/PHASE18_ARCHITECTURE.md", "domain": "Architecture evolution"},
    {"id": "consolidated_rules", "label": "Consolidated Engineering Rules", "type": "engineering_doc", "file": ".misc/consolidated_engineering_rules.md", "domain": "Unified rules"},
]

schemas = [
    {"id": "gauntletci_schema", "label": "GauntletCI Schema", "type": "schema", "file": "schemas/gauntletci-schema.json", "format": "JSON Schema"},
    {"id": "best_practices_json", "label": "Best Practices Config", "type": "schema", "file": ".misc/best-practices.json", "format": "JSON Config"},
    {"id": "manifest_json", "label": "Project Manifest", "type": "schema", "file": ".misc/MANIFEST.json", "format": "JSON Metadata"},
]

# ============================================================================
# CHUNK 8 ANALYSIS: Data & Project Config
# ============================================================================

project_config = [
    {"id": "GauntletCI.csproj", "label": "C# Project File", "type": "project_config", "file": "src/GauntletCI.Core/GauntletCI.Core.csproj", "language": "C#"},
    {"id": "GauntletCI.slnx", "label": "Solution File", "type": "project_config", "file": "GauntletCI.slnx", "language": "C#"},
    {"id": "deploy.yml", "label": "Deployment Config", "type": "config", "file": "deploy.yml", "purpose": "CD/deployment"},
]

data_nodes = [
    {"id": "baseline_corpus", "label": "Baseline Corpus Results", "type": "data", "file": "data/baseline-corpus-results.json", "content": "Test results and baselines"},
]

# ============================================================================
# BUILD NODES ARRAY
# ============================================================================

all_nodes = (
    analyzers + test_classes + chunk6_nodes + docs + config_nodes +
    engineering_docs + schemas + project_config + data_nodes
)

for node in all_nodes:
    graph["nodes"].append({
        "id": node["id"],
        "label": node["label"],
        "file_type": node.get("type", "unknown"),
        "source_file": node.get("file", ""),
        "source_location": None,
        "source_url": None,
        "captured_at": None,
        "author": None,
        "contributor": None,
        "attributes": {k: v for k, v in node.items() if k not in ["id", "label", "type", "file"]}
    })

# ============================================================================
# ARCHITECTURE RELATIONSHIPS (EXTRACTED & INFERRED)
# ============================================================================

edges = [
    # Abstraction hierarchy - EXTRACTED
    ("IGauntletAnalyzer", "IGauntletRule", "defines_contract_for", "EXTRACTED", 1.0),
    ("IGauntletAnalyzer", "IGauntletDiagnostic", "produces", "EXTRACTED", 1.0),
    ("IGauntletDiagnostic", "IProblemLocation", "contains", "EXTRACTED", 1.0),
    ("IProblemLocation", "ProblemLocation", "implemented_by", "EXTRACTED", 1.0),
    
    # Analyzer implementations - INFERRED (high confidence from class names)
    ("IncrementalAnalyzer", "IGauntletAnalyzer", "implements", "INFERRED", 0.95),
    ("DiagnosticSummary", "IGauntletDiagnostic", "implements", "INFERRED", 0.95),
    
    # Test-code coupling: test directly validates implementations
    ("RoslynDiagnosticAnalyzerTests", "IGauntletAnalyzer", "tests", "EXTRACTED", 1.0),
    ("SymbolAnalyzerTests", "IGauntletAnalyzer", "tests", "EXTRACTED", 1.0),
    ("SymbolCachingAnalyzerTests", "IGauntletAnalyzer", "tests", "EXTRACTED", 1.0),
    ("SyntaxAnalyzerTests", "IGauntletAnalyzer", "tests", "EXTRACTED", 1.0),
    ("SemanticAnalyzerTests", "IGauntletAnalyzer", "tests", "EXTRACTED", 1.0),
    ("IncrementalAnalyzerTests", "IncrementalAnalyzer", "tests", "EXTRACTED", 1.0),
    
    # Utility test coverage
    ("AstNormalizerTests", "IGauntletAnalyzer", "tests_utility_for", "INFERRED", 0.85),
    ("GlyphConversionTests", "IGauntletAnalyzer", "tests_utility_for", "INFERRED", 0.85),
    ("WellKnownPatternsTests", "IGauntletAnalyzer", "tests_utility_for", "INFERRED", 0.85),
    
    # Core infrastructure tests
    ("CodeAnalyzerBaseTests", "IGauntletAnalyzer", "tests_base_class", "EXTRACTED", 1.0),
    ("DiagnosticCoreTests", "DiagnosticSummary", "tests", "EXTRACTED", 1.0),
    ("GuardedPatternsTests", "IGauntletRule", "tests_pattern_impl", "INFERRED", 0.8),
    ("RuleBaseTests", "IGauntletRule", "tests", "EXTRACTED", 1.0),
    ("RuleRegistryTests", "IGauntletRule", "tests_registration", "EXTRACTED", 1.0),
    ("GauntletEngineTests", "IncrementalAnalyzer", "tests_orchestration", "INFERRED", 0.9),
    ("ProgramTests", "IncrementalAnalyzer", "tests_entrypoint", "INFERRED", 0.9),
    
    # Documentation-code couplings (LATENT - critical for knowledge graph)
    ("ARCHITECTURE.md", "IGauntletAnalyzer", "documents", "INFERRED", 0.85),
    ("ARCHITECTURE.md", "IGauntletRule", "documents", "INFERRED", 0.85),
    ("ARCHITECTURE.md", "IncrementalAnalyzer", "documents", "INFERRED", 0.85),
    ("API.md", "IGauntletAnalyzer", "documents", "EXTRACTED", 1.0),
    ("API.md", "IGauntletRule", "documents", "EXTRACTED", 1.0),
    ("API.md", "IGauntletDiagnostic", "documents", "EXTRACTED", 1.0),
    ("API.md", "IProblemLocation", "documents", "EXTRACTED", 1.0),
    ("RULES.md", "IGauntletRule", "documents", "EXTRACTED", 1.0),
    ("RULES.md", "RuleBaseTests", "references_tests_for", "INFERRED", 0.75),
    
    # Engineering standards enforce test coverage
    ("ENGINEERING_STANDARDS", "CodeAnalyzerBaseTests", "governs", "INFERRED", 0.8),
    ("ENGINEERING_STANDARDS", "DiagnosticCoreTests", "governs", "INFERRED", 0.8),
    ("PHASE18_INVARIANTS", "IGauntletAnalyzer", "enforces_invariants_on", "INFERRED", 0.75),
    ("PHASE18_INVARIANTS", "IGauntletRule", "enforces_invariants_on", "INFERRED", 0.75),
    ("PHASE18_ARCHITECTURE", "IncrementalAnalyzer", "describes_evolution", "INFERRED", 0.8),
    
    # Schema validation
    ("gauntletci_schema", "action.yml", "validates", "INFERRED", 0.75),
    ("gauntletci_schema", "GauntletCI.nuspec", "defines_format_for", "INFERRED", 0.7),
    ("best_practices_json", "ENGINEERING_STANDARDS", "implements", "INFERRED", 0.8),
    
    # Deployment and integration
    ("action.yml", "IncrementalAnalyzer", "orchestrates", "INFERRED", 0.85),
    ("Dockerfile", "GauntletCI.csproj", "builds", "EXTRACTED", 1.0),
    ("deploy.yml", "Dockerfile", "uses", "EXTRACTED", 1.0),
    ("GauntletCI.nuspec", "GauntletCI.csproj", "packages", "EXTRACTED", 1.0),
    
    # Baseline data anchors to testing
    ("baseline_corpus", "DiagnosticSummaryTests", "provides_test_data", "EXTRACTED", 1.0),
    ("baseline_corpus", "GauntletEngineTests", "provides_test_data", "EXTRACTED", 1.0),
]

for src, tgt, rel, conf_type, score in edges:
    graph["edges"].append({
        "source": src,
        "target": tgt,
        "relation": rel,
        "confidence": conf_type,
        "confidence_score": score,
        "source_file": "cross_chunk_analysis",
        "source_location": None,
        "weight": 1.0
    })

# ============================================================================
# HYPEREDGES: Higher-order relationships
# ============================================================================

hyperedges = [
    {
        "id": "analyzer_protocol_impl",
        "label": "Analyzer Implementation Protocol",
        "nodes": ["IGauntletAnalyzer", "RoslynDiagnosticAnalyzerTests", "SymbolAnalyzerTests", 
                  "SyntaxAnalyzerTests", "SemanticAnalyzerTests", "IncrementalAnalyzer"],
        "relation": "implement",
        "confidence": "EXTRACTED",
        "confidence_score": 0.9,
        "source_file": "architecture_analysis",
        "rationale": "All analyzers conform to the same protocol with comprehensive test coverage"
    },
    {
        "id": "rule_and_diagnostic_flow",
        "label": "Rule-to-Diagnostic Analysis Flow",
        "nodes": ["IGauntletRule", "IGauntletAnalyzer", "IGauntletDiagnostic", 
                  "IProblemLocation", "DiagnosticSummary"],
        "relation": "participate_in",
        "confidence": "EXTRACTED",
        "confidence_score": 0.95,
        "source_file": "architecture_analysis",
        "rationale": "Coordinated flow from rule definition through analysis to diagnostic result"
    },
    {
        "id": "test_coverage_tier1",
        "label": "Core Infrastructure Tests",
        "nodes": ["CodeAnalyzerBaseTests", "DiagnosticCoreTests", "RuleBaseTests", 
                  "GauntletEngineTests", "ProgramTests"],
        "relation": "implement",
        "confidence": "EXTRACTED",
        "confidence_score": 0.95,
        "source_file": "test_analysis",
        "rationale": "Tests covering the foundational layers that all analyzers depend on"
    },
    {
        "id": "test_coverage_tier2",
        "label": "Analyzer-Specific Tests",
        "nodes": ["RoslynDiagnosticAnalyzerTests", "SymbolAnalyzerTests", 
                  "SymbolCachingAnalyzerTests", "SyntaxAnalyzerTests", "SemanticAnalyzerTests",
                  "IncrementalAnalyzerTests"],
        "relation": "implement",
        "confidence": "EXTRACTED",
        "confidence_score": 0.95,
        "source_file": "test_analysis",
        "rationale": "Comprehensive per-analyzer testing with specialized test cases"
    },
    {
        "id": "doc_to_code_alignment",
        "label": "Documentation-Code Alignment",
        "nodes": ["ARCHITECTURE.md", "API.md", "RULES.md", "IGauntletAnalyzer", 
                  "IGauntletRule", "IGauntletDiagnostic"],
        "relation": "participate_in",
        "confidence": "INFERRED",
        "confidence_score": 0.85,
        "source_file": "documentation_analysis",
        "rationale": "Documentation directly describes the public API contracts that code implements"
    },
    {
        "id": "engineering_governance",
        "label": "Engineering Governance Structure",
        "nodes": ["ENGINEERING_STANDARDS", "PHASE18_INVARIANTS", "PHASE18_ARCHITECTURE",
                  "consolidated_rules", "best_practices_json"],
        "relation": "form",
        "confidence": "EXTRACTED",
        "confidence_score": 0.9,
        "source_file": "governance_analysis",
        "rationale": "Unified engineering standards enforced through invariants and architecture decisions"
    },
]

graph["hyperedges"] = hyperedges

# ============================================================================
# OUTPUT
# ============================================================================

with open('graphify-out/chunks_5_8_knowledge_graph.json', 'w') as f:
    json.dump(graph, f, indent=2)

print(f"Knowledge graph extracted:")
print(f"  Nodes: {len(graph['nodes'])}")
print(f"  Edges: {len(graph['edges'])}")
print(f"  Hyperedges: {len(graph['hyperedges'])}")
print(f"  File: graphify-out/chunks_5_8_knowledge_graph.json")

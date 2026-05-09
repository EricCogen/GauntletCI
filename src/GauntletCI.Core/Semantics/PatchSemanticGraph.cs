// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Core.Semantics;

/// <summary>
/// Defines the type of a node in a patch semantic graph.
/// Nodes represent semantic entities at different levels of granularity.
/// </summary>
public enum PatchGraphNodeKind
{
    /// <summary>Root node representing the entire patch.</summary>
    Patch,

    /// <summary>Node representing a changed file.</summary>
    File,

    /// <summary>Node representing a hunk (contiguous region of changes).</summary>
    Hunk,

    /// <summary>Node representing a single added or removed line.</summary>
    Line,

    /// <summary>Node representing a low-level operation (operator change, identifier rename, etc.).</summary>
    Operation,

    /// <summary>Node representing a high-level semantic transformation (refactoring, logic change, etc.).</summary>
    Transformation,

    /// <summary>Node representing evidence supporting a finding.</summary>
    Evidence,

    /// <summary>Node representing a semantic concept (gravity, density, conservation).</summary>
    SemanticConcept,

    /// <summary>Unknown or unclassified node type.</summary>
    Unknown
}

/// <summary>
/// Defines the types of edges (relationships) in a patch semantic graph.
/// Edges express semantic relationships between nodes.
/// </summary>
public enum PatchGraphEdgeKind
{
    /// <summary>Edge A contains edge B as a sub-component (structural containment).</summary>
    Contains,

    /// <summary>Edge A is derived from or computed from edge B.</summary>
    DerivedFrom,

    /// <summary>Edge A replaces edge B (refactoring: old code → new code).</summary>
    Replaces,

    /// <summary>Edge A weakens edge B (removes validation, lessens constraints).</summary>
    Weakens,

    /// <summary>Edge A strengthens edge B (adds validation, tightens constraints).</summary>
    Strengthens,

    /// <summary>Edge A enables edge B (new code path, new capability).</summary>
    Enables,

    /// <summary>Edge A disables edge B (removed code path, removed capability).</summary>
    Disables,

    /// <summary>Edge A affects edge B but the nature is unclear.</summary>
    Affects,

    /// <summary>Edge A conflicts with edge B (contradictory behavior).</summary>
    Conflicts,

    /// <summary>Edge A depends on edge B (must occur after B).</summary>
    DependsOn,

    /// <summary>Edge A is evidence for edge B (supports a claim).</summary>
    EvidenceFor,

    /// <summary>Unknown or unclassified edge type.</summary>
    Unknown
}

/// <summary>
/// Represents a single node in a patch semantic graph.
/// Nodes form the vertices of the graph; edges connect them.
/// </summary>
public sealed class PatchGraphNode
{
    /// <summary>Unique identifier for this node within the graph.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>The semantic kind of this node.</summary>
    public PatchGraphNodeKind Kind
    {
        get; init;
    }

    /// <summary>Human-readable label for this node (e.g., "File: Service.cs", "Operation: LineAdded").</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>Detailed description of what this node represents.</summary>
    public string? Description
    {
        get; init;
    }

    /// <summary>Optional metadata (e.g., line number, operation kind, transformation kind).</summary>
    public Dictionary<string, object> Metadata { get; init; } = [];

    /// <summary>Risk or confidence score associated with this node (0.0-1.0).</summary>
    public double Score
    {
        get; init;
    }
}

/// <summary>
/// Represents an edge (relationship) between two nodes in a patch semantic graph.
/// Edges are directed: from source node to target node.
/// </summary>
public sealed class PatchGraphEdge
{
    /// <summary>Unique identifier for this edge.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>The source node ID this edge originates from.</summary>
    public string SourceNodeId { get; init; } = string.Empty;

    /// <summary>The target node ID this edge points to.</summary>
    public string TargetNodeId { get; init; } = string.Empty;

    /// <summary>The semantic kind of this relationship.</summary>
    public PatchGraphEdgeKind Kind
    {
        get; init;
    }

    /// <summary>Human-readable label for this edge (e.g., "contains", "derives from").</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>Detailed reasoning for this edge relationship.</summary>
    public string? Reason
    {
        get; init;
    }

    /// <summary>Confidence in this edge (0.0-1.0).</summary>
    public double Confidence { get; init; } = 1.0;
}

/// <summary>
/// Represents the complete semantic graph for a single patch.
/// The graph captures structural containment, semantic relationships, and derivation chains.
/// </summary>
public sealed class PatchSemanticGraph
{
    private readonly Dictionary<string, PatchGraphNode> _nodes = [];
    private readonly List<PatchGraphEdge> _edges = [];

    /// <summary>The patch this graph represents.</summary>
    public PatchModel? Patch
    {
        get; init;
    }

    /// <summary>All nodes in the graph, indexed by ID.</summary>
    public IReadOnlyDictionary<string, PatchGraphNode> Nodes => _nodes.AsReadOnly();

    /// <summary>All edges in the graph.</summary>
    public IReadOnlyList<PatchGraphEdge> Edges => _edges.AsReadOnly();

    /// <summary>Adds a node to the graph.</summary>
    public void AddNode(PatchGraphNode node)
    {
        ArgumentNullException.ThrowIfNull(node, nameof(node));
        _nodes[node.Id] = node;
    }

    /// <summary>Adds an edge to the graph.</summary>
    public void AddEdge(PatchGraphEdge edge)
    {
        ArgumentNullException.ThrowIfNull(edge, nameof(edge));
        _edges.Add(edge);
    }

    /// <summary>Gets a node by its ID, or null if not found.</summary>
    public PatchGraphNode? GetNode(string nodeId)
    {
        ArgumentNullException.ThrowIfNull(nodeId, nameof(nodeId));
        _nodes.TryGetValue(nodeId, out var node);
        return node;
    }

    /// <summary>Gets all edges originating from a specific node.</summary>
    public IEnumerable<PatchGraphEdge> EdgesFrom(string sourceNodeId)
    {
        ArgumentNullException.ThrowIfNull(sourceNodeId, nameof(sourceNodeId));
        return _edges.Where(e => e.SourceNodeId == sourceNodeId);
    }

    /// <summary>Gets all edges pointing to a specific node.</summary>
    public IEnumerable<PatchGraphEdge> EdgesTo(string targetNodeId)
    {
        ArgumentNullException.ThrowIfNull(targetNodeId, nameof(targetNodeId));
        return _edges.Where(e => e.TargetNodeId == targetNodeId);
    }

    /// <summary>Gets all edges of a specific kind.</summary>
    public IEnumerable<PatchGraphEdge> EdgesByKind(PatchGraphEdgeKind kind)
    {
        return _edges.Where(e => e.Kind == kind);
    }

    /// <summary>Gets all nodes of a specific kind.</summary>
    public IEnumerable<PatchGraphNode> NodesByKind(PatchGraphNodeKind kind)
    {
        return _nodes.Values.Where(n => n.Kind == kind);
    }

    /// <summary>Gets the root patch node, if present.</summary>
    public PatchGraphNode? PatchNode => _nodes.Values.FirstOrDefault(n => n.Kind == PatchGraphNodeKind.Patch);

    /// <summary>Gets all file nodes.</summary>
    public IEnumerable<PatchGraphNode> FileNodes => NodesByKind(PatchGraphNodeKind.File);

    /// <summary>Gets all operation nodes.</summary>
    public IEnumerable<PatchGraphNode> OperationNodes => NodesByKind(PatchGraphNodeKind.Operation);

    /// <summary>Gets all transformation nodes.</summary>
    public IEnumerable<PatchGraphNode> TransformationNodes => NodesByKind(PatchGraphNodeKind.Transformation);

    /// <summary>Gets all evidence nodes.</summary>
    public IEnumerable<PatchGraphNode> EvidenceNodes => NodesByKind(PatchGraphNodeKind.Evidence);

    /// <summary>Counts total nodes in the graph.</summary>
    public int NodeCount => _nodes.Count;

    /// <summary>Counts total edges in the graph.</summary>
    public int EdgeCount => _edges.Count;

    /// <summary>Clears all nodes and edges from the graph.</summary>
    public void Clear()
    {
        _nodes.Clear();
        _edges.Clear();
    }
}

/// <summary>
/// Constructs a semantic graph from patch analysis results.
/// Maps patch structure (files, hunks, lines) and semantic analysis (operations, transformations, evidence)
/// into a graph with typed nodes and edges.
/// </summary>
public static class PatchSemanticGraphBuilder
{
    private static int _nodeIdCounter;

    /// <summary>Builds a complete semantic graph from a patch and its analysis.</summary>
    public static PatchSemanticGraph BuildGraph(
        PatchModel patch,
        PatchOperationCollection operations,
        PatchTransformationCollection transformations,
        PatchEvidenceCollection evidence)
    {
        ArgumentNullException.ThrowIfNull(patch, nameof(patch));
        ArgumentNullException.ThrowIfNull(operations, nameof(operations));
        ArgumentNullException.ThrowIfNull(transformations, nameof(transformations));
        ArgumentNullException.ThrowIfNull(evidence, nameof(evidence));

        _nodeIdCounter = 0;
        var graph = new PatchSemanticGraph { Patch = patch };

        // 1. Create root patch node
        var patchNode = CreatePatchNode(patch);
        graph.AddNode(patchNode);

        // 2. Create file nodes and wire to patch
        var fileNodes = new Dictionary<string, PatchGraphNode>();
        foreach (var file in patch.Files)
        {
            var fileNode = CreateFileNode(file);
            graph.AddNode(fileNode);
            fileNodes[file.NewPath ?? file.OldPath ?? "unknown"] = fileNode;

            // File contains Hunk
            graph.AddEdge(new PatchGraphEdge
            {
                Id = $"edge_{_nodeIdCounter++}",
                SourceNodeId = patchNode.Id,
                TargetNodeId = fileNode.Id,
                Kind = PatchGraphEdgeKind.Contains,
                Label = "contains",
                Reason = "Patch contains this file"
            });
        }

        // 3. Create operation nodes
        foreach (var op in operations.All)
        {
            var opNode = CreateOperationNode(op);
            graph.AddNode(opNode);

            // Operation derived from file
            if (op.FilePath != null && fileNodes.TryGetValue(op.FilePath, out var fileNode))
            {
                graph.AddEdge(new PatchGraphEdge
                {
                    Id = $"edge_{_nodeIdCounter++}",
                    SourceNodeId = opNode.Id,
                    TargetNodeId = fileNode.Id,
                    Kind = PatchGraphEdgeKind.DerivedFrom,
                    Label = "derived from file",
                    Reason = $"Operation extracted from {op.FilePath}"
                });
            }
        }

        // 4. Create transformation nodes
        foreach (var transform in transformations.All)
        {
            var transformNode = CreateTransformationNode(transform);
            graph.AddNode(transformNode);

            // Transformation affects files
            foreach (var file in transform.AffectedFiles)
            {
                if (fileNodes.TryGetValue(file, out var fileNode))
                {
                    var edgeKind = transform.IsSafePattern ? PatchGraphEdgeKind.Affects : PatchGraphEdgeKind.Affects;
                    graph.AddEdge(new PatchGraphEdge
                    {
                        Id = $"edge_{_nodeIdCounter++}",
                        SourceNodeId = transformNode.Id,
                        TargetNodeId = fileNode.Id,
                        Kind = edgeKind,
                        Label = "affects",
                        Reason = $"Transformation {transform.Kind} affects {file}"
                    });
                }
            }
        }

        // 5. Create evidence nodes
        foreach (var ev in evidence.All)
        {
            var evNode = CreateEvidenceNode(ev);
            graph.AddNode(evNode);

            // Evidence supports or contradicts transformations
            if (!string.IsNullOrEmpty(ev.FilePath))
            {
                if (fileNodes.TryGetValue(ev.FilePath, out var fileNode))
                {
                    graph.AddEdge(new PatchGraphEdge
                    {
                        Id = $"edge_{_nodeIdCounter++}",
                        SourceNodeId = evNode.Id,
                        TargetNodeId = fileNode.Id,
                        Kind = PatchGraphEdgeKind.EvidenceFor,
                        Label = "evidence for",
                        Reason = $"Evidence kind {ev.Kind} found in {ev.FilePath}",
                        Confidence = ev.Confidence
                    });
                }
            }
        }

        return graph;
    }

    private static PatchGraphNode CreatePatchNode(PatchModel patch)
    {
        return new PatchGraphNode
        {
            Id = $"node_{_nodeIdCounter++}",
            Kind = PatchGraphNodeKind.Patch,
            Label = "Patch",
            Description = $"Complete patch with {patch.Files.Count} files, {patch.CountAddedLines()} added, {patch.CountRemovedLines()} removed",
            Metadata = new Dictionary<string, object>
            {
                { "source", patch.Source },
                { "commitSha", patch.CommitSha ?? "unknown" },
                { "fileCount", patch.Files.Count },
                { "addedLines", patch.CountAddedLines() },
                { "removedLines", patch.CountRemovedLines() }
            },
            Score = 1.0
        };
    }

    private static PatchGraphNode CreateFileNode(PatchFile file)
    {
        var filePath = file.NewPath ?? file.OldPath ?? "unknown";
        return new PatchGraphNode
        {
            Id = $"node_{_nodeIdCounter++}",
            Kind = PatchGraphNodeKind.File,
            Label = $"File: {filePath}",
            Description = $"File {file.ChangeKind}: {file.HunkCount} hunks",
            Metadata = new Dictionary<string, object>
            {
                { "path", filePath },
                { "changeKind", file.ChangeKind.ToString() },
                { "isTestFile", file.IsTestFile },
                { "hunkCount", file.Hunks.Count }
            },
            Score = 0.5
        };
    }

    private static PatchGraphNode CreateOperationNode(PatchOperation op)
    {
        return new PatchGraphNode
        {
            Id = $"node_{_nodeIdCounter++}",
            Kind = PatchGraphNodeKind.Operation,
            Label = $"Operation: {op.Kind}",
            Description = op.Description,
            Metadata = new Dictionary<string, object>
            {
                { "kind", op.Kind.ToString() },
                { "filePath", op.FilePath ?? "unknown" },
                { "symbol", op.Symbol ?? "unknown" },
                { "riskLevel", op.RiskLevel }
            },
            Score = op.RiskLevel
        };
    }

    private static PatchGraphNode CreateTransformationNode(PatchTransformation transform)
    {
        return new PatchGraphNode
        {
            Id = $"node_{_nodeIdCounter++}",
            Kind = PatchGraphNodeKind.Transformation,
            Label = $"Transformation: {transform.Kind}",
            Description = transform.Description,
            Metadata = new Dictionary<string, object>
            {
                { "kind", transform.Kind.ToString() },
                { "targetSymbol", transform.TargetSymbol ?? "unknown" },
                { "isSafePattern", transform.IsSafePattern },
                { "behavioralImpact", transform.BehavioralImpact },
                { "riskLevel", transform.RiskLevel }
            },
            Score = transform.RiskLevel
        };
    }

    private static PatchGraphNode CreateEvidenceNode(PatchEvidence ev)
    {
        return new PatchGraphNode
        {
            Id = $"node_{_nodeIdCounter++}",
            Kind = PatchGraphNodeKind.Evidence,
            Label = $"Evidence: {ev.Kind}",
            Description = ev.Description,
            Metadata = new Dictionary<string, object>
            {
                { "kind", ev.Kind.ToString() },
                { "knowledgeLevel", ev.KnowledgeLevel.ToString() },
                { "filePath", ev.FilePath ?? "unknown" },
                { "confidence", ev.Confidence }
            },
            Score = ev.Confidence
        };
    }
}

/// <summary>Extension methods for accessing hunk count on PatchFile.</summary>
internal static class PatchFileExtensions
{
    public static int HunkCount(this PatchFile file) => file.Hunks.Count;
}

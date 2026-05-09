// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Semantics;

namespace GauntletCI.Core.Tests.Semantics;

/// <summary>
/// Tests for PatchSemanticGraph construction and querying.
/// Verifies graph structure, node/edge relationships, and serialization readiness.
/// </summary>
public class PatchSemanticGraphTests
{
    [Fact]
    public void EmptyGraph_HasInitialState()
    {
        // Arrange & Act
        var graph = new PatchSemanticGraph();

        // Assert
        Assert.Empty(graph.Nodes);
        Assert.Empty(graph.Edges);
        Assert.Equal(0, graph.NodeCount);
        Assert.Equal(0, graph.EdgeCount);
    }

    [Fact]
    public void AddNode_IncreasesNodeCount()
    {
        // Arrange
        var graph = new PatchSemanticGraph();
        var node = new PatchGraphNode
        {
            Id = "node1",
            Kind = PatchGraphNodeKind.Patch,
            Label = "Test Patch"
        };

        // Act
        graph.AddNode(node);

        // Assert
        Assert.Single(graph.Nodes);
        Assert.Equal(1, graph.NodeCount);
        Assert.Same(node, graph.GetNode("node1"));
    }

    [Fact]
    public void AddEdge_IncreasesEdgeCount()
    {
        // Arrange
        var graph = new PatchSemanticGraph();
        var node1 = new PatchGraphNode { Id = "n1", Kind = PatchGraphNodeKind.Patch, Label = "P1" };
        var node2 = new PatchGraphNode { Id = "n2", Kind = PatchGraphNodeKind.File, Label = "F1" };
        graph.AddNode(node1);
        graph.AddNode(node2);

        var edge = new PatchGraphEdge
        {
            Id = "e1",
            SourceNodeId = "n1",
            TargetNodeId = "n2",
            Kind = PatchGraphEdgeKind.Contains,
            Label = "contains"
        };

        // Act
        graph.AddEdge(edge);

        // Assert
        Assert.Single(graph.Edges);
        Assert.Equal(1, graph.EdgeCount);
    }

    [Fact]
    public void EdgesFrom_ReturnsCorrectEdges()
    {
        // Arrange
        var graph = new PatchSemanticGraph();
        var n1 = new PatchGraphNode { Id = "n1", Kind = PatchGraphNodeKind.Patch };
        var n2 = new PatchGraphNode { Id = "n2", Kind = PatchGraphNodeKind.File };
        var n3 = new PatchGraphNode { Id = "n3", Kind = PatchGraphNodeKind.File };
        graph.AddNode(n1);
        graph.AddNode(n2);
        graph.AddNode(n3);

        // Add two edges from n1
        graph.AddEdge(new PatchGraphEdge { Id = "e1", SourceNodeId = "n1", TargetNodeId = "n2", Kind = PatchGraphEdgeKind.Contains });
        graph.AddEdge(new PatchGraphEdge { Id = "e2", SourceNodeId = "n1", TargetNodeId = "n3", Kind = PatchGraphEdgeKind.Contains });

        // Act
        var edgesFromN1 = graph.EdgesFrom("n1").ToList();

        // Assert
        Assert.Equal(2, edgesFromN1.Count);
    }

    [Fact]
    public void EdgesTo_ReturnsCorrectEdges()
    {
        // Arrange
        var graph = new PatchSemanticGraph();
        var n1 = new PatchGraphNode { Id = "n1", Kind = PatchGraphNodeKind.Patch };
        var n2 = new PatchGraphNode { Id = "n2", Kind = PatchGraphNodeKind.File };
        graph.AddNode(n1);
        graph.AddNode(n2);

        graph.AddEdge(new PatchGraphEdge { Id = "e1", SourceNodeId = "n1", TargetNodeId = "n2", Kind = PatchGraphEdgeKind.Contains });
        graph.AddEdge(new PatchGraphEdge { Id = "e2", SourceNodeId = "n1", TargetNodeId = "n2", Kind = PatchGraphEdgeKind.Affects });

        // Act
        var edgesToN2 = graph.EdgesTo("n2").ToList();

        // Assert
        Assert.Equal(2, edgesToN2.Count);
    }

    [Fact]
    public void NodesByKind_FiltersCorrectly()
    {
        // Arrange
        var graph = new PatchSemanticGraph();
        graph.AddNode(new PatchGraphNode { Id = "n1", Kind = PatchGraphNodeKind.Patch });
        graph.AddNode(new PatchGraphNode { Id = "n2", Kind = PatchGraphNodeKind.File });
        graph.AddNode(new PatchGraphNode { Id = "n3", Kind = PatchGraphNodeKind.File });
        graph.AddNode(new PatchGraphNode { Id = "n4", Kind = PatchGraphNodeKind.Operation });

        // Act
        var fileNodes = graph.NodesByKind(PatchGraphNodeKind.File).ToList();
        var opNodes = graph.NodesByKind(PatchGraphNodeKind.Operation).ToList();

        // Assert
        Assert.Equal(2, fileNodes.Count);
        Assert.Single(opNodes);
    }

    [Fact]
    public void EdgesByKind_FiltersCorrectly()
    {
        // Arrange
        var graph = new PatchSemanticGraph();
        var n1 = new PatchGraphNode { Id = "n1", Kind = PatchGraphNodeKind.Patch };
        var n2 = new PatchGraphNode { Id = "n2", Kind = PatchGraphNodeKind.File };
        var n3 = new PatchGraphNode { Id = "n3", Kind = PatchGraphNodeKind.Operation };
        graph.AddNode(n1);
        graph.AddNode(n2);
        graph.AddNode(n3);

        graph.AddEdge(new PatchGraphEdge { Id = "e1", SourceNodeId = "n1", TargetNodeId = "n2", Kind = PatchGraphEdgeKind.Contains });
        graph.AddEdge(new PatchGraphEdge { Id = "e2", SourceNodeId = "n2", TargetNodeId = "n3", Kind = PatchGraphEdgeKind.DerivedFrom });
        graph.AddEdge(new PatchGraphEdge { Id = "e3", SourceNodeId = "n1", TargetNodeId = "n3", Kind = PatchGraphEdgeKind.Contains });

        // Act
        var containEdges = graph.EdgesByKind(PatchGraphEdgeKind.Contains).ToList();
        var derivedEdges = graph.EdgesByKind(PatchGraphEdgeKind.DerivedFrom).ToList();

        // Assert
        Assert.Equal(2, containEdges.Count);
        Assert.Single(derivedEdges);
    }

    [Fact]
    public void FileNodes_ReturnsOnlyFileNodes()
    {
        // Arrange
        var graph = new PatchSemanticGraph();
        graph.AddNode(new PatchGraphNode { Id = "n1", Kind = PatchGraphNodeKind.Patch });
        graph.AddNode(new PatchGraphNode { Id = "n2", Kind = PatchGraphNodeKind.File });
        graph.AddNode(new PatchGraphNode { Id = "n3", Kind = PatchGraphNodeKind.File });
        graph.AddNode(new PatchGraphNode { Id = "n4", Kind = PatchGraphNodeKind.Operation });

        // Act
        var fileNodes = graph.FileNodes.ToList();

        // Assert
        Assert.Equal(2, fileNodes.Count);
        Assert.All(fileNodes, n => Assert.Equal(PatchGraphNodeKind.File, n.Kind));
    }

    [Fact]
    public void Clear_RemovesAllNodesAndEdges()
    {
        // Arrange
        var graph = new PatchSemanticGraph();
        graph.AddNode(new PatchGraphNode { Id = "n1", Kind = PatchGraphNodeKind.Patch });
        graph.AddNode(new PatchGraphNode { Id = "n2", Kind = PatchGraphNodeKind.File });
        graph.AddEdge(new PatchGraphEdge { Id = "e1", SourceNodeId = "n1", TargetNodeId = "n2", Kind = PatchGraphEdgeKind.Contains });

        // Act
        graph.Clear();

        // Assert
        Assert.Empty(graph.Nodes);
        Assert.Empty(graph.Edges);
    }

    [Fact]
    public void BuildGraph_SimplePatch_CreatesNodes()
    {
        // Arrange: Create minimal patch
        var patchModel = new PatchModel
        {
            Files = new[]
            {
                new PatchFile
                {
                    NewPath = "test.cs",
                    ChangeKind = PatchFileChangeKind.Modified,
                    Hunks = new[] {
                        new PatchHunk
                        {
                            Header = "@@ -1,3 +1,4 @@",
                            Lines = new[] {
                                new PatchLine { Kind = PatchLineKind.Added, NewLineNumber = 1, Text = "new line" }
                            }
                        }
                    }
                }
            }
        };

        var operations = new PatchOperationCollection();
        operations.Add(PatchOperationFactory.LineAdded(1, "test.cs", "new line"));

        var transformations = new PatchTransformationCollection();
        transformations.Add(new PatchTransformation { Kind = PatchTransformationKind.LogicSimplified });

        var evidence = new PatchEvidenceCollection();
        evidence.Add(PatchEvidenceFactory.AddedLine(1, "test.cs", "new line"));

        // Act
        var graph = PatchSemanticGraphBuilder.BuildGraph(patchModel, operations, transformations, evidence);

        // Assert
        Assert.NotNull(graph.PatchNode);
        Assert.True(graph.FileNodes.Any());
        Assert.True(graph.OperationNodes.Any());
        Assert.True(graph.TransformationNodes.Any());
        Assert.True(graph.EvidenceNodes.Any());
    }

    [Fact]
    public void BuildGraph_MultipleFiles_CreatesFileNodes()
    {
        // Arrange
        var patchModel = new PatchModel
        {
            Files = new[]
            {
                new PatchFile { NewPath = "a.cs", ChangeKind = PatchFileChangeKind.Modified, Hunks = [] },
                new PatchFile { NewPath = "b.cs", ChangeKind = PatchFileChangeKind.Added, Hunks = [] }
            }
        };

        var operations = new PatchOperationCollection();
        var transformations = new PatchTransformationCollection();
        var evidence = new PatchEvidenceCollection();

        // Act
        var graph = PatchSemanticGraphBuilder.BuildGraph(patchModel, operations, transformations, evidence);

        // Assert
        var fileNodes = graph.FileNodes.ToList();
        Assert.Equal(2, fileNodes.Count);
    }

    [Fact]
    public void BuildGraph_WithOperations_CreatesOperationNodes()
    {
        // Arrange
        var patchModel = new PatchModel { Files = [] };
        var operations = new PatchOperationCollection();
        operations.Add(PatchOperationFactory.LineAdded(1, "test.cs", "line1"));
        operations.Add(PatchOperationFactory.LineRemoved(2, "test.cs", "line2"));
        operations.Add(PatchOperationFactory.IdentifierRenamed("old", "new", 3, "test.cs"));

        var transformations = new PatchTransformationCollection();
        var evidence = new PatchEvidenceCollection();

        // Act
        var graph = PatchSemanticGraphBuilder.BuildGraph(patchModel, operations, transformations, evidence);

        // Assert
        var opNodes = graph.OperationNodes.ToList();
        Assert.Equal(3, opNodes.Count);
    }

    [Fact]
    public void BuildGraph_WithTransformations_CreatesTransformationNodes()
    {
        // Arrange
        var patchModel = new PatchModel { Files = [] };
        var operations = new PatchOperationCollection();

        var transformations = new PatchTransformationCollection();
        transformations.Add(PatchTransformationFactory.ExtractMethod("Method1", "test.cs", operations.All));
        transformations.Add(PatchTransformationFactory.LogicChanged(
            "Logic simplified",
            ["test.cs"],
            operations.All
        ));

        var evidence = new PatchEvidenceCollection();

        // Act
        var graph = PatchSemanticGraphBuilder.BuildGraph(patchModel, operations, transformations, evidence);

        // Assert
        var transformNodes = graph.TransformationNodes.ToList();
        Assert.Equal(2, transformNodes.Count);
    }

    [Fact]
    public void BuildGraph_WithEvidence_CreatesEvidenceNodes()
    {
        // Arrange
        var patchModel = new PatchModel { Files = [] };
        var operations = new PatchOperationCollection();
        var transformations = new PatchTransformationCollection();

        var evidence = new PatchEvidenceCollection();
        evidence.Add(PatchEvidenceFactory.AddedLine(1, "test.cs", "line"));
        evidence.Add(PatchEvidenceFactory.OperatorChange(">", ">=", 2, "test.cs"));

        // Act
        var graph = PatchSemanticGraphBuilder.BuildGraph(patchModel, operations, transformations, evidence);

        // Assert
        var evNodes = graph.EvidenceNodes.ToList();
        Assert.Equal(2, evNodes.Count);
    }

    [Fact]
    public void BuildGraph_CreatesContainmentEdges()
    {
        // Arrange
        var patchModel = new PatchModel
        {
            Files = new[]
            {
                new PatchFile { NewPath = "test.cs", ChangeKind = PatchFileChangeKind.Modified, Hunks = [] }
            }
        };
        var operations = new PatchOperationCollection();
        var transformations = new PatchTransformationCollection();
        var evidence = new PatchEvidenceCollection();

        // Act
        var graph = PatchSemanticGraphBuilder.BuildGraph(patchModel, operations, transformations, evidence);

        // Assert
        var containEdges = graph.EdgesByKind(PatchGraphEdgeKind.Contains).ToList();
        Assert.True(containEdges.Any());
    }

    [Fact]
    public void BuildGraph_CreatesDerivedFromEdges()
    {
        // Arrange
        var patchModel = new PatchModel
        {
            Files = new[]
            {
                new PatchFile { NewPath = "test.cs", ChangeKind = PatchFileChangeKind.Modified, Hunks = [] }
            }
        };
        var operations = new PatchOperationCollection();
        operations.Add(PatchOperationFactory.LineAdded(1, "test.cs", "line"));

        var transformations = new PatchTransformationCollection();
        var evidence = new PatchEvidenceCollection();

        // Act
        var graph = PatchSemanticGraphBuilder.BuildGraph(patchModel, operations, transformations, evidence);

        // Assert
        var derivedEdges = graph.EdgesByKind(PatchGraphEdgeKind.DerivedFrom).ToList();
        Assert.True(derivedEdges.Any());
    }

    [Fact]
    public void GraphNode_MetadataPreserved()
    {
        // Arrange
        var metadata = new Dictionary<string, object>
        {
            { "kind", "LineAdded" },
            { "riskLevel", 0.5 }
        };

        var node = new PatchGraphNode
        {
            Id = "n1",
            Kind = PatchGraphNodeKind.Operation,
            Label = "Operation",
            Metadata = metadata
        };

        // Act & Assert
        Assert.Equal(metadata, node.Metadata);
        Assert.Equal("LineAdded", node.Metadata["kind"]);
        Assert.Equal(0.5, node.Metadata["riskLevel"]);
    }

    [Fact]
    public void GraphEdge_ConfidenceTracked()
    {
        // Arrange & Act
        var edge = new PatchGraphEdge
        {
            Id = "e1",
            SourceNodeId = "n1",
            TargetNodeId = "n2",
            Kind = PatchGraphEdgeKind.EvidenceFor,
            Label = "evidence for",
            Confidence = 0.85
        };

        // Assert
        Assert.Equal(0.85, edge.Confidence);
    }

    [Fact]
    public void PatchNode_ReturnsRootNode()
    {
        // Arrange
        var graph = new PatchSemanticGraph();
        var patchNode = new PatchGraphNode { Id = "root", Kind = PatchGraphNodeKind.Patch };
        var fileNode = new PatchGraphNode { Id = "f1", Kind = PatchGraphNodeKind.File };
        graph.AddNode(patchNode);
        graph.AddNode(fileNode);

        // Act
        var root = graph.PatchNode;

        // Assert
        Assert.NotNull(root);
        Assert.Equal("root", root.Id);
        Assert.Equal(PatchGraphNodeKind.Patch, root.Kind);
    }
}

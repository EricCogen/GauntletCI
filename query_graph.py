import json, sys
import networkx as nx
from networkx.readwrite import json_graph
from pathlib import Path

graph_path = Path.home() / '.gauntletci' / 'graphify' / 'graph.json'
data = json.loads(graph_path.read_text())
G = json_graph.node_link_graph(data, edges='links')

print("=" * 70)
print("GRAPH ANALYSIS: Guard Patterns & Rule Relationships")
print("=" * 70)

# Query 1: Find all rules and their connectivity
print("\n[1] ALL RULES BY CONNECTIVITY (degree)")
print("-" * 70)
rule_nodes = {}
for node in G.nodes():
    label = G.nodes[node].get('label', '')
    if 'GCI00' in label:
        rule_nodes[node] = (label, G.degree(node))

for node, (label, degree) in sorted(rule_nodes.items(), key=lambda x: x[1][1], reverse=True)[:15]:
    print(f"  {label:40} degree={degree}")

# Query 2: Find guard-related patterns
print("\n[2] GUARD PATTERNS IN GRAPH")
print("-" * 70)
guard_patterns = {}
for node in G.nodes():
    label = G.nodes[node].get('label', '').lower()
    if any(x in label for x in ['guard', 'isgenerated', 'istest', 'isinfrastructure', 'nullable', 'nrt']):
        guard_patterns[node] = (G.nodes[node].get('label', ''), G.degree(node))

for node, (label, degree) in sorted(guard_patterns.items(), key=lambda x: x[1][1], reverse=True)[:20]:
    print(f"  {label:50} degree={degree}")

# Query 3: Find edges between rules (cross-rule relationships)
print("\n[3] CROSS-RULE RELATIONSHIPS")
print("-" * 70)
for source in rule_nodes:
    for target in G.neighbors(source):
        if target in rule_nodes:
            edge_data = G.edges[source, target]
            relation = edge_data.get('relation', edge_data.get('type', 'related'))
            print(f"  {rule_nodes[source][0]:25} --{relation}--> {rule_nodes[target][0]}")

# Query 4: God nodes (most connected)
print("\n[4] GOD NODES (Most Connected Concepts)")
print("-" * 70)
node_degrees = [(n, G.degree(n), G.nodes[n].get('label', n)) for n in G.nodes()]
for node, degree, label in sorted(node_degrees, key=lambda x: x[1], reverse=True)[:15]:
    print(f"  {label:50} degree={degree}")

print("\n" + "=" * 70)

import json, sys
import networkx as nx
from networkx.readwrite import json_graph
from pathlib import Path

graph_path = Path.home() / '.gauntletci' / 'graphify' / 'graph.json'
data = json.loads(graph_path.read_text())
G = json_graph.node_link_graph(data, edges='links')

print("=" * 70)
print("GUARD PATTERN ANALYSIS & SHARED PATTERNS")
print("=" * 70)

# Query 1: NRT-related guards
print("\n[1] NRT (Nullable Reference Type) GUARDS")
print("-" * 70)
nrt_nodes = [n for n in G.nodes() if 'nrt' in str(G.nodes[n].get('label', '')).lower() or 'nullable' in str(G.nodes[n].get('label', '')).lower()]
for node in nrt_nodes[:10]:
    label = G.nodes[node].get('label', '')
    degree = G.degree(node)
    neighbors = list(G.neighbors(node))[:5]
    neighbor_labels = [G.nodes[n].get('label', n)[:40] for n in neighbors]
    print(f"  {label}")
    print(f"    → Connected to: {', '.join(neighbor_labels)}")

# Query 2: Find guards used by multiple rules
print("\n[2] SHARED GUARDS ACROSS RULES")
print("-" * 70)
# Get all guard method nodes
guard_methods = [n for n in G.nodes() if '.Is' in str(G.nodes[n].get('label', '')) or '.Has' in str(G.nodes[n].get('label', ''))]

for guard in guard_methods[:15]:
    guard_label = G.nodes[guard].get('label', '')
    # Find rules that use this guard
    rules_using = []
    for neighbor in G.neighbors(guard):
        neighbor_label = G.nodes[neighbor].get('label', '')
        if 'GCI00' in neighbor_label:
            rules_using.append(neighbor_label)
    
    if rules_using and len(rules_using) > 1:
        print(f"  {guard_label}")
        print(f"    Used by: {', '.join(rules_using[:5])}")

# Query 3: Find false positive reduction patterns
print("\n[3] FALSE POSITIVE REDUCTION COMPONENTS")
print("-" * 70)
fp_nodes = [n for n in G.nodes() if any(x in str(G.nodes[n].get('label', '')).lower() 
            for x in ['false positive', 'fp', 'precision', 'corpus', 'labeler', 'enricher'])]
for node in fp_nodes[:15]:
    label = G.nodes[node].get('label', '')
    degree = G.degree(node)
    print(f"  {label:50} (degree={degree})")

# Query 4: Find framework-specific patterns
print("\n[4] FRAMEWORK-SPECIFIC GUARDS")
print("-" * 70)
frameworks = ['grpc', 'http', 'log4net', 'factory', 'di', 'injection', 'di']
for fw in frameworks:
    fw_nodes = [n for n in G.nodes() if fw.lower() in str(G.nodes[n].get('label', '')).lower()]
    if fw_nodes:
        print(f"\n  {fw.upper()}:")
        for node in fw_nodes[:5]:
            label = G.nodes[node].get('label', '')
            degree = G.degree(node)
            print(f"    {label:45} (degree={degree})")

print("\n" + "=" * 70)

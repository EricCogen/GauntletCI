#!/usr/bin/env python3
import json
import os
from pathlib import Path
from datetime import datetime

# Configuration
chunk_file = r"C:\Users\ericc\source\repos\GauntletCI\graphify-out\chunks\chunk_32.txt"
output_file = r"C:\Users\ericc\source\repos\GauntletCI\graphify-out\graphify_chunk_32.json"

def extract_semantic_from_chunk():
    """Extract semantic information from chunk 32 files."""
    
    # Read the chunk file
    files = []
    with open(chunk_file, 'r') as f:
        for line in f:
            line = line.strip()
            if line and not line.startswith('#'):
                # Remove numbering prefix if present
                if line[0].isdigit() and '.' in line[:5]:
                    line = line.split('. ', 1)[1]
                if line:
                    files.append(line)
    
    nodes = []
    edges = []
    node_map = {}  # Track created nodes
    
    # Extract semantic information
    for file_path in files:
        if not file_path.strip():
            continue
            
        file_path = file_path.strip()
        filename = Path(file_path).name
        file_ext = Path(file_path).suffix.lower()
        
        # Determine file type and create node
        if file_ext in ['.svg', '.png', '.jpg', '.jpeg', '.gif']:
            # Visual assets
            asset_name = filename.replace(file_ext, '').replace('-', ' ').title()
            node_id = f"asset_{filename.replace('.', '_').replace('-', '_')}"
            
            # Extract category from path
            if 'logos' in file_path:
                category = 'Logo'
                concepts = [asset_name, 'Integration', 'Visual Asset', 'Branding']
            elif 'og' in file_path:
                category = 'Open Graph Image'
                # Extract what the image is about from filename
                if 'compare' in filename:
                    concepts = ['Comparison', 'Tool Integration', 'Analysis', 'Visual Guide']
                elif 'detect' in filename:
                    concepts = ['Detection', 'Breaking Changes', 'Code Analysis', 'Pull Request']
                elif 'why' in filename:
                    concepts = ['Education', 'Code Quality', 'Bug Detection', 'Testing']
                else:
                    concepts = ['Landing Page', 'Product Guide', 'Visual Asset']
            else:
                category = 'Visual Asset'
                concepts = ['Asset']
            
            node = {
                "id": node_id,
                "label": asset_name,
                "type": "asset",
                "category": category,
                "source_file": file_path,
                "file_extension": file_ext,
                "confidence": 0.95
            }
            nodes.append(node)
            node_map[node_id] = node
            
            # Create nodes for concepts and edges
            for concept in concepts:
                concept_id = f"concept_{concept.lower().replace(' ', '_')}"
                
                # Add concept node if not already added
                if concept_id not in node_map:
                    concept_node = {
                        "id": concept_id,
                        "label": concept,
                        "type": "concept",
                        "confidence": 0.88
                    }
                    nodes.append(concept_node)
                    node_map[concept_id] = concept_node
                
                # Create edge from asset to concept
                edge = {
                    "source": node_id,
                    "target": concept_id,
                    "relationship": "has_concept",
                    "confidence": 0.85
                }
                edges.append(edge)
    
    # Create additional relationships
    # Logos category relationship
    logo_nodes = [n for n in nodes if 'logos' in n.get('source_file', '')]
    if logo_nodes:
        logo_category_id = "category_integrations"
        if logo_category_id not in node_map:
            category_node = {
                "id": logo_category_id,
                "label": "Integrations & Tools",
                "type": "concept",
                "confidence": 0.92
            }
            nodes.append(category_node)
            node_map[logo_category_id] = category_node
        
        for logo_node in logo_nodes:
            edge = {
                "source": logo_node['id'],
                "target": logo_category_id,
                "relationship": "belongs_to_category",
                "confidence": 0.88
            }
            edges.append(edge)
    
    # OG Images category relationship
    og_nodes = [n for n in nodes if 'og' in n.get('source_file', '')]
    if og_nodes:
        og_category_id = "category_marketing"
        if og_category_id not in node_map:
            category_node = {
                "id": og_category_id,
                "label": "Marketing & Documentation",
                "type": "concept",
                "confidence": 0.92
            }
            nodes.append(category_node)
            node_map[og_category_id] = category_node
        
        for og_node in og_nodes:
            edge = {
                "source": og_node['id'],
                "target": og_category_id,
                "relationship": "belongs_to_category",
                "confidence": 0.88
            }
            edges.append(edge)
    
    # Build the JSON output
    output = {
        "chunk": 32,
        "files_processed": len(files),
        "extraction_timestamp": datetime.now().isoformat(),
        "nodes": nodes,
        "edges": edges,
        "hyperedges": [],
        "statistics": {
            "total_nodes": len(nodes),
            "total_edges": len(edges),
            "asset_nodes": len([n for n in nodes if n['type'] == 'asset']),
            "concept_nodes": len([n for n in nodes if n['type'] == 'concept']),
            "average_confidence": round(sum(n.get('confidence', 0.8) for n in nodes) / len(nodes), 3) if nodes else 0,
            "file_categories": {
                "logos": len([f for f in files if 'logos' in f]),
                "og_images": len([f for f in files if 'og' in f])
            }
        }
    }
    
    # Write output file
    os.makedirs(os.path.dirname(output_file), exist_ok=True)
    with open(output_file, 'w') as f:
        json.dump(output, f, indent=2)
    
    return output

if __name__ == "__main__":
    result = extract_semantic_from_chunk()
    print(f"✓ Chunk 32 semantic extraction complete")
    print(f"  - Files processed: {result['files_processed']}")
    print(f"  - Total nodes: {result['statistics']['total_nodes']}")
    print(f"  - Total edges: {result['statistics']['total_edges']}")
    print(f"  - Asset nodes: {result['statistics']['asset_nodes']}")
    print(f"  - Concept nodes: {result['statistics']['concept_nodes']}")
    print(f"  - Average confidence: {result['statistics']['average_confidence']}")
    print(f"  - Output file: {result['statistics'].get('output_file', 'graphify_chunk_32.json')}")
    print(f"\nFile categories:")
    for cat, count in result['statistics']['file_categories'].items():
        print(f"  - {cat}: {count}")

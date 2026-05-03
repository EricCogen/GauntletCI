#!/usr/bin/env python3
"""
Semantic code extraction for graphify chunks.
Processes markdown and code files to extract concepts, patterns, and relationships.
"""

import json
import os
import re
import sys
from collections import defaultdict
from pathlib import Path

def extract_markdown_content(file_path):
    """Extract topics, concepts, and technical terms from markdown files."""
    try:
        with open(file_path, 'r', encoding='utf-8', errors='ignore') as f:
            content = f.read()
    except Exception as e:
        return [], []
    
    concepts = []
    technical_terms = []
    
    # Extract headers as topics
    headers = re.findall(r'^#+\s+(.+)$', content, re.MULTILINE)
    concepts.extend([h.strip() for h in headers if h.strip()])
    
    # Extract code blocks - indicate technical patterns
    code_blocks = re.findall(r'```(?:\w+)?\n(.*?)```', content, re.DOTALL)
    if code_blocks:
        technical_terms.append('code_pattern')
    
    # Extract technical keywords
    keywords = [
        'git', 'github', 'pr', 'pull request', 'branch', 'commit', 'merge',
        'test', 'unit test', 'ci', 'cd', 'pipeline', 'build', 'deploy',
        'docker', 'kubernetes', 'aws', 'azure', 'gcp', 'cloud',
        'api', 'rest', 'http', 'grpc', 'graphql',
        'database', 'sql', 'nosql', 'cache', 'redis',
        'auth', 'security', 'encryption', 'jwt', 'oauth',
        'performance', 'optimization', 'benchmark', 'profiling',
        'error', 'exception', 'logging', 'monitoring', 'observability',
        'migration', 'upgrade', 'compatibility', 'deprecated',
        'feature', 'bug', 'fix', 'issue', 'task', 'epic',
        'dotnet', 'csharp', 'c#', 'aspnet', 'net', 'nuget',
        'sdk', 'library', 'package', 'module', 'component',
        'review', 'feedback', 'comment', 'discussion'
    ]
    
    for keyword in keywords:
        if keyword.lower() in content.lower():
            technical_terms.append(keyword)
    
    # Extract URLs and references
    urls = re.findall(r'https?://[^\s\)]+', content)
    if urls:
        technical_terms.append('external_reference')
    
    return list(set(concepts)), list(set(technical_terms))

def create_nodes_from_file(file_path, idx):
    """Create semantic nodes from a file."""
    nodes = []
    file_name = Path(file_path).name
    parent_dir = Path(file_path).parent.name
    
    # Determine file type
    file_ext = Path(file_path).suffix.lower()
    
    if file_ext == '.md':
        concepts, terms = extract_markdown_content(file_path)
        
        # Create node for the file itself
        nodes.append({
            'id': f'file_{idx}',
            'label': f'{parent_dir}/{file_name}',
            'type': 'document',
            'source_file': str(file_path),
            'confidence': 0.95
        })
        
        # Create nodes for concepts
        for concept in concepts[:5]:  # Limit to top 5
            node_id = f'concept_{idx}_{concepts.index(concept)}'
            nodes.append({
                'id': node_id,
                'label': concept,
                'type': 'concept',
                'source_file': str(file_path),
                'confidence': 0.85
            })
        
        # Create nodes for technical terms
        for term in terms[:5]:  # Limit to top 5
            node_id = f'term_{idx}_{terms.index(term)}'
            nodes.append({
                'id': node_id,
                'label': term,
                'type': 'technical_term',
                'source_file': str(file_path),
                'confidence': 0.80
            })
    
    elif file_ext in ['.png', '.jpg', '.jpeg', '.gif', '.svg']:
        nodes.append({
            'id': f'asset_{idx}',
            'label': file_name,
            'type': 'asset',
            'source_file': str(file_path),
            'confidence': 0.90
        })
    
    else:
        nodes.append({
            'id': f'file_{idx}',
            'label': file_name,
            'type': 'file',
            'source_file': str(file_path),
            'confidence': 0.85
        })
    
    return nodes

def create_edges(nodes):
    """Create relationship edges between nodes."""
    edges = []
    edge_id = 0
    
    # Group nodes by source file
    file_nodes = defaultdict(list)
    for node in nodes:
        if 'source_file' in node:
            file_nodes[node['source_file']].append(node)
    
    # Create edges within same file (document contains concept/term)
    for source_file, file_node_list in file_nodes.items():
        if len(file_node_list) > 1:
            doc_node = next((n for n in file_node_list if n['type'] == 'document'), None)
            if doc_node:
                for node in file_node_list:
                    if node['id'] != doc_node['id']:
                        edges.append({
                            'id': f'edge_{edge_id}',
                            'source': doc_node['id'],
                            'target': node['id'],
                            'relationship': 'contains',
                            'confidence': 0.80
                        })
                        edge_id += 1
    
    # Create edges between related terms
    concepts = [n for n in nodes if n['type'] == 'concept']
    for i, concept1 in enumerate(concepts):
        for concept2 in concepts[i+1:]:
            # Semantic similarity heuristic
            if any(word in concept1['label'].lower() for word in concept2['label'].lower().split()):
                edges.append({
                    'id': f'edge_{edge_id}',
                    'source': concept1['id'],
                    'target': concept2['id'],
                    'relationship': 'related',
                    'confidence': 0.65
                })
                edge_id += 1
    
    return edges

def process_chunk(chunk_num):
    """Process a specific chunk of files."""
    chunk_file = f'C:\\Users\\ericc\\source\\repos\\GauntletCI\\graphify-out\\chunks\\chunk_{chunk_num:02d}.txt'
    
    try:
        with open(chunk_file, 'r', encoding='utf-8') as f:
            file_list = [line.strip() for line in f.readlines() if line.strip()]
    except Exception as e:
        print(f"Error reading chunk file: {e}")
        return None
    
    # Filter to actual files (exclude empty lines)
    files = [f for f in file_list if f and not f.isdigit()]
    
    print(f"Processing chunk {chunk_num}: {len(files)} files")
    
    all_nodes = []
    all_edges = []
    
    # Process each file
    for idx, file_path in enumerate(files):
        if not os.path.exists(file_path):
            print(f"  Skipped (not found): {file_path}")
            continue
        
        try:
            nodes = create_nodes_from_file(file_path, idx)
            all_nodes.extend(nodes)
            print(f"  [{idx+1}/{len(files)}] {Path(file_path).name}: {len(nodes)} nodes")
        except Exception as e:
            print(f"  Error processing {file_path}: {e}")
    
    # Create edges
    all_edges = create_edges(all_nodes)
    
    # Build output
    output = {
        'chunk': chunk_num,
        'files_processed': len(files),
        'nodes': all_nodes,
        'edges': all_edges,
        'hyperedges': [],
        'statistics': {
            'total_nodes': len(all_nodes),
            'total_edges': len(all_edges),
            'confidence_score': round(sum(n.get('confidence', 0.5) for n in all_nodes) / max(len(all_nodes), 1), 2)
        }
    }
    
    return output

def main():
    chunk_num = int(sys.argv[1]) if len(sys.argv) > 1 else 5
    
    result = process_chunk(chunk_num)
    
    if result:
        output_file = f'C:\\Users\\ericc\\source\\repos\\GauntletCI\\graphify-out\\graphify_chunk_{chunk_num:02d}.json'
        
        with open(output_file, 'w', encoding='utf-8') as f:
            json.dump(result, f, indent=2, ensure_ascii=False)
        
        print(f"\nResults saved to: {output_file}")
        print(f"Summary: {result['statistics']['total_nodes']} nodes, {result['statistics']['total_edges']} edges")
        print(f"Confidence: {result['statistics']['confidence_score']}")

if __name__ == '__main__':
    main()

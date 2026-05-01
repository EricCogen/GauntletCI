#!/usr/bin/env python3
"""
Phase 12: Analyze high-FP rules to guide refinement strategy.

For each of the 5 rules with 100% FP rate, query samples and analyze patterns.
"""

import sqlite3
from pathlib import Path
from collections import Counter

HIGH_FP_RULES = {
    "GCI0012": "?",
    "GCI0021": "?",
    "GCI0022": "?", 
    "GCI0029": "?",
    "GCI0039": "?",
}

def connect_db():
    db_path = Path("data/gauntletci-corpus.db")
    if not db_path.exists():
        print(f"ERROR: Corpus not found")
        return None
    return sqlite3.connect(str(db_path))

def analyze_rule(conn, rule_id):
    """Analyze all detections for a rule to understand FP patterns."""
    cursor = conn.cursor()
    
    # Get all detections
    query = """
    SELECT 
        af.fixture_id,
        f.code,
        f.tags,
        f.source_repo,
        ef.should_trigger
    FROM actual_findings af
    JOIN fixtures f ON af.fixture_id = f.id
    LEFT JOIN expected_findings ef ON af.fixture_id = ef.fixture_id AND af.rule_id = ef.rule_id
    WHERE af.rule_id = ? AND af.did_trigger = 1
    ORDER BY af.fixture_id
    """
    
    cursor.execute(query, (rule_id,))
    rows = cursor.fetchall()
    
    if not rows:
        return None
    
    # Analyze patterns
    tp_count = sum(1 for r in rows if r[4] == 1)
    fp_count = len(rows) - tp_count
    
    # Tag patterns in detections
    tag_counter = Counter()
    repo_counter = Counter()
    
    for fixture_id, code, tags, repo, expected in rows:
        if tags:
            for tag in tags.split(';'):
                tag_counter[tag.strip()] += 1
        if repo:
            repo_counter[repo.split('/')[0]] += 1  # Extract org/user
    
    return {
        "total_detections": len(rows),
        "tp": tp_count,
        "fp": fp_count,
        "fp_rate": (fp_count / len(rows) * 100) if rows else 0,
        "common_tags": tag_counter.most_common(3),
        "common_repos": repo_counter.most_common(3),
        "sample_code": rows[0][1][:200] if rows[0][1] else "N/A",
        "sample_fixtures": [r[0] for r in rows[:5]]  # First 5 fixture IDs
    }

def main():
    conn = connect_db()
    if not conn:
        return
    
    print("\n" + "="*100)
    print("PHASE 12: HIGH-FP RULE ANALYSIS")
    print("="*100 + "\n")
    
    for rule_id, rule_name in HIGH_FP_RULES.items():
        print(f"\n{rule_id}: {rule_name}")
        print("-" * 100)
        
        analysis = analyze_rule(conn, rule_id)
        
        if not analysis:
            print("  (No detections found)")
            continue
        
        print(f"  Total detections: {analysis['total_detections']}")
        print(f"  True Positives: {analysis['tp']} ({analysis['tp']/analysis['total_detections']*100:.1f}%)")
        print(f"  False Positives: {analysis['fp']} ({analysis['fp_rate']:.1f}%)")
        
        if analysis['common_tags']:
            print(f"  Common tags: {', '.join([f'{tag}({count})' for tag, count in analysis['common_tags']])}")
        
        if analysis['common_repos']:
            print(f"  Common repos: {', '.join([f'{repo}({count})' for repo, count in analysis['common_repos']])}")
        
        print(f"  Sample code (first 200 chars):")
        print(f"    {analysis['sample_code'][:200].replace(chr(10), ' ')}")
        
        print(f"  Sample fixtures for manual review: {', '.join(analysis['sample_fixtures'])}")
    
    conn.close()

if __name__ == "__main__":
    main()

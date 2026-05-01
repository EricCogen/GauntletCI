#!/usr/bin/env python3
"""
Phase 11: Ground Truth Validation Tool

For each post-label rule, query unmapped detections and sample for manual review.
"""

import sqlite3
import json
import sys
from pathlib import Path
from typing import List, Dict, Tuple
from collections import defaultdict

# Post-label rules: rule_id -> (rule_name, expected_sample_size)
POST_LABEL_RULES = {
    "GCI0043": ("Service Locator Anti-Pattern", 25),
    "GCI0032": ("Rollback/Revert Detection", 25),
    "GCI0042": ("Unsafe Reflection Usage", 25),
    "GCI0044": ("Missing Null Check Patterns", 25),
    "GCI0045": ("Dependency Injection Anti-Pattern", 25),
    "GCI0046": ("Rule 46", 20),
    "GCI0049": ("Rule 49", 20),
    "GCI0047": ("Rule 47", 20),
}

def connect_db():
    """Connect to corpus database."""
    db_path = Path("data/gauntletci-corpus.db")
    if not db_path.exists():
        print(f"ERROR: Corpus database not found at {db_path}")
        sys.exit(1)
    return sqlite3.connect(str(db_path))

def get_unmapped_detections(conn: sqlite3.Connection, rule_id: str) -> List[Dict]:
    """
    Query all detections for a rule that don't have corresponding expected_findings.
    
    Returns: List of dicts with fixture_id, detection_count, fixture_code_snippet
    """
    cursor = conn.cursor()
    
    query = """
    SELECT 
        af.fixture_id,
        COUNT(*) as detection_count,
        MAX(f.code) as code_snippet
    FROM actual_findings af
    JOIN fixtures f ON af.fixture_id = f.id
    LEFT JOIN expected_findings ef ON af.fixture_id = ef.fixture_id AND af.rule_id = ef.rule_id
    WHERE af.rule_id = ? 
      AND af.did_trigger = 1
      AND ef.id IS NULL
    GROUP BY af.fixture_id
    ORDER BY detection_count DESC
    """
    
    cursor.execute(query, (rule_id,))
    rows = cursor.fetchall()
    
    results = []
    for fixture_id, count, code_snippet in rows:
        results.append({
            "fixture_id": fixture_id,
            "detection_count": count,
            "code_snippet": code_snippet or ""
        })
    
    return results

def get_fixture_context(conn: sqlite3.Connection, fixture_id: str) -> Dict:
    """Get full context for a fixture (PR link, title, commit message, etc)."""
    cursor = conn.cursor()
    
    query = """
    SELECT id, source_repo, pr_url, commit_sha, description, code, tags
    FROM fixtures
    WHERE id = ?
    """
    
    cursor.execute(query, (fixture_id,))
    row = cursor.fetchone()
    
    if not row:
        return {}
    
    fixture_id_val, repo, pr_url, commit, desc, code, tags = row
    
    return {
        "fixture_id": fixture_id_val,
        "repo": repo,
        "pr_url": pr_url,
        "commit": commit,
        "description": desc,
        "code": code,
        "tags": tags
    }

def sample_detections(detections: List[Dict], sample_size: int) -> List[Dict]:
    """Sample uniformly from detections, stratified by detection_count."""
    if len(detections) <= sample_size:
        return detections
    
    # Simple stratified sampling: take every nth item
    stride = len(detections) // sample_size
    return detections[::stride][:sample_size]

def report_for_rule(conn: sqlite3.Connection, rule_id: str, rule_name: str, sample_size: int):
    """Generate a spot-check report for a single rule."""
    print(f"\n{'='*80}")
    print(f"RULE: {rule_id} - {rule_name}")
    print(f"{'='*80}\n")
    
    detections = get_unmapped_detections(conn, rule_id)
    
    if not detections:
        print(f"  ✓ All detections already mapped (expected_findings exist)")
        return
    
    print(f"  Total unmapped detections: {len(detections)}")
    print(f"  Sampling {sample_size} fixtures for manual review...\n")
    
    samples = sample_detections(detections, sample_size)
    
    for idx, detection in enumerate(samples, 1):
        fixture_id = detection["fixture_id"]
        context = get_fixture_context(conn, fixture_id)
        
        print(f"\n  [{idx}/{len(samples)}] Fixture: {fixture_id}")
        print(f"       Repo: {context.get('repo', 'unknown')}")
        print(f"       PR:   {context.get('pr_url', 'N/A')}")
        if context.get('tags'):
            print(f"       Tags: {context.get('tags')}")
        if context.get('description'):
            print(f"       Desc: {context.get('description')[:100]}...")
        
        # Show code snippet (first 300 chars)
        code = context.get('code', '')
        if code:
            code_preview = code.replace('\n', '\n       ')[:300]
            print(f"       Code:\n       {code_preview}")
        
        # Prompt for categorization
        print(f"\n       Assessment: [TP] True Positive, [FP] False Positive, [?] Ambiguous, [S] Skip")
        response = input("       Your call (TP/FP/?/S): ").strip().upper()
        
        if response in ["TP", "FP", "?"]:
            print(f"       → Recorded as {response}")
        elif response == "S":
            print(f"       → Skipped")
        else:
            print(f"       → Invalid input, treating as skip")

def main():
    """Run validation workflow."""
    conn = connect_db()
    
    print("Phase 11: Post-Label Rule Validation")
    print("=" * 80)
    print("\nThis tool samples unmapped detections from each post-label rule.")
    print("For each sample, review the PR/commit context and assess if the detection")
    print("is a True Positive (real issue) or False Positive (mistaken detection).\n")
    
    # Offer menu
    print("Options:")
    print("  1. Run all rules (batch mode)")
    print("  2. Pick a specific rule")
    print("  3. Show summary only (no interactive)")
    
    choice = input("\nYour choice (1-3): ").strip()
    
    if choice == "1":
        # Run all
        for rule_id, (rule_name, sample_size) in POST_LABEL_RULES.items():
            report_for_rule(conn, rule_id, rule_name, sample_size)
    
    elif choice == "2":
        # Pick one
        print("\nAvailable rules:")
        for i, (rule_id, (rule_name, _)) in enumerate(POST_LABEL_RULES.items(), 1):
            print(f"  {i}. {rule_id} - {rule_name}")
        
        rule_idx = input("\nSelect (1-8): ").strip()
        try:
            rule_idx = int(rule_idx) - 1
            rule_id = list(POST_LABEL_RULES.keys())[rule_idx]
            rule_name, sample_size = POST_LABEL_RULES[rule_id]
            report_for_rule(conn, rule_id, rule_name, sample_size)
        except (ValueError, IndexError):
            print("Invalid selection")
    
    elif choice == "3":
        # Summary only
        print("\nSummary of unmapped detections per rule:\n")
        for rule_id, (rule_name, _) in POST_LABEL_RULES.items():
            detections = get_unmapped_detections(conn, rule_id)
            print(f"  {rule_id} ({rule_name}): {len(detections)} unmapped fixtures")
    
    else:
        print("Invalid choice")
    
    conn.close()

if __name__ == "__main__":
    main()

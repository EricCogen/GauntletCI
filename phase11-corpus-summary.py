#!/usr/bin/env python3
"""
Phase 11 Corpus Summary: Show metrics for all 8 post-label rules.
"""

import sqlite3
import json
from pathlib import Path
from collections import defaultdict

# Post-label rules identified
POST_LABEL_RULES = {
    "GCI0043": "Service Locator Anti-Pattern",
    "GCI0032": "Rollback/Revert Detection",
    "GCI0042": "Unsafe Reflection Usage",
    "GCI0044": "Missing Null Check Patterns",
    "GCI0045": "Dependency Injection Anti-Pattern",
    "GCI0046": "Unknown Rule 46",
    "GCI0049": "Unknown Rule 49",
    "GCI0047": "Unknown Rule 47",
}

def connect_db():
    db_path = Path("data/gauntletci-corpus.db")
    if not db_path.exists():
        print(f"ERROR: Corpus database not found at {db_path}")
        return None
    return sqlite3.connect(str(db_path))

def query_rule_metrics(conn, rule_id):
    """Get all metrics for a rule."""
    cursor = conn.cursor()
    
    # Total detections
    cursor.execute("SELECT COUNT(*) FROM actual_findings WHERE rule_id = ? AND did_trigger = 1", (rule_id,))
    total_detections = cursor.fetchone()[0]
    
    # Unmapped detections (have actual_findings but no expected_findings)
    cursor.execute("""
        SELECT COUNT(DISTINCT af.fixture_id)
        FROM actual_findings af
        LEFT JOIN expected_findings ef ON af.fixture_id = ef.fixture_id AND af.rule_id = ef.rule_id
        WHERE af.rule_id = ? AND af.did_trigger = 1 AND ef.id IS NULL
    """, (rule_id,))
    unmapped_fixtures = cursor.fetchone()[0]
    
    # Expected findings for this rule
    cursor.execute("SELECT COUNT(*) FROM expected_findings WHERE rule_id = ? AND should_trigger = 1", (rule_id,))
    expected_findings = cursor.fetchone()[0]
    
    # Unique fixtures with detections
    cursor.execute("SELECT COUNT(DISTINCT fixture_id) FROM actual_findings WHERE rule_id = ? AND did_trigger = 1", (rule_id,))
    total_fixtures = cursor.fetchone()[0]
    
    return {
        "total_detections": total_detections,
        "unique_fixtures": total_fixtures,
        "unmapped_fixtures": unmapped_fixtures,
        "expected_findings": expected_findings,
    }

def main():
    conn = connect_db()
    if not conn:
        return
    
    print("\n" + "="*100)
    print("PHASE 11: POST-LABEL RULE VALIDATION BASELINE")
    print("="*100 + "\n")
    
    total_unmapped = 0
    total_detections = 0
    
    results = []
    
    for rule_id, rule_name in sorted(POST_LABEL_RULES.items()):
        metrics = query_rule_metrics(conn, rule_id)
        results.append((rule_id, rule_name, metrics))
        total_unmapped += metrics["unmapped_fixtures"]
        total_detections += metrics["total_detections"]
    
    # Print table
    print(f"{'Rule ID':<10} {'Rule Name':<40} {'Detections':<12} {'Fixtures':<10} {'Unmapped':<10} {'Expected':<10}")
    print("-" * 100)
    
    for rule_id, rule_name, metrics in results:
        print(f"{rule_id:<10} {rule_name:<40} {metrics['total_detections']:<12} "
              f"{metrics['unique_fixtures']:<10} {metrics['unmapped_fixtures']:<10} "
              f"{metrics['expected_findings']:<10}")
    
    print("-" * 100)
    print(f"{'TOTAL':<10} {'':<40} {total_detections:<12} {'':<10} {total_unmapped:<10}")
    
    print(f"\nSUMMARY:")
    print(f"  Total detections from post-label rules: {total_detections}")
    print(f"  Total unmapped fixtures: {total_unmapped}")
    print(f"  Estimated effort: {total_unmapped * 2}-{total_unmapped * 3} minutes (2-3 min per fixture)")
    
    # Show recommended validation order
    print(f"\nRECOMMENDED VALIDATION ORDER (by impact):")
    sorted_by_unmapped = sorted(results, key=lambda x: x[2]["unmapped_fixtures"], reverse=True)
    for idx, (rule_id, rule_name, metrics) in enumerate(sorted_by_unmapped[:5], 1):
        print(f"  {idx}. {rule_id}: {metrics['unmapped_fixtures']} unmapped")
    
    conn.close()

if __name__ == "__main__":
    main()

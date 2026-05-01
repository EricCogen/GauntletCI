#!/usr/bin/env python3
"""
Phase 10C-B: Debug corpus re-labeling
"""
import sqlite3
from pathlib import Path

def main():
    db_path = Path("data/gauntletci-corpus.db")
    conn = sqlite3.connect(str(db_path))
    cursor = conn.cursor()
    
    # Check actual findings for GCI0038
    cursor.execute("""
        SELECT COUNT(*) as count FROM actual_findings 
        WHERE rule_id = 'GCI0038' AND did_trigger = 1
    """)
    actual_0038 = cursor.fetchone()[0]
    
    cursor.execute("""
        SELECT COUNT(*) as count FROM actual_findings 
        WHERE rule_id = 'GCI0010' AND did_trigger = 1
    """)
    actual_0010 = cursor.fetchone()[0]
    
    # Check expected findings for these rules
    cursor.execute("""
        SELECT COUNT(*) as count FROM expected_findings 
        WHERE rule_id = 'GCI0038' AND should_trigger = 1
    """)
    expected_0038 = cursor.fetchone()[0]
    
    cursor.execute("""
        SELECT COUNT(*) as count FROM expected_findings 
        WHERE rule_id = 'GCI0010' AND should_trigger = 1
    """)
    expected_0010 = cursor.fetchone()[0]
    
    # Check overlaps
    cursor.execute("""
        SELECT COUNT(*) as count FROM actual_findings af
        LEFT JOIN expected_findings ef 
          ON ef.fixture_id = af.fixture_id AND ef.rule_id = af.rule_id
        WHERE af.rule_id = 'GCI0038' AND af.did_trigger = 1 AND ef.id IS NULL
    """)
    unmapped_0038 = cursor.fetchone()[0]
    
    cursor.execute("""
        SELECT COUNT(*) as count FROM actual_findings af
        LEFT JOIN expected_findings ef 
          ON ef.fixture_id = af.fixture_id AND ef.rule_id = af.rule_id
        WHERE af.rule_id = 'GCI0010' AND af.did_trigger = 1 AND ef.id IS NULL
    """)
    unmapped_0010 = cursor.fetchone()[0]
    
    print("CORPUS ANALYSIS FOR PHASE 10C-B")
    print("=" * 60)
    print(f"\nGCI0038 (DI Safety):")
    print(f"  Actual detections (did_trigger=1): {actual_0038}")
    print(f"  Expected findings (should_trigger=1): {expected_0038}")
    print(f"  Unmapped (actual but no expected): {unmapped_0038}")
    
    print(f"\nGCI0010 (Hardcoding):")
    print(f"  Actual detections (did_trigger=1): {actual_0010}")
    print(f"  Expected findings (should_trigger=1): {expected_0010}")
    print(f"  Unmapped (actual but no expected): {unmapped_0010}")
    
    print(f"\nTotal to re-label: {unmapped_0038 + unmapped_0010}")
    
    conn.close()

if __name__ == "__main__":
    main()

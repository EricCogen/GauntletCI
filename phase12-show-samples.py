#!/usr/bin/env python3
"""Phase 12: Show sample detections from high-FP rules."""

import sqlite3
from pathlib import Path

def main():
    conn = sqlite3.connect('data/gauntletci-corpus.db')
    cursor = conn.cursor()
    
    for rule_id in ['GCI0012', 'GCI0021', 'GCI0022', 'GCI0029', 'GCI0039']:
        print(f"\n{'='*80}\n{rule_id}\n{'='*80}")
        
        # Get detection counts
        cursor.execute("""
        SELECT COUNT(*) FROM actual_findings 
        WHERE rule_id = ? AND did_trigger = 1
        """, (rule_id,))
        total = cursor.fetchone()[0]
        
        # Get a sample detection
        cursor.execute("""
        SELECT af.fixture_id, af.message, af.evidence_json
        FROM actual_findings af
        WHERE af.rule_id = ? AND af.did_trigger = 1
        LIMIT 1
        """, (rule_id,))
        
        row = cursor.fetchone()
        if row:
            print(f"Total detections: {total}")
            print(f"Sample fixture: {row[0]}")
            if row[1]:
                print(f"Message: {row[1][:150]}")
    
    conn.close()

if __name__ == '__main__':
    main()

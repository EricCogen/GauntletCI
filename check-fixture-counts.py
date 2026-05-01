#!/usr/bin/env python3
import sqlite3
conn = sqlite3.connect('data/gauntletci-corpus.db')
cursor = conn.cursor()

for rule_id in ['GCI0012', 'GCI0021']:
    # Total detections
    cursor.execute("SELECT COUNT(*) FROM actual_findings WHERE rule_id = ? AND did_trigger = 1", (rule_id,))
    total = cursor.fetchone()[0]
    
    # Expected findings
    cursor.execute("SELECT COUNT(*) FROM expected_findings WHERE rule_id = ? AND should_trigger = 1", (rule_id,))
    expected = cursor.fetchone()[0]
    
    # Unmapped (detections without expected)
    cursor.execute("""
    SELECT COUNT(DISTINCT af.fixture_id)
    FROM actual_findings af
    LEFT JOIN expected_findings ef ON af.fixture_id = ef.fixture_id AND af.rule_id = ef.rule_id
    WHERE af.rule_id = ? AND af.did_trigger = 1 AND ef.id IS NULL
    """, (rule_id,))
    unmapped = cursor.fetchone()[0]
    
    print(f"{rule_id}: detections={total}, expected={expected}, unmapped_fixtures={unmapped}")

conn.close()

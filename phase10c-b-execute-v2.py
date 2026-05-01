#!/usr/bin/env python3
"""
Phase 10C-B: Execute selective re-labeling SQL (without INSERT OR IGNORE)
"""
import sqlite3
from pathlib import Path
import uuid

def main():
    db_path = Path("data/gauntletci-corpus.db")
    conn = sqlite3.connect(str(db_path))
    cursor = conn.cursor()
    
    try:
        # GCI0038
        print("Processing GCI0038...")
        cursor.execute("""
            SELECT DISTINCT af.fixture_id
            FROM actual_findings af
            WHERE af.rule_id = 'GCI0038'
              AND af.did_trigger = 1
              AND NOT EXISTS (
                SELECT 1 FROM expected_findings ef
                WHERE ef.fixture_id = af.fixture_id AND ef.rule_id = af.rule_id
              )
        """)
        
        gci0038_fixtures = [row[0] for row in cursor.fetchall()]
        print(f"  Found {len(gci0038_fixtures)} unmapped fixtures")
        
        for fixture_id in gci0038_fixtures:
            cursor.execute("""
                INSERT INTO expected_findings (id, fixture_id, rule_id, should_trigger, label_source, reason)
                VALUES (?, ?, ?, ?, ?, ?)
            """, (
                f"ef_phase10cb_{fixture_id}_GCI0038_{uuid.uuid4().hex[:8]}",
                fixture_id,
                'GCI0038',
                1,
                'phase10c-b-relabel',
                'Rule Stricter: DI changes missed in original corpus'
            ))
        
        # GCI0010
        print("Processing GCI0010...")
        cursor.execute("""
            SELECT DISTINCT af.fixture_id
            FROM actual_findings af
            WHERE af.rule_id = 'GCI0010'
              AND af.did_trigger = 1
              AND NOT EXISTS (
                SELECT 1 FROM expected_findings ef
                WHERE ef.fixture_id = af.fixture_id AND ef.rule_id = af.rule_id
              )
        """)
        
        gci0010_fixtures = [row[0] for row in cursor.fetchall()]
        print(f"  Found {len(gci0010_fixtures)} unmapped fixtures")
        
        for fixture_id in gci0010_fixtures:
            cursor.execute("""
                INSERT INTO expected_findings (id, fixture_id, rule_id, should_trigger, label_source, reason)
                VALUES (?, ?, ?, ?, ?, ?)
            """, (
                f"ef_phase10cb_{fixture_id}_GCI0010_{uuid.uuid4().hex[:8]}",
                fixture_id,
                'GCI0010',
                1,
                'phase10c-b-relabel',
                'Rule Stricter: Config changes missed in original corpus'
            ))
        
        conn.commit()
        
        total = len(gci0038_fixtures) + len(gci0010_fixtures)
        print(f"\n✅ Re-labeling complete!")
        print(f"Total new expected_findings: {total}")
        print(f"  GCI0038: {len(gci0038_fixtures)}")
        print(f"  GCI0010: {len(gci0010_fixtures)}")
        
        return 0
    
    except Exception as e:
        print(f"ERROR: {e}")
        import traceback
        traceback.print_exc()
        conn.rollback()
        return 1
    
    finally:
        conn.close()

if __name__ == "__main__":
    exit(main())

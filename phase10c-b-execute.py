#!/usr/bin/env python3
"""
Phase 10C-B: Execute selective re-labeling SQL
"""
import sqlite3
from pathlib import Path

def main():
    db_path = Path("data/gauntletci-corpus.db")
    sql_path = Path("phase10c-b-relabel-statements.sql")
    
    if not db_path.exists():
        print(f"ERROR: Database not found: {db_path}")
        return 1
    
    if not sql_path.exists():
        print(f"ERROR: SQL file not found: {sql_path}")
        return 1
    
    # Read SQL
    with open(sql_path) as f:
        sql = f.read()
    
    # Execute
    conn = sqlite3.connect(str(db_path))
    cursor = conn.cursor()
    
    try:
        # Split into statements and execute
        statements = [s.strip() for s in sql.split(';') if s.strip() and not s.strip().startswith('--')]
        
        for stmt in statements:
            if stmt and not stmt.startswith('--'):
                print(f"Executing: {stmt[:80]}...")
                cursor.execute(stmt)
        
        conn.commit()
        
        # Verification
        cursor.execute("SELECT COUNT(*) as count FROM expected_findings WHERE label_source = 'phase10c-b-relabel';")
        new_count = cursor.fetchone()[0]
        
        cursor.execute("SELECT rule_id, COUNT(*) as count FROM expected_findings WHERE label_source = 'phase10c-b-relabel' GROUP BY rule_id;")
        breakdown = cursor.fetchall()
        
        print(f"\n✅ Re-labeling complete!")
        print(f"Total new expected_findings: {new_count}")
        print(f"\nBreakdown by rule:")
        for rule_id, count in breakdown:
            print(f"  {rule_id}: {count} samples")
        
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

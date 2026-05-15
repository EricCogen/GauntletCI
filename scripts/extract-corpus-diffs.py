#!/usr/bin/env python3
"""Extract diffs from corpus database and cache them to disk"""
import sqlite3
from pathlib import Path

db_path = Path(r'C:\Users\ericc\.gauntletci\corpus.db')
fixtures_base = Path(r'C:\Users\ericc\source\repos\GauntletCI\data\fixtures\discovery')

if not db_path.exists():
    print(f"❌ Database not found: {db_path}")
    exit(1)

if not fixtures_base.exists():
    fixtures_base.mkdir(parents=True, exist_ok=True)
    print(f"✅ Created fixtures directory: {fixtures_base}")

db = sqlite3.connect(str(db_path))
c = db.cursor()

# Get all fixtures with their diffs
c.execute('''
    SELECT fixture_id, diff_content 
    FROM fixtures 
    WHERE diff_content IS NOT NULL
    ORDER BY fixture_id
''')

results = c.fetchall()
total = len(results)
written = 0
skipped = 0

print(f"📦 Processing {total} fixtures with diffs...")
print()

for fixture_id, diff_content in results:
    fixture_dir = fixtures_base / fixture_id
    diff_path = fixture_dir / "diff.patch"
    
    # Skip if already exists
    if diff_path.exists():
        skipped += 1
        continue
    
    # Create directory
    fixture_dir.mkdir(parents=True, exist_ok=True)
    
    # Write diff
    try:
        diff_path.write_text(diff_content, encoding='utf-8')
        written += 1
        if written % 50 == 0:
            print(f"  [{written}/{total}] {fixture_id}")
    except Exception as e:
        print(f"  ❌ Error writing {fixture_id}: {e}")

db.close()

print()
print(f"✅ Done!")
print(f"   Written: {written}")
print(f"   Skipped: {skipped}")
print(f"   Total: {total}")

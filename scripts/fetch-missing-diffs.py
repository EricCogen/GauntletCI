#!/usr/bin/env python3
"""Fetch missing PR diffs from GitHub and cache them"""
import sqlite3
import urllib.request
import urllib.error
from pathlib import Path
import time
import sys

db_path = Path(r'C:\Users\ericc\.gauntletci\corpus.db')
fixtures_base = Path(r'C:\Users\ericc\source\repos\GauntletCI\data\fixtures\discovery')

db = sqlite3.connect(str(db_path))
c = db.cursor()

# Get all fixtures
c.execute('SELECT fixture_id, repo, pr_number FROM fixtures ORDER BY fixture_id')
all_fixtures = c.fetchall()

# Find which ones are missing diffs
missing = []
for fixture_id, repo, pr_number in all_fixtures:
    diff_path = fixtures_base / fixture_id / "diff.patch"
    if not diff_path.exists():
        missing.append((fixture_id, repo, pr_number))

print(f"📊 Found {len(missing)} fixtures without cached diffs")
print()

if len(missing) == 0:
    print("✅ All diffs already cached!")
    sys.exit(0)

print("🔄 Fetching diffs from GitHub...")
print("   (This may take a few minutes due to rate limiting)")
print()

fetched = 0
failed = 0

for i, (fixture_id, repo, pr_number) in enumerate(missing, 1):
    # GitHub API: get PR diff
    url = f"https://api.github.com/repos/{repo}/pulls/{pr_number}"
    
    try:
        # Get diff using GitHub's standard diff endpoint
        diff_url = f"https://patch-diff.githubusercontent.com/raw/{repo}/pull/{pr_number}.patch"
        
        try:
            with urllib.request.urlopen(diff_url, timeout=10) as resp:
                content = resp.read()
        except urllib.error.HTTPError as e:
            if e.code == 404:
                # PR not found or private
                failed += 1
                if failed <= 5:
                    print(f"  [{i:3d}/{len(missing)}] ❌ {fixture_id} - HTTP 404")
            else:
                failed += 1
                if failed <= 5:
                    print(f"  [{i:3d}/{len(missing)}] ❌ {fixture_id} - HTTP {e.code}")
            continue
        
        # Create fixture directory
        fixture_dir = fixtures_base / fixture_id
        fixture_dir.mkdir(parents=True, exist_ok=True)
        
        # Write diff
        diff_path = fixture_dir / "diff.patch"
        diff_path.write_bytes(content)
        
        fetched += 1
        if fetched % 10 == 0 or fetched == 1:
            print(f"  [{i:3d}/{len(missing)}] ✅ {fixture_id} ({len(content)} bytes)")
    
    except Exception as e:
        failed += 1
        if failed <= 5:
            print(f"  [{i:3d}/{len(missing)}] ❌ {fixture_id} - {type(e).__name__}: {str(e)[:50]}")
    
    # Rate limiting - be respectful to GitHub
    if i % 5 == 0:
        time.sleep(1)

db.close()

print()
print(f"✅ Complete!")
print(f"   Fetched: {fetched}")
print(f"   Failed: {failed}")
print(f"   Total attempted: {len(missing)}")

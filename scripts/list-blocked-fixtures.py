#!/usr/bin/env python3
"""Get list of blocked/missing fixtures from corpus audit"""
import sqlite3
from pathlib import Path

db_path = Path(r'C:\Users\ericc\.gauntletci\corpus.db')
fixtures_base = Path(r'C:\Users\ericc\source\repos\GauntletCI\data\fixtures\discovery')

db = sqlite3.connect(str(db_path))
c = db.cursor()

# Get all fixtures that DON'T have cached diffs
c.execute('SELECT fixture_id, repo, pr_number, pr_size_bucket FROM fixtures ORDER BY fixture_id')
all_fixtures = c.fetchall()

# Check which ones are missing
blocked = []
for fixture_id, repo, pr_number, size_bucket in all_fixtures:
    diff_path = fixtures_base / fixture_id / "diff.patch"
    if not diff_path.exists():
        blocked.append({
            'fixture_id': fixture_id,
            'repo': repo,
            'pr_number': pr_number,
            'size': size_bucket
        })

db.close()

print(f"📊 BLOCKED FIXTURES (Missing Diffs)")
print(f"{'='*80}")
print(f"Total Blocked: {len(blocked)}")
print(f"{'='*80}\n")

# Group by repo
from collections import defaultdict
by_repo = defaultdict(list)
for item in blocked:
    by_repo[item['repo']].append(item)

# Display
for repo in sorted(by_repo.keys()):
    items = by_repo[repo]
    print(f"{repo}: {len(items)} blocked")
    for item in sorted(items, key=lambda x: x['pr_number']):
        print(f"  - PR #{item['pr_number']} ({item['size']})")
    print()

print(f"\n{'='*80}")
print(f"Summary by Size:")
print(f"{'='*80}")
by_size = defaultdict(int)
for item in blocked:
    by_size[item['size']] += 1

for size in ['Huge', 'Large', 'Medium', 'Small', 'Tiny']:
    count = by_size.get(size, 0)
    pct = (count / len(blocked) * 100) if blocked else 0
    print(f"{size:10} {count:3} ({pct:5.1f}%)")

print(f"{'TOTAL':10} {len(blocked):3}")

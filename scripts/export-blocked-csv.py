#!/usr/bin/env python3
"""Export blocked fixtures to CSV"""
import sqlite3
from pathlib import Path
import csv

db_path = Path(r'C:\Users\ericc\.gauntletci\corpus.db')
fixtures_base = Path(r'C:\Users\ericc\source\repos\GauntletCI\data\fixtures\discovery')

db = sqlite3.connect(str(db_path))
c = db.cursor()

# Get all fixtures with details
c.execute('''
    SELECT fixture_id, repo, pr_number, pr_size_bucket, 
           has_tests_changed, has_review_comments, source, created_at_utc
    FROM fixtures 
    ORDER BY repo, pr_number
''')

all_fixtures = c.fetchall()

# Filter blocked ones
blocked = []
for fixture_id, repo, pr_number, size_bucket, has_tests, has_comments, source, created_at in all_fixtures:
    diff_path = fixtures_base / fixture_id / "diff.patch"
    if not diff_path.exists():
        blocked.append({
            'fixture_id': fixture_id,
            'repo': repo,
            'pr_number': pr_number,
            'size': size_bucket,
            'has_tests': 'Yes' if has_tests else 'No',
            'has_comments': 'Yes' if has_comments else 'No',
            'source': source,
            'created_at': created_at,
            'github_url': f'https://github.com/{repo}/pull/{pr_number}'
        })

db.close()

# Write CSV
csv_path = Path(r'C:\Users\ericc\source\repos\GauntletCI\BLOCKED_FIXTURES.csv')
with open(csv_path, 'w', newline='', encoding='utf-8') as f:
    writer = csv.DictWriter(f, fieldnames=[
        'fixture_id', 'repo', 'pr_number', 'size', 'has_tests', 'has_comments', 
        'source', 'created_at', 'github_url'
    ])
    writer.writeheader()
    writer.writerows(blocked)

print(f"✅ Exported {len(blocked)} blocked fixtures to BLOCKED_FIXTURES.csv")
print(f"Location: {csv_path}")

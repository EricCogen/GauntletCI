import sqlite3
import os

db_path = os.path.expanduser('~/.gauntletci/corpus.db')
conn = sqlite3.connect(db_path)
c = conn.cursor()

# Get all fixtures with paths
c.execute('SELECT fixture_id, path FROM fixtures WHERE tier = ? AND path IS NOT NULL ORDER BY fixture_id', ('Discovery',))
all_fixtures = c.fetchall()

# Check which have diff.patch
with_diff = []
without_diff = []

for fixture_id, rel_path in all_fixtures:
    diff_path = os.path.join(rel_path, 'diff.patch')
    if os.path.exists(diff_path):
        with_diff.append(fixture_id)
    else:
        without_diff.append((fixture_id, rel_path))

print(f"Total fixtures in DB: {len(all_fixtures)}")
print(f"With diff.patch: {len(with_diff)}")
print(f"Missing diff.patch: {len(without_diff)}")
print()

if without_diff:
    print("Sample of missing fixtures (first 20):")
    for fixture_id, rel_path in without_diff[:20]:
        # Extract repo and PR from fixture_id (format: repo_name_pr_number)
        parts = fixture_id.rsplit('_', 1)
        if len(parts) == 2:
            repo, pr_num = parts
            print(f"  {fixture_id}: {repo}#{pr_num} (path: {rel_path})")
        else:
            print(f"  {fixture_id}: (path: {rel_path})")

conn.close()

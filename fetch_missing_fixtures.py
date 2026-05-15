#!/usr/bin/env python3
"""
Fetch missing diff.patch files for corpus fixtures from GitHub.
Uses 'gh' CLI tool to download PR diffs.
Runs as a background task with error resilience.
"""

import sqlite3
import os
import subprocess
import json
import time
from datetime import datetime

db_path = os.path.expanduser('~/.gauntletci/corpus.db')
conn = sqlite3.connect(db_path)
c = conn.cursor()

# Get missing fixtures
c.execute('SELECT fixture_id, path FROM fixtures WHERE tier = ? AND path IS NOT NULL ORDER BY fixture_id', ('Discovery',))
all_fixtures = c.fetchall()

missing_fixtures = []
for fixture_id, rel_path in all_fixtures:
    diff_path = os.path.join(rel_path, 'diff.patch')
    if not os.path.exists(diff_path):
        missing_fixtures.append((fixture_id, rel_path))

print(f"Found {len(missing_fixtures)} missing fixtures")
print(f"Start time: {datetime.now().isoformat()}")
print()

# Parse fixture_id format: owner_repo_prNNNN
stats = {
    'fetched': 0,
    'failed': 0,
    'skipped': 0,
    'errors': []
}

for idx, (fixture_id, rel_path) in enumerate(missing_fixtures, 1):
    try:
        # Parse fixture_id
        # Format examples: aws_aws-sdk-net_pr4331, domaindrivendev_swashbuckle.aspnetcore_pr2414
        parts = fixture_id.split('_')
        if len(parts) < 3:
            print(f"[{idx:4d}/{len(missing_fixtures)}] SKIP {fixture_id}: invalid format")
            stats['skipped'] += 1
            continue
        
        # Last part is prNNNN, everything before is owner/repo
        pr_part = parts[-1]
        if not pr_part.startswith('pr'):
            print(f"[{idx:4d}/{len(missing_fixtures)}] SKIP {fixture_id}: no pr number")
            stats['skipped'] += 1
            continue
        
        pr_number = pr_part[2:]  # Remove 'pr' prefix
        repo_part = '_'.join(parts[:-1])  # Everything before prNNNN
        
        # Reconstruct owner/repo format (replace underscores with slashes)
        # This is tricky because repo names can have underscores
        # Try splitting at first underscore as owner/repo boundary
        parts = repo_part.split('_', 1)
        if len(parts) != 2:
            print(f"[{idx:4d}/{len(missing_fixtures)}] SKIP {fixture_id}: can't parse owner/repo")
            stats['skipped'] += 1
            continue
        
        owner, repo = parts
        # Handle repo names with underscores (e.g., aws-sdk-net, swashbuckle.aspnetcore)
        # These stay as-is
        
        # Create fixture directory if needed
        os.makedirs(rel_path, exist_ok=True)
        diff_path = os.path.join(rel_path, 'diff.patch')
        
        # Check again in case it was created since we started
        if os.path.exists(diff_path):
            print(f"[{idx:4d}/{len(missing_fixtures)}] EXISTS {fixture_id}")
            stats['skipped'] += 1
            continue
        
        # Fetch using gh CLI
        # gh pr diff <PR> [--repo owner/repo]
        try:
            result = subprocess.run(
                ['gh', 'pr', 'diff', pr_number, '--repo', f'{owner}/{repo}'],
                capture_output=True,
                text=True,
                timeout=30
            )
            
            if result.returncode == 0 and result.stdout:
                # Save diff
                with open(diff_path, 'w') as f:
                    f.write(result.stdout)
                print(f"[{idx:4d}/{len(missing_fixtures)}] FETCH {fixture_id}: {len(result.stdout)} bytes")
                stats['fetched'] += 1
            else:
                error_msg = result.stderr or f"exit code {result.returncode}"
                print(f"[{idx:4d}/{len(missing_fixtures)}] FAIL {fixture_id}: {error_msg[:60]}")
                stats['failed'] += 1
                stats['errors'].append({'fixture_id': fixture_id, 'error': error_msg[:100]})
        
        except subprocess.TimeoutExpired:
            print(f"[{idx:4d}/{len(missing_fixtures)}] TIMEOUT {fixture_id}")
            stats['failed'] += 1
            stats['errors'].append({'fixture_id': fixture_id, 'error': 'timeout'})
        except FileNotFoundError:
            print(f"FATAL: 'gh' CLI not found. Install GitHub CLI from https://cli.github.com")
            break
        
        # Be respectful to GitHub API - rate limit is 5000 req/hr = ~1.4 req/sec
        # Add 1 second delay between requests
        if idx < len(missing_fixtures):
            time.sleep(1)
    
    except Exception as e:
        print(f"[{idx:4d}/{len(missing_fixtures)}] ERROR {fixture_id}: {str(e)[:60]}")
        stats['failed'] += 1
        stats['errors'].append({'fixture_id': fixture_id, 'error': str(e)[:100]})

# Summary
print()
print("=" * 70)
print("Fetch Summary")
print("=" * 70)
print(f"Fetched:   {stats['fetched']}")
print(f"Failed:    {stats['failed']}")
print(f"Skipped:   {stats['skipped']}")
print(f"Total:     {len(missing_fixtures)}")
print(f"End time:  {datetime.now().isoformat()}")
print()

if stats['errors']:
    print("Failed fixtures (first 10):")
    for entry in stats['errors'][:10]:
        print(f"  {entry['fixture_id']}: {entry['error']}")

conn.close()

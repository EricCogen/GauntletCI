#!/usr/bin/env python3
import sqlite3, os, subprocess, json, sys, time
from datetime import datetime

db_path = os.path.expanduser('~/.gauntletci/corpus.db')
db = sqlite3.connect(db_path)
c = db.cursor()

# Get all cached fixtures
c.execute('SELECT fixture_id, path FROM fixtures WHERE tier = ? AND path IS NOT NULL ORDER BY fixture_id', ('Discovery',))
fixtures = c.fetchall()

total = len(fixtures)
findings_by_fixture = {}
all_findings = 0
cli_path = r'src/GauntletCI.Cli/bin/Release/net8.0/gauntletci.exe'

print(f"Starting corpus audit: {total} fixtures, 55 rules enabled")
print(f"Start time: {datetime.now().isoformat()}")
print("")

start_time = time.time()

for idx, (fixture_id, rel_path) in enumerate(fixtures, 1):
    diff_path = os.path.join(rel_path, 'diff.patch')
    
    if not os.path.exists(diff_path):
        continue
    
    try:
        with open(diff_path, 'r', encoding='utf-8', errors='ignore') as f:
            diff_content = f.read()
        
        result = subprocess.run(
            [cli_path, 'analyze', '--stdin'],
            input=diff_content,
            capture_output=True,
            text=True,
            timeout=30
        )
        
        # Parse findings
        findings = 0
        for line in result.stdout.split('\n'):
            if 'Findings:' in line:
                try:
                    findings = int(line.split('Findings:')[1].strip().split()[0])
                except:
                    pass
        
        if findings > 0:
            findings_by_fixture[fixture_id] = findings
            all_findings += findings
            if idx % 20 == 0 or findings > 0:
                print(f"[{idx:3d}/{total}] {fixture_id}: {findings} findings")
        elif idx % 50 == 0:
            print(f"[{idx:3d}/{total}] OK")
    
    except subprocess.TimeoutExpired:
        print(f"[{idx:3d}/{total}] {fixture_id}: TIMEOUT")
    except Exception as e:
        print(f"[{idx:3d}/{total}] {fixture_id}: ERROR - {str(e)[:50]}")

elapsed = time.time() - start_time

print("")
print("=" * 70)
print(f"Audit Complete")
print("=" * 70)
print(f"Fixtures audited:        {total}")
print(f"Fixtures with findings:  {len(findings_by_fixture)}")
print(f"Total findings:          {all_findings}")
print(f"Time elapsed:            {elapsed:.1f}s")
print(f"Avg per fixture:         {elapsed/total:.2f}s")
print(f"End time:                {datetime.now().isoformat()}")

if findings_by_fixture:
    print("")
    print("Top violations:")
    for fid in sorted(findings_by_fixture.keys(), key=lambda x: findings_by_fixture[x], reverse=True)[:10]:
        print(f"  {fid}: {findings_by_fixture[fid]}")

db.close()

#!/usr/bin/env python
"""Inspect corpus database schema for Phase 10A planning."""
import sqlite3
import json

conn = sqlite3.connect('data/gauntletci-corpus.db')
cur = conn.cursor()

# Get all tables
cur.execute("SELECT name FROM sqlite_master WHERE type='table' ORDER BY name")
tables = cur.fetchall()

print('CORPUS DATABASE SCHEMA')
print('=' * 70)
print()

for table in tables:
    table_name = table[0]
    print(f'TABLE: {table_name}')
    print('-' * 70)
    
    # Get columns
    cur.execute(f'PRAGMA table_info({table_name})')
    cols = cur.fetchall()
    for col in cols:
        col_id, col_name, col_type, notnull, default, pk = col
        pk_marker = ' [PRIMARY KEY]' if pk else ''
        nn_marker = ' [NOT NULL]' if notnull else ''
        print(f'  {col_name:30s} {col_type:15s}{pk_marker}{nn_marker}')
    
    # Get row count
    cur.execute(f'SELECT COUNT(*) FROM {table_name}')
    row_count = cur.fetchone()[0]
    print(f'\n  Row count: {row_count:,}')
    print()

# Get sample data from each table
print()
print('SAMPLE DATA')
print('=' * 70)
print()

for table in tables:
    table_name = table[0]
    print(f'\n{table_name} (first 3 rows):')
    print('-' * 70)
    cur.execute(f'SELECT * FROM {table_name} LIMIT 3')
    rows = cur.fetchall()
    
    # Get column names
    cur.execute(f'PRAGMA table_info({table_name})')
    cols = [col[1] for col in cur.fetchall()]
    
    for row in rows:
        row_dict = dict(zip(cols, row))
        print(json.dumps(row_dict, indent=2, default=str))

conn.close()

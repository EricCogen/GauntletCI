#!/usr/bin/env python3
"""Check corpus database schema"""
import sqlite3

db = sqlite3.connect(r'C:\Users\ericc\.gauntletci\corpus.db')
c = db.cursor()

# List all tables
c.execute("SELECT name FROM sqlite_master WHERE type='table'")
tables = c.fetchall()

print("📊 Database Tables:")
for (table,) in sorted(tables):
    c.execute(f"PRAGMA table_info({table})")
    columns = c.fetchall()
    print(f"\n  {table}:")
    for col_id, name, type_, notnull, default, pk in columns:
        print(f"    - {name} ({type_})")

db.close()

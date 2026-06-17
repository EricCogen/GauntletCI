#!/usr/bin/env python3
"""Apply data/corpus-label-overrides.json to expected_findings in agent corpus DB."""
from __future__ import annotations

import argparse
import json
import sqlite3
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
DEFAULT_DB = Path.home() / ".gauntletci" / "corpus.db"
DEFAULT_OVERRIDES = REPO / "data" / "corpus-label-overrides.json"


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--db", default=str(DEFAULT_DB))
    parser.add_argument("--overrides", default=str(DEFAULT_OVERRIDES))
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    db_path = Path(args.db)
    overrides_path = Path(args.overrides)
    if not overrides_path.exists():
        raise SystemExit(f"Missing overrides file: {overrides_path}")
    if not db_path.exists():
        raise SystemExit(f"Missing corpus DB: {db_path}")

    doc = json.loads(overrides_path.read_text(encoding="utf-8"))
    rows = doc.get("overrides") or []
    if not rows:
        print("No overrides to apply")
        return

    con = sqlite3.connect(db_path)
    updated = 0
    missing = 0
    try:
        for row in rows:
            fixture_id = row["fixture_id"]
            rule_id = row["rule_id"]
            inconclusive = 1 if row.get("is_inconclusive") else 0
            reason = row.get("reason")
            cur = con.execute(
                """
                UPDATE expected_findings
                SET is_inconclusive = ?, reason = COALESCE(?, reason)
                WHERE fixture_id = ? AND rule_id = ?
                """,
                (inconclusive, reason, fixture_id, rule_id),
            )
            if cur.rowcount == 0:
                missing += 1
                print(f"WARN no row: {fixture_id} / {rule_id}")
            else:
                updated += cur.rowcount
                print(f"OK {fixture_id} / {rule_id} is_inconclusive={inconclusive}")

        if args.dry_run:
            con.rollback()
            print(f"Dry run: would update {updated} row(s); missing {missing}")
        else:
            con.commit()
            print(f"Applied {updated} override(s); missing {missing}")
    finally:
        con.close()


if __name__ == "__main__":
    main()

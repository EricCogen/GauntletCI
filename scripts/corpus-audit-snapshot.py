#!/usr/bin/env python3
"""Insert a labeled TP/FP/FN audit snapshot into the agent corpus DB."""
from __future__ import annotations

import argparse
import sqlite3
import sys
from datetime import datetime, timezone
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from corpus_db_read import compute_labeled_rule_metrics, ensure_read_indexes


def compute_rule_rows(cur: sqlite3.Cursor) -> list[dict]:
    """Classify expected_findings vs latest completed run (EvaluationClassifier parity)."""
    usefulness: dict[str, float | None] = {}
    for rule_id, avg in cur.execute(
        "SELECT rule_id, AVG(usefulness) FROM evaluations GROUP BY rule_id"
    ).fetchall():
        usefulness[rule_id] = round(float(avg), 3) if avg is not None else None

    result: list[dict] = []
    for rule_id, metrics in compute_labeled_rule_metrics(cur).items():
        tp = metrics["tp"]
        fp = metrics["fp"]
        fn = metrics["fn"]
        result.append(
            {
                "rule_id": rule_id,
                "labeled": metrics["labeled"],
                "tp": tp,
                "fp": fp,
                "fn": fn,
                "precision_score": metrics.get("labeled_precision"),
                "recall_score": metrics.get("labeled_recall"),
                "usefulness_score": usefulness.get(rule_id),
            }
        )
    result.sort(key=lambda row: row["rule_id"])
    return result


def insert_snapshot(con: sqlite3.Connection, rows: list[dict], notes: str | None) -> int:
    cur = con.cursor()
    cur.execute(
        """
        INSERT INTO audit_snapshots (snapped_at_utc, rules_snapped, notes)
        VALUES (?, ?, ?)
        """,
        (
            datetime.now(timezone.utc).strftime("%Y-%m-%d %H:%M:%S"),
            len(rows),
            notes,
        ),
    )
    snapshot_id = int(cur.lastrowid)
    for row in rows:
        cur.execute(
            """
            INSERT INTO audit_snapshot_rows (
                snapshot_id, rule_id, labeled, tp, fp, fn,
                precision_score, recall_score, usefulness_score
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
            """,
            (
                snapshot_id,
                row["rule_id"],
                row["labeled"],
                row["tp"],
                row["fp"],
                row["fn"],
                row["precision_score"],
                row["recall_score"],
                row["usefulness_score"],
            ),
        )
    con.commit()
    return snapshot_id


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--db",
        default=str(Path.home() / ".gauntletci" / "corpus.db"),
    )
    parser.add_argument(
        "--notes",
        default="post run-all labeled metrics",
        help="Optional snapshot note stored in audit_snapshots.notes",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Print top rows only; do not insert",
    )
    args = parser.parse_args()

    con = sqlite3.connect(args.db)
    ensure_read_indexes(con)
    con.row_factory = sqlite3.Row
    rows = compute_rule_rows(con.cursor())

    top = sorted(
        [r for r in rows if r["tp"] or r["fp"] or r["fn"]],
        key=lambda r: (r["fp"], r["fn"], -r["tp"]),
        reverse=True,
    )[:8]

    print(f"rules_with_labels={len(rows)}")
    for r in top:
        print(
            f"{r['rule_id']:10} labeled={r['labeled']:3} "
            f"tp={r['tp']:3} fp={r['fp']:3} fn={r['fn']:3} "
            f"prec={r['precision_score']}"
        )

    if args.dry_run:
        con.close()
        return

    snapshot_id = insert_snapshot(con, rows, args.notes)
    print(f"snapshot_id={snapshot_id} rows={len(rows)}")
    con.close()


if __name__ == "__main__":
    main()

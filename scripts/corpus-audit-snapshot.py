#!/usr/bin/env python3
"""Insert a labeled TP/FP/FN audit snapshot into the agent corpus DB."""
from __future__ import annotations

import argparse
import sqlite3
from datetime import datetime, timezone
from pathlib import Path


def compute_rule_rows(cur: sqlite3.Cursor) -> list[dict]:
    """Classify expected_findings vs latest completed run (EvaluationClassifier parity)."""
    rows = cur.execute(
        """
        WITH latest AS (
            SELECT fixture_id, MAX(id) AS run_id
            FROM rule_runs
            WHERE UPPER(status) = 'COMPLETED'
            GROUP BY fixture_id
        ),
        pairs AS (
            SELECT ef.rule_id,
                   ef.fixture_id,
                   ef.should_trigger,
                   COALESCE(MAX(af.did_trigger), 0) AS did_trigger
            FROM expected_findings ef
            INNER JOIN latest lr ON lr.fixture_id = ef.fixture_id
            LEFT JOIN actual_findings af
              ON af.fixture_id = ef.fixture_id
             AND af.rule_id = ef.rule_id
             AND af.run_id = lr.run_id
            WHERE COALESCE(ef.is_inconclusive, 0) = 0
            GROUP BY ef.rule_id, ef.fixture_id, ef.should_trigger
        )
        SELECT rule_id,
               COUNT(*) AS labeled,
               SUM(CASE WHEN should_trigger = 1 AND did_trigger = 1 THEN 1 ELSE 0 END) AS tp,
               SUM(CASE WHEN should_trigger = 0 AND did_trigger = 1 THEN 1 ELSE 0 END) AS fp,
               SUM(CASE WHEN should_trigger = 1 AND did_trigger = 0 THEN 1 ELSE 0 END) AS fn
        FROM pairs
        GROUP BY rule_id
        ORDER BY rule_id
        """
    ).fetchall()

    usefulness: dict[str, float | None] = {}
    for rule_id, avg in cur.execute(
        "SELECT rule_id, AVG(usefulness) FROM evaluations GROUP BY rule_id"
    ).fetchall():
        usefulness[rule_id] = round(float(avg), 3) if avg is not None else None

    result: list[dict] = []
    for rule_id, labeled, tp, fp, fn in rows:
        tp = int(tp or 0)
        fp = int(fp or 0)
        fn = int(fn or 0)
        labeled = int(labeled or 0)
        precision = round(tp / (tp + fp), 3) if (tp + fp) else None
        recall = round(tp / (tp + fn), 3) if (tp + fn) else None
        result.append(
            {
                "rule_id": rule_id,
                "labeled": labeled,
                "tp": tp,
                "fp": fp,
                "fn": fn,
                "precision_score": precision,
                "recall_score": recall,
                "usefulness_score": usefulness.get(rule_id),
            }
        )
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

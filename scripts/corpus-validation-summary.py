#!/usr/bin/env python3
"""Compute corpus validation metrics for docs/rules.md refresh."""
from __future__ import annotations

import argparse
import json
import sqlite3
import sys
from datetime import datetime, timezone
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from corpus_db_read import LATEST_RUN_CTE, ensure_read_indexes


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--db",
        default=str(Path.home() / ".gauntletci" / "corpus.db"),
    )
    args = parser.parse_args()

    con = sqlite3.connect(args.db)
    ensure_read_indexes(con)
    con.row_factory = sqlite3.Row
    cur = con.cursor()

    fixtures = cur.execute("SELECT COUNT(*) FROM fixtures").fetchone()[0]
    gold_rows = cur.execute("SELECT COUNT(*) FROM expected_findings").fetchone()[0]
    gold_rules = cur.execute(
        "SELECT COUNT(DISTINCT rule_id) FROM expected_findings"
    ).fetchone()[0]
    rule_runs = cur.execute("SELECT COUNT(*) FROM rule_runs").fetchone()[0]

    snap = cur.execute(
        """
        SELECT id, snapped_at_utc, rules_snapped
        FROM audit_snapshots
        ORDER BY snapped_at_utc DESC
        LIMIT 1
        """
    ).fetchone()

    snapshot_rows: list[dict] = []
    snapshot_date = None
    if snap:
        snapshot_date = snap["snapped_at_utc"]
        rows = cur.execute(
            """
            SELECT rule_id, labeled, tp, fp, fn, precision_score
            FROM audit_snapshot_rows
            WHERE snapshot_id = ?
              AND rule_id LIKE 'GCI%'
              AND rule_id NOT LIKE 'GCI_SYN%'
              AND (tp > 0 OR fp > 0 OR fn > 0)
            ORDER BY fp DESC, fn DESC, tp DESC
            """,
            (snap["id"],),
        ).fetchall()
        snapshot_rows = [dict(r) for r in rows]

    agg_updated = cur.execute(
        "SELECT MAX(last_updated_utc) FROM aggregates"
    ).fetchone()[0]

    discovery_precision = cur.execute(
        """
        SELECT rule_id, trigger_rate, precision_score, recall_score
        FROM aggregates
        WHERE tier = 'Discovery'
          AND rule_id LIKE 'GCI%'
          AND rule_id NOT LIKE 'GCI_SYN%'
          AND precision_score IS NOT NULL
          AND precision_score > 0
        ORDER BY trigger_rate DESC, precision_score DESC
        LIMIT 12
        """
    ).fetchall()

    low_precision = cur.execute(
        """
        SELECT rule_id, trigger_rate, precision_score, recall_score
        FROM aggregates
        WHERE tier = 'Discovery'
          AND rule_id LIKE 'GCI%'
          AND rule_id NOT LIKE 'GCI_SYN%'
          AND trigger_rate >= 0.05
          AND (precision_score IS NULL OR precision_score < 0.5)
        ORDER BY trigger_rate DESC
        LIMIT 8
        """
    ).fetchall()

    # Gold-label TP/FP/FN: one verdict per (fixture, rule) on latest run
    gold_metrics = cur.execute(
        LATEST_RUN_CTE
        + """
        , pairs AS (
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
            WHERE ef.is_inconclusive = 0
            GROUP BY ef.rule_id, ef.fixture_id, ef.should_trigger
        )
        SELECT rule_id,
               SUM(CASE WHEN should_trigger = 1 AND did_trigger = 1 THEN 1 ELSE 0 END) AS tp,
               SUM(CASE WHEN should_trigger = 0 AND did_trigger = 1 THEN 1 ELSE 0 END) AS fp,
               SUM(CASE WHEN should_trigger = 1 AND did_trigger = 0 THEN 1 ELSE 0 END) AS fn,
               COUNT(*) AS pairs
        FROM pairs
        GROUP BY rule_id
        HAVING pairs >= 3
        ORDER BY fp DESC, fn DESC
        LIMIT 15
        """
    ).fetchall()

    out = {
        "generated_utc": datetime.now(timezone.utc).strftime("%Y-%m-%d %H:%M:%S UTC"),
        "fixtures": fixtures,
        "expected_findings_rows": gold_rows,
        "expected_findings_rules": gold_rules,
        "rule_runs": rule_runs,
        "aggregates_last_updated": agg_updated,
        "audit_snapshot_date": snapshot_date,
        "audit_snapshot_rows_with_tp_fp_fn": snapshot_rows,
        "discovery_precision_top": [dict(r) for r in discovery_precision],
        "discovery_low_precision_high_trigger": [dict(r) for r in low_precision],
        "gold_label_latest_run_metrics": [
            {
                "rule_id": r[0],
                "tp": r[1],
                "fp": r[2],
                "fn": r[3],
                "pairs": r[4],
                "precision": round(r[1] / (r[1] + r[2]), 3) if (r[1] + r[2]) else None,
            }
            for r in gold_metrics
        ],
    }
    print(json.dumps(out, indent=2))
    con.close()


if __name__ == "__main__":
    main()

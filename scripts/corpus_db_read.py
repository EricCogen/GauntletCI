#!/usr/bin/env python3
"""Shared read-only corpus DB helpers for agent scripts."""
from __future__ import annotations

import sqlite3

INDEXES = [
    "CREATE INDEX IF NOT EXISTS idx_actual_findings_run_id ON actual_findings(run_id)",
    "CREATE INDEX IF NOT EXISTS idx_actual_findings_run_trigger ON actual_findings(run_id, did_trigger)",
    "CREATE INDEX IF NOT EXISTS idx_rule_runs_fixture_completed ON rule_runs(fixture_id, completed_at_utc)",
    "CREATE INDEX IF NOT EXISTS idx_expected_findings_fixture_rule ON expected_findings(fixture_id, rule_id)",
]

LATEST_RUN_CTE = """
WITH latest AS (
    SELECT fixture_id, id AS run_id
    FROM (
        SELECT fixture_id,
               id,
               ROW_NUMBER() OVER (
                   PARTITION BY fixture_id
                   ORDER BY completed_at_utc DESC, id DESC
               ) AS rn
        FROM rule_runs
        WHERE UPPER(status) = 'COMPLETED'
    )
    WHERE rn = 1
)
"""


def ensure_read_indexes(con: sqlite3.Connection) -> None:
    for ddl in INDEXES:
        con.execute(ddl)
    con.commit()


def compute_labeled_rule_metrics(cur: sqlite3.Cursor) -> dict[str, dict]:
    """Per-rule TP/FP/FN from expected_findings vs latest completed run."""
    rows = cur.execute(
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
        """
    ).fetchall()

    stats: dict[str, dict] = {}
    for rule_id, labeled, tp, fp, fn in rows:
        tp = int(tp or 0)
        fp = int(fp or 0)
        fn = int(fn or 0)
        entry: dict = {
            "labeled": int(labeled or 0),
            "labeled_tp": tp,
            "labeled_fp": fp,
            "labeled_fn": fn,
            "tp": tp,
            "fp": fp,
            "fn": fn,
        }
        if tp + fp:
            entry["labeled_precision"] = round(tp / (tp + fp), 3)
        if tp + fn:
            entry["labeled_recall"] = round(tp / (tp + fn), 3)
        stats[rule_id] = entry
    return stats


def fixtures_triggered_latest_run(cur: sqlite3.Cursor) -> dict[str, int]:
    rows = cur.execute(
        LATEST_RUN_CTE
        + """
        SELECT af.rule_id, COUNT(DISTINCT af.fixture_id)
        FROM actual_findings af
        INNER JOIN latest lr ON lr.run_id = af.run_id
        WHERE af.did_trigger = 1
        GROUP BY af.rule_id
        """
    ).fetchall()
    return {rule_id: int(count) for rule_id, count in rows}

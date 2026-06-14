#!/usr/bin/env python3
"""Shared helpers for benchmark discovery sweep export and drift checks."""
from __future__ import annotations

import json
import sqlite3
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
SWEEP_JSON = REPO / "eval" / "benchmark-discovery-sweep.json"

sys_path_inserted = False


def pct_string(rate: float | None) -> str | None:
    if rate is None:
        return None
    return f"{round(rate * 100)}%"


def load_db_metrics(db_path: Path) -> tuple[dict[str, str], dict[str, str], int]:
    import sys

    global sys_path_inserted
    if not sys_path_inserted:
        sys.path.insert(0, str(REPO / "scripts"))
        sys_path_inserted = True

    from corpus_db_read import ensure_read_indexes  # noqa: E402

    con = sqlite3.connect(db_path)
    ensure_read_indexes(con)
    cur = con.cursor()

    fixture_count = int(cur.execute("SELECT COUNT(*) FROM fixtures").fetchone()[0])

    trigger_rows = cur.execute(
        """
        SELECT rule_id, trigger_rate
        FROM aggregates
        WHERE tier = 'Discovery'
          AND rule_id LIKE 'GCI%'
          AND rule_id NOT LIKE 'GCI_SYN%'
        """
    ).fetchall()
    triggers = {rid: pct_string(rate) for rid, rate in trigger_rows if rate is not None}

    gold_rows = cur.execute(
        """
        WITH latest AS (
            SELECT fixture_id, MAX(id) AS run_id
            FROM rule_runs
            GROUP BY fixture_id
        ),
        pairs AS (
            SELECT ef.rule_id,
                   ef.fixture_id,
                   ef.should_trigger,
                   MAX(af.did_trigger) AS did_trigger
            FROM expected_findings ef
            JOIN latest lr ON lr.fixture_id = ef.fixture_id
            JOIN actual_findings af
              ON af.fixture_id = ef.fixture_id
             AND af.rule_id = ef.rule_id
             AND af.run_id = lr.run_id
            WHERE ef.is_inconclusive = 0
            GROUP BY ef.rule_id, ef.fixture_id, ef.should_trigger
        )
        SELECT rule_id,
               SUM(CASE WHEN should_trigger = 1 AND did_trigger = 1 THEN 1 ELSE 0 END) AS tp,
               SUM(CASE WHEN should_trigger = 0 AND did_trigger = 1 THEN 1 ELSE 0 END) AS fp
        FROM pairs
        GROUP BY rule_id
        """
    ).fetchall()
    gold: dict[str, str] = {}
    for rule_id, tp, fp in gold_rows:
        tp = int(tp or 0)
        fp = int(fp or 0)
        if tp + fp:
            gold[rule_id] = pct_string(tp / (tp + fp)) or ""

    con.close()
    return triggers, gold, fixture_count


def load_sweep_json(path: Path = SWEEP_JSON) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def sweep_rows_as_map(doc: dict) -> dict[str, dict[str, str | None]]:
    rows: dict[str, dict[str, str | None]] = {}
    for row in doc.get("discoveryRows", []):
        rule_id = row["id"]
        rows[rule_id] = {
            "triggerPct": row.get("triggerPct"),
            "goldPrecision": row.get("goldPrecision"),
        }
    return rows

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

    from corpus_db_read import compute_labeled_rule_metrics, ensure_read_indexes  # noqa: E402

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

    # Same latest-run + LEFT JOIN logic as LabeledRuleMetricsReader / audit-snapshot CLI.
    labeled = compute_labeled_rule_metrics(cur)
    gold: dict[str, str] = {}
    for rule_id, entry in labeled.items():
        precision = entry.get("labeled_precision")
        if precision is not None:
            gold[rule_id] = pct_string(precision) or ""

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

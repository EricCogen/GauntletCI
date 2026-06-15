#!/usr/bin/env python3
"""Shared helpers for benchmark discovery sweep export and drift checks."""
from __future__ import annotations

import json
import re
import sqlite3
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
SWEEP_JSON = REPO / "eval" / "benchmark-discovery-sweep.json"

sys_path_inserted = False

RULE_ID_RE = re.compile(r"^GCI\d{4}$")
PCT_RE = re.compile(r"^\d{1,3}%$")


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


def validate_sweep_json(doc: dict) -> list[str]:
    """Structural checks for eval/benchmark-discovery-sweep.json (CI-safe, no DB)."""
    errors: list[str] = []

    if doc.get("schemaVersion") != 1:
        errors.append("schemaVersion must be 1")

    if not isinstance(doc.get("generatedUtc"), str) or not doc["generatedUtc"]:
        errors.append("generatedUtc must be a non-empty ISO timestamp string")

    if not isinstance(doc.get("corpusNote"), str) or not doc["corpusNote"]:
        errors.append("corpusNote must be a non-empty string")

    fixture_count = doc.get("fixtureCount")
    if not isinstance(fixture_count, int) or fixture_count <= 0:
        errors.append("fixtureCount must be a positive integer")

    rows = doc.get("discoveryRows")
    if not isinstance(rows, list) or not rows:
        errors.append("discoveryRows must be a non-empty list")
    else:
        seen_ids: set[str] = set()
        for index, row in enumerate(rows):
            if not isinstance(row, dict):
                errors.append(f"discoveryRows[{index}] must be an object")
                continue
            rule_id = row.get("id")
            if not isinstance(rule_id, str) or not RULE_ID_RE.match(rule_id):
                errors.append(f"discoveryRows[{index}].id must match GCI####")
            elif rule_id in seen_ids:
                errors.append(f"duplicate discoveryRows id {rule_id}")
            else:
                seen_ids.add(rule_id)
            if not isinstance(row.get("name"), str) or not row["name"]:
                errors.append(f"discoveryRows[{index}].name must be a non-empty string")
            trigger = row.get("triggerPct")
            if trigger is not None and (not isinstance(trigger, str) or not PCT_RE.match(trigger)):
                errors.append(f"discoveryRows[{index}].triggerPct must be like 12%")
            gold = row.get("goldPrecision")
            if gold is not None and (not isinstance(gold, str) or not PCT_RE.match(gold)):
                errors.append(f"discoveryRows[{index}].goldPrecision must be like 75%")

    card_gold = doc.get("ruleCardAgentGold")
    if card_gold is None:
        errors.append("ruleCardAgentGold must be present (object, may be empty)")
    elif not isinstance(card_gold, dict):
        errors.append("ruleCardAgentGold must be an object")
    else:
        for rule_id, value in card_gold.items():
            if not RULE_ID_RE.match(rule_id):
                errors.append(f"ruleCardAgentGold key {rule_id} must match GCI####")
            if not isinstance(value, str) or not PCT_RE.match(value):
                errors.append(f"ruleCardAgentGold[{rule_id}] must be like 75%")

    return errors


def sweep_rows_as_map(doc: dict) -> dict[str, dict[str, str | None]]:
    rows: dict[str, dict[str, str | None]] = {}
    for row in doc.get("discoveryRows", []):
        rule_id = row["id"]
        rows[rule_id] = {
            "triggerPct": row.get("triggerPct"),
            "goldPrecision": row.get("goldPrecision"),
        }
    return rows

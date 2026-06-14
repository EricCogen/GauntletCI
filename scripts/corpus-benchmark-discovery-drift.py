#!/usr/bin/env python3
"""Check benchmark page discovery sweep matches agent corpus DB (when present)."""
from __future__ import annotations

import argparse
import re
import sqlite3
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
BENCHMARK = REPO / "site" / "app" / "benchmark" / "page.tsx"
DEFAULT_DB = Path.home() / ".gauntletci" / "corpus.db"

sys.path.insert(0, str(REPO / "scripts"))
from corpus_db_read import ensure_read_indexes  # noqa: E402


def parse_benchmark_table(path: Path) -> dict[str, dict[str, str | None]]:
    text = path.read_text(encoding="utf-8")
    block = re.search(
        r"const discoverySweepJune2026.*?=\s*\[(.*?)\];",
        text,
        re.DOTALL,
    )
    if not block:
        raise SystemExit(f"Could not find discoverySweepJune2026 in {path}")

    rows: dict[str, dict[str, str | None]] = {}
    for entry in re.finditer(
        r'\{\s*id:\s*"([^"]+)"[^}]*triggerPct:\s*"([^"]+)"'
        r'(?:[^}]*goldPrecision:\s*"([^"]*)")?',
        block.group(1),
    ):
        rule_id, trigger_pct, gold_prec = entry.group(1), entry.group(2), entry.group(3)
        rows[rule_id] = {
            "triggerPct": trigger_pct,
            "goldPrecision": gold_prec,
        }
    return rows


def pct_string(rate: float | None) -> str | None:
    if rate is None:
        return None
    return f"{round(rate * 100)}%"


def load_db_metrics(db_path: Path) -> tuple[dict[str, str], dict[str, str]]:
    con = sqlite3.connect(db_path)
    ensure_read_indexes(con)
    cur = con.cursor()

    trigger_rows = cur.execute(
        """
        SELECT rule_id, trigger_rate
        FROM aggregates
        WHERE tier = 'Discovery'
          AND rule_id LIKE 'GCI%'
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
    return triggers, gold


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--db", default=str(DEFAULT_DB))
    parser.add_argument(
        "--skip-if-missing-db",
        action="store_true",
        help="Exit 0 when agent corpus DB is absent (CI)",
    )
    args = parser.parse_args()

    expected = parse_benchmark_table(BENCHMARK)
    db_path = Path(args.db)
    if not db_path.exists():
        if args.skip_if_missing_db:
            print(f"skip: corpus db not found at {db_path}")
            return
        raise SystemExit(f"Corpus DB not found: {db_path}")

    triggers, gold = load_db_metrics(db_path)
    errors: list[str] = []

    for rule_id, exp in expected.items():
        actual_trigger = triggers.get(rule_id)
        if actual_trigger and exp["triggerPct"] != actual_trigger:
            errors.append(
                f"{rule_id} triggerPct page={exp['triggerPct']} db={actual_trigger}"
            )
        if exp.get("goldPrecision"):
            actual_gold = gold.get(rule_id)
            if actual_gold and exp["goldPrecision"] != actual_gold:
                errors.append(
                    f"{rule_id} goldPrecision page={exp['goldPrecision']} db={actual_gold}"
                )

    if errors:
        for line in errors:
            print(f"FAIL {line}", file=sys.stderr)
        raise SystemExit(1)

    print(f"OK benchmark discovery metrics match {db_path} ({len(expected)} rules)")


if __name__ == "__main__":
    main()

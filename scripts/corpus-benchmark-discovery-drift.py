#!/usr/bin/env python3
"""Check eval/benchmark-discovery-sweep.json matches agent corpus DB (when present)."""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
DEFAULT_DB = Path.home() / ".gauntletci" / "corpus.db"

sys.path.insert(0, str(REPO / "scripts"))
from benchmark_discovery_lib import (  # noqa: E402
    SWEEP_JSON,
    load_db_metrics,
    load_sweep_json,
    sweep_rows_as_map,
    validate_sweep_json,
)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--db", default=str(DEFAULT_DB))
    parser.add_argument(
        "--json",
        default=str(SWEEP_JSON),
        help="Benchmark discovery sweep JSON path",
    )
    parser.add_argument(
        "--skip-if-missing-db",
        action="store_true",
        help="Exit 0 when agent corpus DB is absent (CI)",
    )
    args = parser.parse_args()

    json_path = Path(args.json)
    if not json_path.exists():
        raise SystemExit(f"Missing sweep JSON: {json_path}")

    doc = load_sweep_json(json_path)
    schema_errors = validate_sweep_json(doc)
    if schema_errors:
        for line in schema_errors:
            print(f"FAIL {line}", file=sys.stderr)
        raise SystemExit(1)

    expected = sweep_rows_as_map(doc)
    card_gold = doc.get("ruleCardAgentGold", {})

    db_path = Path(args.db)
    if not db_path.exists():
        if args.skip_if_missing_db:
            print(
                f"OK sweep JSON schema ({len(expected)} discovery rows, "
                f"{len(card_gold)} rule-card gold); skip DB compare (no agent corpus in CI)"
            )
            return
        raise SystemExit(f"Corpus DB not found: {db_path}")

    triggers, gold, _fixture_count = load_db_metrics(db_path)
    errors: list[str] = []

    for rule_id, exp in expected.items():
        actual_trigger = triggers.get(rule_id)
        if actual_trigger and exp.get("triggerPct") != actual_trigger:
            errors.append(
                f"{rule_id} triggerPct json={exp.get('triggerPct')} db={actual_trigger}"
            )
        if exp.get("goldPrecision"):
            actual_gold = gold.get(rule_id)
            if actual_gold and exp["goldPrecision"] != actual_gold:
                errors.append(
                    f"{rule_id} goldPrecision json={exp['goldPrecision']} db={actual_gold}"
                )

    for rule_id, exp_gold in card_gold.items():
        actual_gold = gold.get(rule_id)
        if actual_gold and exp_gold != actual_gold:
            errors.append(
                f"{rule_id} ruleCardAgentGold json={exp_gold} db={actual_gold}"
            )

    if errors:
        for line in errors:
            print(f"FAIL {line}", file=sys.stderr)
        raise SystemExit(1)

    print(
        f"OK benchmark discovery metrics match {db_path} "
        f"({len(expected)} rows, {len(card_gold)} rule-card gold entries)"
    )


if __name__ == "__main__":
    main()

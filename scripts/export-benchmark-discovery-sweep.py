#!/usr/bin/env python3
"""Refresh eval/benchmark-discovery-sweep.json metrics from agent corpus DB."""
from __future__ import annotations

import argparse
import json
import sqlite3
import sys
from datetime import datetime, timezone
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(REPO / "scripts"))

from benchmark_discovery_lib import (  # noqa: E402
    SWEEP_JSON,
    load_db_metrics,
    load_sweep_json,
)

DEFAULT_DB = Path.home() / ".gauntletci" / "corpus.db"


def refresh_metrics(doc: dict, triggers: dict[str, str], gold: dict[str, str]) -> dict:
    for row in doc.get("discoveryRows", []):
        rule_id = row["id"]
        if rule_id in triggers:
            row["triggerPct"] = triggers[rule_id]
        if rule_id in gold:
            row["goldPrecision"] = gold[rule_id]
        elif "goldPrecision" in row and rule_id not in gold:
            row.pop("goldPrecision", None)

    card_gold = doc.get("ruleCardAgentGold", {})
    for rule_id in list(card_gold.keys()):
        if rule_id in gold:
            card_gold[rule_id] = gold[rule_id]
        else:
            del card_gold[rule_id]

    doc["generatedUtc"] = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    return doc


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--db", default=str(DEFAULT_DB))
    parser.add_argument(
        "--output",
        default=str(SWEEP_JSON),
        help="Output JSON path (default: eval/benchmark-discovery-sweep.json)",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Print JSON to stdout instead of writing",
    )
    args = parser.parse_args()

    db_path = Path(args.db)
    if not db_path.exists():
        raise SystemExit(f"Corpus DB not found: {db_path}")

    doc = load_sweep_json(Path(args.output)) if Path(args.output).exists() else load_sweep_json()
    triggers, gold, fixture_count = load_db_metrics(db_path)
    doc = refresh_metrics(doc, triggers, gold)
    doc["fixtureCount"] = fixture_count

    con = sqlite3.connect(db_path)
    discovery_count = int(
        con.execute("SELECT COUNT(*) FROM fixtures WHERE tier = 'Discovery'").fetchone()[0]
    )
    con.close()
    doc["corpusNote"] = (
        f"{fixture_count} total fixtures ({discovery_count} discovery); agent corpus only. "
        "Regenerate: scripts/export-benchmark-discovery-sweep.py; "
        "verify: scripts/corpus-benchmark-discovery-drift.py"
    )

    payload = json.dumps(doc, indent=2) + "\n"
    if args.dry_run:
        print(payload)
        return

    out_path = Path(args.output)
    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text(payload, encoding="utf-8")
    print(f"wrote {out_path} ({len(doc.get('discoveryRows', []))} rows)")


if __name__ == "__main__":
    main()

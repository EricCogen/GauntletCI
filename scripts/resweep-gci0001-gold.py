#!/usr/bin/env python3
"""Re-count GCI0001 on gold fixtures that fired before the companion-file fix."""
from __future__ import annotations

import json
import os
import subprocess
import sys
from collections import Counter
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
REPORT = REPO / "eval" / "reports" / "gold-noise-sweep.json"
OUT = REPO / "eval" / "reports" / "gci0001-resweep.json"

sys.path.insert(0, str(REPO / "scripts"))
from benchmark_lib import REPO as _REPO, load_suite, resolve_diff, suite_fixtures  # noqa: E402

assert _REPO == REPO


def prior_gci0001_fixtures() -> list[str]:
    if not REPORT.exists():
        return []
    data = json.loads(REPORT.read_text(encoding="utf-8-sig"))
    out: list[str] = []
    for m in data.get("fixtures_by_sensitivity", {}).get("balanced", []):
        if m.get("rule_histogram", {}).get("GCI0001", 0) > 0:
            out.append(m["fixture_id"])
    return out


def analyze_gci0001(entry: dict) -> bool:
    diff = resolve_diff(entry)
    if not diff:
        return False
    proc = subprocess.run(
        [
            "dotnet",
            "run",
            "--project",
            str(REPO / "src/GauntletCI.Cli"),
            "--no-build",
            "-c",
            "Release",
            "--",
            "analyze",
            "--diff",
            str(diff),
            "--output",
            "json",
            "--sensitivity",
            "balanced",
            "--no-banner",
        ],
        cwd=REPO,
        capture_output=True,
        text=True,
        check=False,
    )
    if proc.returncode not in (0, 1):
        return False
    try:
        doc = json.loads(proc.stdout)
    except json.JSONDecodeError:
        return False
    return any(f.get("RuleId") == "GCI0001" for f in doc.get("Findings") or [])


def main() -> None:
    import argparse

    ap = argparse.ArgumentParser(description="Re-count GCI0001 on prior gold noise hits")
    ap.add_argument("--limit", type=int, default=0, help="Max fixtures to re-run (0 = all)")
    args = ap.parse_args()

    suite = load_suite()
    gold = {e["fixture_id"]: e for e in suite_fixtures(suite, tier="gold")}
    targets = prior_gci0001_fixtures()
    if not targets:
        print("No prior GCI0001 hits in gold-noise-sweep.json; run full sweep first.")
        raise SystemExit(0)
    if args.limit > 0:
        targets = targets[: args.limit]

    fired = 0
    missing_diff = 0
    errors = 0
    for fid in targets:
        entry = gold.get(fid)
        if not entry:
            errors += 1
            print(f"skip {fid}: not in gold suite")
            continue
        if not resolve_diff(entry):
            missing_diff += 1
            print(f"skip {fid}: no diff")
            continue
        hits = analyze_gci0001(entry)
        if hits:
            fired += 1
        print(f"{'GCI0001' if hits else 'ok'} {fid}")

    summary = {
        "prior_gci0001_fixture_count": len(targets),
        "still_fires_gci0001": fired,
        "resolved_noise_reduction": len(targets) - fired - missing_diff - errors,
        "missing_diff": missing_diff,
        "not_in_gold_suite": errors,
        "prior_report_gci0001_total": 171,
    }
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(summary, indent=2))


if __name__ == "__main__":
    main()

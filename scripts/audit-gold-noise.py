#!/usr/bin/env python3
"""Audit GauntletCI noise on gold fixtures: rule histograms, sensitivity sweep, corpus FP hints."""
from __future__ import annotations

import argparse
import json
import os
import sqlite3
import subprocess
from collections import Counter, defaultdict
from datetime import datetime, timezone
from pathlib import Path

from benchmark_lib import DB, REPO, load_suite, resolve_diff, suite_fixtures

EVAL = REPO / "eval"
GT_DIR = EVAL / "ground-truth"
RUNS = EVAL / "runs" / "gauntletci"
SWEEP_ROOT = EVAL / "runs" / "gauntletci-sweep"
REPORT_PATH = EVAL / "reports" / "gold-noise-sweep.json"
CAP = 25
SENSITIVITIES = ("strict", "balanced", "permissive")


def read_run_json(path: Path) -> dict | None:
    if not path.exists() or path.stat().st_size == 0:
        return None
    return json.loads(path.read_text(encoding="utf-8-sig"))


def load_gt(fixture_id: str) -> dict | None:
    path = GT_DIR / f"{fixture_id}.json"
    if not path.exists():
        return None
    return json.loads(path.read_text(encoding="utf-8-sig"))


def ci_required_rules(gt: dict | None) -> set[str]:
    if not gt:
        return set()
    return {d["primary_rule"] for d in gt.get("defects", []) if d.get("ci_required")}


def primary_rules_from_gt(gt: dict | None) -> set[str]:
    if not gt:
        return set()
    return {d["primary_rule"] for d in gt.get("defects", []) if d.get("primary_rule")}


def run_path(sensitivity: str, fixture_id: str, *, use_main_balanced: bool) -> Path:
    if sensitivity == "balanced" and use_main_balanced:
        return RUNS / f"{fixture_id}.json"
    return SWEEP_ROOT / sensitivity / f"{fixture_id}.json"


def analyze_fixture(entry: dict, sensitivity: str, out_path: Path) -> dict | None:
    fid = entry["fixture_id"]
    diff = resolve_diff(entry)
    if not diff:
        return None

    cfg = Path(os.environ.get("TEMP", ".")) / f"gci-audit-{fid}-{sensitivity}"
    cfg.mkdir(parents=True, exist_ok=True)
    (cfg / ".gauntletci.json").write_text(
        json.dumps(
            {
                "domain": {"profile": entry.get("domain_profile", "library")},
                "output": {"delivery": {"enabled": True, "globalMaxFindings": CAP}},
                "provenance": {"enabled": True},
            },
            indent=2,
        ),
        encoding="utf-8",
    )
    out_path.parent.mkdir(parents=True, exist_ok=True)
    proc = subprocess.run(
        [
            "dotnet",
            "run",
            "--project",
            str(REPO / "src/GauntletCI.Cli"),
            "--no-build",
            "--",
            "analyze",
            "--diff",
            str(diff),
            "--repo",
            str(cfg),
            "--output",
            str(out_path),
            "--sensitivity",
            sensitivity,
            "--no-banner",
        ],
        cwd=REPO,
        capture_output=True,
        text=True,
        check=False,
    )
    if proc.returncode != 0 or not out_path.exists():
        err = (proc.stderr or proc.stdout or "")[:300]
        print(f"FAIL {fid} {sensitivity}: {err}")
        return None
    return read_run_json(out_path)


def findings_from_doc(doc: dict | None) -> list[dict]:
    if not doc:
        return []
    return doc.get("Findings") or []


def fixture_metrics(
    entry: dict,
    sensitivity: str,
    doc: dict | None,
    gt: dict | None,
) -> dict:
    findings = findings_from_doc(doc)
    required = ci_required_rules(gt)
    primary = primary_rules_from_gt(gt)
    fired = {f.get("RuleId") for f in findings if f.get("RuleId")}
    rule_counts = Counter(f.get("RuleId") for f in findings if f.get("RuleId"))
    noise_rules = [r for r in rule_counts if r not in required]
    missing_required = sorted(required - fired)

    return {
        "fixture_id": entry["fixture_id"],
        "repo": entry["repo"],
        "sensitivity": sensitivity,
        "finding_count": len(findings),
        "at_cap": len(findings) >= CAP,
        "rule_histogram": dict(rule_counts),
        "ci_required_rules": sorted(required),
        "ci_required_fired": sorted(required & fired),
        "ci_required_missing": missing_required,
        "recall_ok": len(missing_required) == 0,
        "noise_rule_count": sum(rule_counts[r] for r in noise_rules),
        "top_noise_rules": rule_counts.most_common(5),
    }


def aggregate_noise(metrics: list[dict]) -> dict:
    counts = [m["finding_count"] for m in metrics]
    if not counts:
        return {"fixture_count": 0, "data_available": False}
    has_data = any(c > 0 for c in counts)
    counts_sorted = sorted(counts)
    mid = len(counts_sorted) // 2
    return {
        "fixture_count": len(counts),
        "data_available": has_data,
        "mean_findings": round(sum(counts) / len(counts), 2) if has_data else 0,
        "median_findings": counts_sorted[mid] if has_data else 0,
        "max_findings": max(counts) if has_data else 0,
        "at_delivery_cap": sum(1 for m in metrics if m["at_cap"]),
        "recall_failures": [m["fixture_id"] for m in metrics if has_data and not m["recall_ok"]],
    }


def corpus_rule_fp_counts(rule_ids: list[str]) -> dict[str, dict]:
    if not DB.exists() or not rule_ids:
        return {}
    con = sqlite3.connect(DB)
    out: dict[str, dict] = {}
    for rid in rule_ids:
        row = con.execute(
            """
            SELECT
              SUM(CASE WHEN a.did_trigger = 1 AND e.should_trigger = 0 THEN 1 ELSE 0 END),
              SUM(CASE WHEN a.did_trigger = 1 AND e.should_trigger = 1 THEN 1 ELSE 0 END),
              SUM(CASE WHEN a.did_trigger = 0 AND e.should_trigger = 1 THEN 1 ELSE 0 END)
            FROM actual_findings a
            JOIN expected_findings e
              ON e.fixture_id = a.fixture_id AND e.rule_id = a.rule_id
            WHERE a.rule_id = ?
            """,
            (rid,),
        ).fetchone()
        if not row:
            continue
        fp, tp, fn = (int(row[0] or 0), int(row[1] or 0), int(row[2] or 0))
        prec = round(tp / (tp + fp), 3) if (tp + fp) else None
        out[rid] = {"fp": fp, "tp": tp, "fn": fn, "precision": prec}
    con.close()
    return out


def global_rule_histogram(metrics: list[dict]) -> list[dict]:
    total = Counter()
    required_hits = Counter()
    for m in metrics:
        for rule, n in m["rule_histogram"].items():
            total[rule] += n
            if rule in m["ci_required_rules"]:
                required_hits[rule] += n
    rows = []
    for rule, count in total.most_common():
        req_n = required_hits.get(rule, 0)
        rows.append(
            {
                "rule_id": rule,
                "gold_finding_count": count,
                "ci_required_hits": req_n,
                "likely_noise_on_gold": count > req_n,
                "noise_findings": count - req_n,
            }
        )
    return rows


def main() -> None:
    ap = argparse.ArgumentParser(description="Gold noise audit and sensitivity sweep")
    ap.add_argument("--sweep", action="store_true", help="Run analyze at strict/balanced/permissive into sweep dir")
    ap.add_argument(
        "--use-main-runs",
        action="store_true",
        default=True,
        help="Use eval/runs/gauntletci for balanced (default)",
    )
    ap.add_argument("--no-use-main-runs", action="store_false", dest="use_main_runs")
    ap.add_argument("--report-only", action="store_true", help="Only read existing JSON outputs")
    ap.add_argument("--top-rules", type=int, default=3, help="Corpus FP lookup for top N noisy rules")
    args = ap.parse_args()

    suite = load_suite()
    gold = suite_fixtures(suite, tier="gold")
    if not gold:
        raise SystemExit("No gold fixtures in benchmark-suite.json")

    by_sensitivity: dict[str, list[dict]] = {s: [] for s in SENSITIVITIES}
    global_hist_balanced = Counter()

    for entry in gold:
        fid = entry["fixture_id"]
        gt = load_gt(fid)
        for sens in SENSITIVITIES:
            path = run_path(sens, fid, use_main_balanced=args.use_main_runs)
            doc = None
            if path.exists():
                doc = read_run_json(path)
            elif args.sweep and not args.report_only:
                if sens == "balanced" and args.use_main_runs and path == RUNS / f"{fid}.json" and path.exists():
                    doc = read_run_json(path)
                else:
                    doc = analyze_fixture(entry, sens, path)
            elif sens == "balanced" and (RUNS / f"{fid}.json").exists():
                doc = read_run_json(RUNS / f"{fid}.json")

            m = fixture_metrics(entry, sens, doc, gt)
            by_sensitivity[sens].append(m)
            if sens == "balanced":
                for rule, n in m["rule_histogram"].items():
                    global_hist_balanced[rule] += n
            if doc is None:
                print(f"skip {fid} {sens}: no run data")
            else:
                status = "ok" if m["recall_ok"] else "MISS"
                print(f"{status} {fid} {sens}: {m['finding_count']} findings")

    rule_rows = global_rule_histogram(by_sensitivity["balanced"])
    noisy = sorted(
        [r for r in rule_rows if r["likely_noise_on_gold"]],
        key=lambda r: r["noise_findings"],
        reverse=True,
    )
    top_noisy_ids = [r["rule_id"] for r in noisy[: args.top_rules]]
    corpus_fp = corpus_rule_fp_counts(top_noisy_ids)

    report = {
        "schema_version": "1.0.0",
        "generated_at_utc": datetime.now(timezone.utc).isoformat(),
        "gold_fixture_count": len(gold),
        "delivery_cap": CAP,
        "recommended_gate_sensitivity": "balanced",
        "sensitivity_summary": {
            s: aggregate_noise(by_sensitivity[s]) for s in SENSITIVITIES
        },
        "balanced_global_rule_histogram": rule_rows[:30],
        "top_noisy_rules_balanced": noisy[:15],
        "corpus_fp_for_top_noisy": corpus_fp,
        "next_actions": [
            f"Tune delivery/rules starting with: {', '.join(top_noisy_ids)}" if top_noisy_ids else "No dominant noisy rules",
            "Hold recall: every gold ci_required rule must appear in balanced ci_required_fired",
            "CI benchmark: run-benchmark-suite.ps1 -GoldOnly -Sensitivity balanced",
        ],
        "fixtures_by_sensitivity": by_sensitivity,
    }

    REPORT_PATH.parent.mkdir(parents=True, exist_ok=True)
    REPORT_PATH.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(f"Wrote {REPORT_PATH}")

    bal = report["sensitivity_summary"]["balanced"]
    perm = report["sensitivity_summary"]["permissive"]
    if bal.get("fixture_count"):
        print(
            f"balanced: mean={bal.get('mean_findings')} median={bal.get('median_findings')} "
            f"cap_hits={bal.get('at_delivery_cap')} recall_failures={len(bal.get('recall_failures', []))}"
        )
    if bal.get("data_available") and perm.get("data_available") and perm.get("mean_findings"):
        reduction = 1 - (bal["mean_findings"] / perm["mean_findings"])
        print(f"mean findings reduction balanced vs permissive: {round(reduction * 100, 1)}%")
    elif not perm.get("data_available"):
        print("Run with -FullSweep on run-gold-noise-audit.ps1 for strict/permissive comparison")


if __name__ == "__main__":
    main()

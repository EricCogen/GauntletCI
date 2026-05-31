#!/usr/bin/env python3
"""Promote corpus expected_findings to eval/ground-truth and validate GauntletCI primary rules."""
from __future__ import annotations

import json
import os
import sqlite3
import subprocess
from datetime import datetime, timezone
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
DB = Path(os.environ.get("USERPROFILE", "")) / ".gauntletci" / "corpus.db"
SUITE = REPO / "eval" / "benchmark-suite.json"
GT_DIR = REPO / "eval" / "ground-truth"
RUNS = REPO / "eval" / "runs" / "gauntletci"

RULE_FN = {
    "GCI0058": "sibling-implementation-drift",
    "GCI0007": "intentional-swallow",
    "GCI0016": "concurrency-risk",
    "GCI0003": "logic-bug-no-token",
    "GCI0004": "breaking-change",
    "GCI0006": "edge-case-handling",
    "GCI0015": "data-integrity-risk",
    "GCI0012": "security-risk",
    "GCI0024": "resource-leak",
}


def normalize_rule(rid: str) -> str:
    if rid.startswith("GCI") and len(rid) == 7 and rid[3:].isdigit():
        return f"GCI{int(rid[3:]):04d}"
    return rid


def load_suite() -> dict:
    return json.loads(SUITE.read_text(encoding="utf-8"))


def save_suite(doc: dict) -> None:
    doc["generated_at_utc"] = datetime.now(timezone.utc).isoformat()
    SUITE.write_text(json.dumps(doc, indent=2) + "\n", encoding="utf-8")


def expected_for(con: sqlite3.Connection, fixture_id: str) -> list[dict]:
    rows = con.execute(
        """
        SELECT rule_id, reason, label_source
        FROM expected_findings
        WHERE fixture_id = ? AND should_trigger = 1 AND COALESCE(is_inconclusive, 0) = 0
        ORDER BY expected_confidence DESC
        """,
        (fixture_id,),
    ).fetchall()
    out = []
    for rule_id, reason, label_source in rows:
        src = (label_source or "").lower()
        if not any(x in src for x in ("human", "seed", "manual", "gold")):
            continue
        rid = normalize_rule(rule_id)
        if rid.startswith("GCI_SYN"):
            continue
        out.append({"rule_id": rid, "reason": reason or "", "label_source": label_source})
    return out[:3]


def write_ground_truth(entry: dict, labels: list[dict]) -> Path:
    defects = []
    for i, lab in enumerate(labels):
        rid = lab["rule_id"]
        defects.append(
            {
                "defect_id": f"corpus-{rid.lower()}",
                "summary": (lab["reason"] or f"Corpus label expects {rid}")[:240],
                "primary_rule": rid,
                "fn_class": RULE_FN.get(rid, "logic-bug-no-token"),
                "file": "",
                "adjudication_confidence": "medium",
                "ci_required": False,
                "label_source": lab.get("label_source"),
                "match_keywords": [rid],
            }
        )
    doc = {
        "schema_version": "1.0.0",
        "fixture_id": entry["fixture_id"],
        "repo": entry["repo"],
        "pr_number": entry["pr_number"],
        "suite_tier": entry["suite_tier"],
        "upstream_pr_url": f"https://github.com/{entry['repo']}/pull/{entry['pr_number']}",
        "domain_profile": entry.get("domain_profile", "library"),
        "defects": defects,
        "notes": "Auto-promoted from corpus expected_findings; refine file/line for gold tier.",
    }
    path = GT_DIR / f"{entry['fixture_id']}.json"
    path.write_text(json.dumps(doc, indent=2) + "\n", encoding="utf-8")
    return path


def run_analyze(entry: dict) -> list[str]:
    fid = entry["fixture_id"]
    diff = REPO / "eval" / "diffs" / f"{fid}.patch"
    if not diff.exists():
        cp = entry.get("corpus_path")
        if cp:
            src = REPO / cp.replace("/", "\\") / "diff.patch"
            if src.exists():
                diff.parent.mkdir(parents=True, exist_ok=True)
                diff.write_bytes(src.read_bytes())
    if not diff.exists():
        return []

    cfg = Path(os.environ.get("TEMP", ".")) / f"gci-promote-{fid}"
    cfg.mkdir(parents=True, exist_ok=True)
    (cfg / ".gauntletci.json").write_text(
        json.dumps(
            {
                "domain": {"profile": entry.get("domain_profile", "library")},
                "output": {"delivery": {"enabled": True, "globalMaxFindings": 25}},
                "provenance": {"enabled": True},
            },
            indent=2,
        ),
        encoding="utf-8",
    )
    out = RUNS / f"{fid}.json"
    RUNS.mkdir(parents=True, exist_ok=True)
    subprocess.run(
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
            str(out),
            "--sensitivity",
            "permissive",
            "--no-banner",
        ],
        cwd=REPO,
        capture_output=True,
        text=True,
        check=False,
    )
    if not out.exists():
        return []
    doc = json.loads(out.read_text(encoding="utf-8-sig"))
    return [f.get("RuleId") for f in doc.get("Findings", [])]


def main() -> None:
    import argparse

    ap = argparse.ArgumentParser()
    ap.add_argument("--refresh-all", action="store_true", help="Re-promote and re-validate all gold fixtures")
    args = ap.parse_args()

    GT_DIR.mkdir(parents=True, exist_ok=True)
    con = sqlite3.connect(DB)
    suite = load_suite()
    validated = 0
    for entry in suite["fixtures"]:
        if entry["suite_tier"] not in ("gold",):
            continue
        if (GT_DIR / f"{entry['fixture_id']}.json").exists() and not args.refresh_all:
            continue
        labels = expected_for(con, entry["fixture_id"])
        if not labels:
            continue
        write_ground_truth(entry, labels)
        fired = set(run_analyze(entry))
        primary = []
        gt_path = GT_DIR / f"{entry['fixture_id']}.json"
        gt = json.loads(gt_path.read_text(encoding="utf-8"))
        for d in gt["defects"]:
            if d["primary_rule"] in fired:
                d["ci_required"] = True
                primary.append(d["primary_rule"])
        gt_path.write_text(json.dumps(gt, indent=2) + "\n", encoding="utf-8")
        entry["primary_rules"] = primary
        entry["defect_ids"] = [d["defect_id"] for d in gt["defects"]]
        entry["ci_regression"] = len(primary) > 0
        if primary:
            validated += 1
        print(entry["fixture_id"], "primary", primary)
    con.close()
    save_suite(suite)
    print(f"validated gold with CI rules: {validated}")


if __name__ == "__main__":
    main()

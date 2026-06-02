#!/usr/bin/env python3
"""Synthesize eval/reports/gold-expansion.json from suite, scorecards, and scope."""
from __future__ import annotations

import json
from datetime import datetime, timezone
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
EVAL = REPO / "eval"


def load(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def main() -> None:
    suite = load(EVAL / "benchmark-suite.json")
    rollup = load(EVAL / "scorecards" / "competitive-suite.json")
    scope = load(EVAL / "competitor-scope.json")
    matrix = load(EVAL / "competitive-matrix.json")

    gold = [f for f in suite.get("fixtures", []) if f.get("suite_tier") == "gold"]
    gold_seg = rollup.get("segments", {}).get("gold_cross_repo", {})
    recall = gold_seg.get("recall_by_tool", {})

    headline_parts = []
    gci = recall.get("GauntletCI", {})
    if gci.get("total"):
        headline_parts.append(f"GauntletCI {gci['caught']}/{gci['total']} ci_required recall")
    for tool in ("CodeQL", "SonarCloud", "Semgrep"):
        row = recall.get(tool, {})
        if row.get("total"):
            headline_parts.append(f"{tool} {row['caught']}/{row['total']}")

    noise_sweep_path = EVAL / "reports" / "gold-noise-sweep.json"
    noise_sweep = load(noise_sweep_path) if noise_sweep_path.exists() else None

    report = {
        "schema_version": "1.0.0",
        "generated_at_utc": datetime.now(timezone.utc).isoformat(),
        "comparison_scope": scope.get("comparison_scope"),
        "gauntletci_promise": scope.get("gauntletci_promise"),
        "gold_fixture_count": len(gold),
        "gold_scoring_eligible": sum(1 for f in gold if f.get("scoring_eligible")),
        "gold_with_primary_rules": sum(1 for f in gold if f.get("primary_rules")),
        "repos_in_gold": len({f["repo"] for f in gold}),
        "selection_report": suite.get("selection_report"),
        "scale_note": (
            "Full gold cohort includes fixtures without corpus labels; "
            "measured ci_required recall applies only after ground-truth promotion."
            if len(gold) > 80
            else None
        ),
        "measured_recall": recall,
        "recall_by_fn_class": gold_seg.get("recall_by_fn_class", {}),
        "gauntletci_noise": gold_seg.get("gauntletci_noise", {}),
        "gold_noise_sweep": noise_sweep.get("sensitivity_summary") if noise_sweep else None,
        "top_noisy_rules_balanced": (noise_sweep.get("top_noisy_rules_balanced") or [])[:5] if noise_sweep else None,
        "recommended_gate_sensitivity": noise_sweep.get("recommended_gate_sensitivity") if noise_sweep else "balanced",
        "anchor_recall": rollup.get("segments", {}).get("anchor_only", {}).get("recall_by_tool"),
        "evidence_tier": rollup.get("evidence_tier"),
        "headline": "; ".join(headline_parts) if headline_parts else matrix.get("headline"),
        "angles_covered": [
            "same_defect_recall_ci_required",
            "recall_by_fn_class",
            "gauntletci_delivery_noise_on_gold",
            "gold_noise_sensitivity_sweep",
            "static_vs_llm_on_anchor",
            "free_tier_comparable_tools_only",
        ],
        "artifacts": [
            "eval/benchmark-suite.json",
            "eval/competitor-scope.json",
            "eval/scorecards/competitive-suite.json",
            "eval/competitive-matrix.json",
            "eval/ground-truth/",
            "eval/competitor-runs/",
            "eval/reports/gold-noise-sweep.json",
        ],
    }

    out = EVAL / "reports" / "gold-expansion.json"
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(f"Wrote {out}")
    print(report["headline"])

    matrix["headline"] = (
        f"Gold expansion: {len(gold)} fixtures, {report['repos_in_gold']} repos. {report['headline']}"
    )
    matrix["gold_expansion_report"] = "eval/reports/gold-expansion.json"
    matrix["scorecard_generated_at_utc"] = rollup.get("generated_at_utc")
    matrix["gold_cross_repo"] = {
        "fixtures": gold_seg.get("fixtures", len(gold)),
        "recall_by_tool": recall,
        "recall_by_fn_class": gold_seg.get("recall_by_fn_class", {}),
        "gauntletci_noise": gold_seg.get("gauntletci_noise", {}),
    }
    (EVAL / "competitive-matrix.json").write_text(json.dumps(matrix, indent=2) + "\n", encoding="utf-8")
    print("Updated eval/competitive-matrix.json headline")


if __name__ == "__main__":
    main()

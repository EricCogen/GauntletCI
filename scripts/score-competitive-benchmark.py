#!/usr/bin/env python3
"""Score competitive benchmark: ground truth + competitor runs + GauntletCI outputs."""
from __future__ import annotations

import argparse
import json
import re
from datetime import datetime, timezone
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
EVAL = REPO / "eval"


def load_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def match_llm(text: str, defect: dict) -> tuple[bool, list[str]]:
    low = text.lower()
    evidence: list[str] = []
    file_part = defect.get("file", "").lower()
    if file_part and Path(file_part).name.lower() not in low:
        return False, []
    keywords = defect.get("match_keywords") or []
    hits = [k for k in keywords if k.lower() in low]
    if len(hits) < 2:
        return False, []
    evidence.extend(hits[:5])
    fn = defect.get("fn_class", "")
    if fn == "sibling-implementation-drift":
        if "invert" in low or "opposite" in low or "polarity" in low:
            evidence.append("inversion-language")
            return True, evidence
    if fn == "intentional-swallow" and "catch" in low:
        evidence.append("catch-mentioned")
        return True, evidence
    if hits:
        return True, evidence
    return False, evidence


def match_codeql(alerts: list[dict], defect: dict) -> tuple[bool, list[str]]:
    file_part = defect.get("file", "").lower()
    evidence: list[str] = []
    for a in alerts:
        cf = (a.get("changed_file") or a.get("file") or "").lower()
        if file_part and file_part not in cf and Path(file_part).name not in cf:
            continue
        msg = (a.get("message") or "") + " " + (a.get("codeql_rule_name") or a.get("codeql_rule") or "")
        low = msg.lower()
        if defect.get("fn_class") == "sibling-implementation-drift":
            if any(t in low for t in ("logic", "condition", "comparison", "always true", "dead code")):
                evidence.append(a.get("codeql_rule", "codeql"))
                return True, evidence
        evidence.append(a.get("codeql_rule", "codeql-alert"))
    return False, evidence


def match_gauntlet(findings: list[dict], defect: dict) -> tuple[bool, list[str]]:
    rule = defect.get("primary_rule")
    for f in findings:
        if f.get("RuleId") == rule:
            return True, [rule, f.get("Evidence", "")[:120]]
    return False, []


def score_fixture(
    fixture_id: str,
    gt: dict,
    gci_path: Path | None,
    competitor_dir: Path,
    codescan_path: Path | None,
) -> dict:
    defects = gt.get("defects", [])
    findings = []
    if gci_path and gci_path.exists():
        doc = load_json(gci_path)
        findings = doc.get("Findings") or []

    tool_names = ["GauntletCI", "CodeQL", "CodeRabbit", "Greptile", "Qodo"]
    tools_out = []
    for name in tool_names:
        caught_by: dict = {}
        run_doc: dict = {}
        tool_file = competitor_dir / f"{name.lower().replace(' ', '')}.json"
        if name == "GauntletCI" and gci_path:
            run_doc = {"finding_count": len(findings), "source": str(gci_path)}
        elif tool_file.exists():
            run_doc = load_json(tool_file)
        elif name == "CodeQL" and codescan_path and codescan_path.exists():
            run_doc = load_json(codescan_path)
        else:
            run_doc = {}

        text_blob = json.dumps(run_doc)
        alerts = run_doc.get("alerts") or run_doc.get("code_scanning_alerts") or []

        for d in defects:
            did = d["defect_id"]
            if name == "GauntletCI":
                ok, ev = match_gauntlet(findings, d)
            elif name == "CodeQL":
                ok, ev = match_codeql(alerts if alerts else [], d)
                if not ok and not alerts:
                    body = text_blob
                    ok, ev = match_llm(body, d) if "codeql" in body else (False, [])
            else:
                ok, ev = match_llm(text_blob, d)
            caught_by[did] = {
                "caught_ground_truth": ok,
                "evidence": ev,
                "notes": "" if ok else "no_match",
            }

        tools_out.append(
            {
                "name": name,
                "finding_count": run_doc.get("finding_count") or len(alerts) or len(findings) if name == "GauntletCI" else None,
                "harvested_at_utc": run_doc.get("harvested_at_utc"),
                "caught_by_defect": caught_by,
            }
        )

    return {
        "schema_version": "1.0.0",
        "fixture_id": fixture_id,
        "repo": gt.get("repo"),
        "pr_number": gt.get("pr_number"),
        "suite_tier": gt.get("suite_tier", "anchor"),
        "generated_at_utc": datetime.now(timezone.utc).isoformat(),
        "upstream_pr_url": gt.get("upstream_pr_url"),
        "eval_lab_pr_url": gt.get("eval_lab_pr_url"),
        "defects": defects,
        "tools": tools_out,
    }


def rollup(scorecards: list[dict]) -> dict:
    anchor = [s for s in scorecards if s.get("suite_tier") == "anchor"]
    gold = [s for s in scorecards if s.get("suite_tier") == "gold"]

    def recall_segment(cards: list[dict]) -> dict:
        tools = ["GauntletCI", "CodeQL", "CodeRabbit", "Greptile", "Qodo"]
        out: dict = {}
        for t in tools:
            caught = 0
            total = 0
            for card in cards:
                for defect in card.get("defects", []):
                    if not defect.get("ci_required", True):
                        continue
                    did = defect["defect_id"]
                    total += 1
                    for tool in card.get("tools", []):
                        if tool["name"] == t:
                            if tool.get("caught_by_defect", {}).get(did, {}).get("caught_ground_truth"):
                                caught += 1
                            break
            out[t] = {"caught": caught, "total": total, "rate": round(caught / total, 3) if total else 0}
        return out

    repos = {s.get("repo") for s in scorecards}
    return {
        "schema_version": "1.0.0",
        "generated_at_utc": datetime.now(timezone.utc).isoformat(),
        "sample_size": sum(1 for s in scorecards for d in s.get("defects", []) if d.get("ci_required", True)),
        "fixtures_scored": len(scorecards),
        "repos_represented": len(repos),
        "evidence_tier": "measured" if len(gold) >= 15 and len(repos) >= 8 else "provisional",
        "segments": {
            "anchor_only": {"fixtures": len(anchor), "recall_by_tool": recall_segment(anchor)},
            "gold_cross_repo": {"fixtures": len(gold), "recall_by_tool": recall_segment(gold)},
        },
    }


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--fixture", default="stackexchange-redis-pr-2995")
    ap.add_argument("--all-with-ground-truth", action="store_true")
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()

    gt_dir = EVAL / "ground-truth"
    score_dir = EVAL / "scorecards"
    score_dir.mkdir(parents=True, exist_ok=True)

    fixture_ids = [args.fixture]
    if args.all_with_ground_truth:
        fixture_ids = [p.stem for p in gt_dir.glob("*.json")]

    scorecards: list[dict] = []
    for fid in fixture_ids:
        gt_path = gt_dir / f"{fid}.json"
        if not gt_path.exists():
            continue
        gt = load_json(gt_path)
        comp_dir = EVAL / "competitor-runs" / fid
        gci = EVAL / "runs" / "gauntletci" / f"{fid}.json"
        if not gci.exists():
            latest = EVAL / "redis-2995-latest.json"
            if fid == "stackexchange-redis-pr-2995" and latest.exists():
                gci = latest
        codescan = comp_dir / "codeql.json"
        card = score_fixture(fid, gt, gci if gci.exists() else None, comp_dir, codescan)
        out_path = score_dir / f"{fid}.json"
        out_path.write_text(json.dumps(card, indent=2) + "\n", encoding="utf-8")
        scorecards.append(card)
        print(f"Wrote {out_path}")

    if scorecards:
        rollup_doc = rollup(scorecards)
        rollup_path = score_dir / "competitive-suite.json"
        rollup_path.write_text(json.dumps(rollup_doc, indent=2) + "\n", encoding="utf-8")
        print(f"Wrote {rollup_path}")

        if args.check:
            for card in scorecards:
                if card.get("suite_tier") not in ("anchor", "gold"):
                    continue
                for tool in card.get("tools", []):
                    if tool["name"] != "GauntletCI":
                        continue
                    for did, row in tool.get("caught_by_defect", {}).items():
                        defect = next((d for d in card.get("defects", []) if d["defect_id"] == did), {})
                        if defect.get("ci_required", True) and not row.get("caught_ground_truth"):
                            raise SystemExit(f"GauntletCI missed {did} on {card['fixture_id']}")


if __name__ == "__main__":
    main()
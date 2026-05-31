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
SCOPE_PATH = EVAL / "competitor-scope.json"


def load_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def load_competitor_scope() -> dict:
    return load_json(SCOPE_PATH)


def scoring_tool_names(scope: dict) -> list[str]:
    """Tool display names to score per eval/competitor-scope.json harness segments."""
    segments = scope.get("harness_segments", {})
    names: list[str] = []
    for key in ("anchor_only", "gold_cross_repo"):
        seg = segments.get(key, {})
        for n in seg.get("tool_names", []):
            if n not in names:
                names.append(n)
    return names or ["GauntletCI", "CodeQL", "CodeRabbit", "Greptile", "Qodo"]


def harvest_artifact_path(competitor_dir: Path, tool_name: str, scope: dict) -> Path | None:
    for entry in scope.get("comparable_tools", []):
        if entry.get("name") != tool_name:
            continue
        artifact = entry.get("harvest_artifact")
        if artifact:
            return competitor_dir / artifact
        return None
    legacy = tool_name.lower().replace(" ", "")
    legacy_path = competitor_dir / f"{legacy}.json"
    return legacy_path if legacy_path.exists() else None


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


def _file_overlap(defect: dict, alert_path: str) -> bool:
    file_part = defect.get("file", "").lower()
    cf = alert_path.lower()
    if not file_part:
        return True
    return file_part in cf or Path(file_part).name in cf


def match_static_alerts(alerts: list[dict], defect: dict, *, strict_logic: bool = True) -> tuple[bool, list[str]]:
    evidence: list[str] = []
    for a in alerts:
        cf = a.get("changed_file") or a.get("file") or ""
        if not _file_overlap(defect, cf):
            continue
        msg = " ".join(
            str(a.get(k) or "")
            for k in ("message", "codeql_rule_name", "codeql_rule", "sonar_rule", "rule_id", "sonar_message")
        )
        low = msg.lower()
        rule = (
            a.get("codeql_rule")
            or a.get("sonar_rule")
            or a.get("rule_id")
            or "static-alert"
        )
        if defect.get("fn_class") == "sibling-implementation-drift" and strict_logic:
            if any(t in low for t in ("logic", "condition", "comparison", "always true", "dead code", "invert")):
                evidence.append(str(rule))
                return True, evidence
            continue
        if defect.get("fn_class") == "intentional-swallow" and "catch" in low:
            evidence.append(str(rule))
            return True, evidence
        if defect.get("fn_class") == "breaking-change" and any(
            t in low for t in ("deprecated", "obsolete", "removed", "breaking")
        ):
            evidence.append(str(rule))
            return True, evidence
        if defect.get("primary_rule", "").lower() in low:
            evidence.append(str(rule))
            return True, evidence
    return False, evidence


def match_codeql(alerts: list[dict], defect: dict) -> tuple[bool, list[str]]:
    return match_static_alerts(alerts, defect, strict_logic=True)


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
    scope: dict,
    tool_names: list[str],
) -> dict:
    defects = gt.get("defects", [])
    findings = []
    if gci_path and gci_path.exists():
        doc = load_json(gci_path)
        findings = doc.get("Findings") or []

    tools_out = []
    for name in tool_names:
        caught_by: dict = {}
        run_doc: dict = {}
        tool_file = harvest_artifact_path(competitor_dir, name, scope)
        if name == "GauntletCI" and gci_path:
            run_doc = {"finding_count": len(findings), "source": str(gci_path)}
        elif tool_file and tool_file.exists():
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
            elif name in ("CodeQL", "SonarCloud", "Semgrep"):
                ok, ev = match_static_alerts(alerts if alerts else [], d, strict_logic=(name == "CodeQL"))
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


def rollup(scorecards: list[dict], scope: dict) -> dict:
    anchor = [s for s in scorecards if s.get("suite_tier") == "anchor"]
    gold = [s for s in scorecards if s.get("suite_tier") == "gold"]
    segments = scope.get("harness_segments", {})

    def recall_segment(cards: list[dict], segment_key: str) -> dict:
        tools = segments.get(segment_key, {}).get("tool_names", scoring_tool_names(scope))
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

    def recall_by_fn_class(cards: list[dict], segment_key: str) -> dict:
        tools = segments.get(segment_key, {}).get("tool_names", scoring_tool_names(scope))
        by_class: dict[str, dict] = {}
        for card in cards:
            for defect in card.get("defects", []):
                if not defect.get("ci_required", True):
                    continue
                fn = defect.get("fn_class", "unknown")
                did = defect["defect_id"]
                bucket = by_class.setdefault(fn, {t: {"caught": 0, "total": 0} for t in tools})
                for t in tools:
                    bucket[t]["total"] += 1
                    for tool in card.get("tools", []):
                        if tool["name"] == t and tool.get("caught_by_defect", {}).get(did, {}).get(
                            "caught_ground_truth"
                        ):
                            bucket[t]["caught"] += 1
                            break
        for fn, tool_stats in by_class.items():
            for t, row in tool_stats.items():
                total = row["total"]
                row["rate"] = round(row["caught"] / total, 3) if total else 0.0
        return by_class

    def gauntlet_gold_noise(cards: list[dict]) -> dict:
        counts: list[int] = []
        at_cap = 0
        for card in cards:
            for tool in card.get("tools", []):
                if tool["name"] != "GauntletCI":
                    continue
                n = tool.get("finding_count")
                if n is None:
                    continue
                counts.append(int(n))
                if n >= 25:
                    at_cap += 1
                break
        if not counts:
            return {"fixture_count": 0}
        counts.sort()
        mid = len(counts) // 2
        return {
            "fixture_count": len(counts),
            "mean_findings": round(sum(counts) / len(counts), 2),
            "median_findings": counts[mid],
            "at_delivery_cap_25": at_cap,
        }

    repos = {s.get("repo") for s in scorecards}
    gold_with_primary = sum(1 for g in gold if any(d.get("ci_required") for d in g.get("defects", [])))
    return {
        "schema_version": "1.0.0",
        "generated_at_utc": datetime.now(timezone.utc).isoformat(),
        "sample_size": sum(1 for s in scorecards for d in s.get("defects", []) if d.get("ci_required", True)),
        "fixtures_scored": len(scorecards),
        "gold_fixtures": len(gold),
        "gold_with_ci_required": gold_with_primary,
        "repos_represented": len(repos),
        "evidence_tier": "measured" if len(gold) >= 15 and len(repos) >= 8 else "provisional",
        "comparison_scope": scope.get("comparison_scope", "diff_behavioral_review"),
        "scope_manifest": "eval/competitor-scope.json",
        "segments": {
            "anchor_only": {"fixtures": len(anchor), "recall_by_tool": recall_segment(anchor, "anchor_only")},
            "gold_cross_repo": {
                "fixtures": len(gold),
                "recall_by_tool": recall_segment(gold, "gold_cross_repo"),
                "recall_by_fn_class": recall_by_fn_class(gold, "gold_cross_repo"),
                "gauntletci_noise": gauntlet_gold_noise(gold),
            },
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
    scope = load_competitor_scope()
    tool_names = scoring_tool_names(scope)

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
        card = score_fixture(fid, gt, gci if gci.exists() else None, comp_dir, codescan, scope, tool_names)
        out_path = score_dir / f"{fid}.json"
        out_path.write_text(json.dumps(card, indent=2) + "\n", encoding="utf-8")
        scorecards.append(card)
        print(f"Wrote {out_path}")

    if scorecards:
        rollup_doc = rollup(scorecards, scope)
        rollup_path = score_dir / "competitive-suite.json"
        rollup_path.write_text(json.dumps(rollup_doc, indent=2) + "\n", encoding="utf-8")
        print(f"Wrote {rollup_path}")

        if args.check:
            for card in scorecards:
                if card.get("suite_tier") not in ("anchor", "gold"):
                    continue
                fid = card["fixture_id"]
                gci_run = EVAL / "runs" / "gauntletci" / f"{fid}.json"
                if not gci_run.exists():
                    latest = EVAL / "redis-2995-latest.json"
                    if not (fid == "stackexchange-redis-pr-2995" and latest.exists()):
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
#!/usr/bin/env python3
"""Build eval/rule-audit.json from rule sources and corpus.db metrics."""
from __future__ import annotations

import json
import os
import re
import sqlite3
import sys
from datetime import datetime, timezone
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from corpus_db_read import (
    compute_labeled_rule_metrics,
    ensure_read_indexes,
    fixtures_triggered_latest_run,
)

REPO = Path(__file__).resolve().parents[1]
RULES_DIR = REPO / "src" / "GauntletCI.Core" / "Rules" / "Implementations"
CORPUS = Path(os.environ.get("USERPROFILE", "")) / ".gauntletci" / "corpus.db"
CORPUS_PATH_DISPLAY = r"%USERPROFILE%\.gauntletci\corpus.db"
OUT = REPO / "eval" / "rule-audit.json"
FIXTURES_CSV = REPO / "data" / "corpus-fixtures.csv"

ROOT_CAUSES = {
    "RC-1": "Added-line treated as new risk (diff artifact blindness)",
    "RC-2": "Line-local pattern without method/control-flow scope",
    "RC-3": "No cross-entity reasoning (sibling methods, guard+use)",
    "RC-4": "Domain assumptions (web/DI/EF) on all repos",
    "RC-5": "Unbounded per-line fanout on large diffs",
    "RC-6": "Confidence/severity does not gate delivery",
    "RC-7": "Roslyn/semantics underused",
    "RC-8": "Success metric is pattern fire, not adjudicated defect",
}

PATTERN_DEFS = {
    "P1": "Added-line substring scan",
    "P2": "Removed-line substring scan",
    "P3": "File/diff aggregate (weak line anchor)",
    "P4": "Token/regex without method context",
    "P5": "Cross-entity compare (partial or required)",
    "P6": "Web/DI/HTTP/DB domain-specific",
    "P7": "Roslyn or syntax-guard augmented",
    "P8": "Removed+added pair compare",
    "P9": "Hunk-local removed vs added",
}

FN_CLASSES = [
    "inverted-condition",
    "sibling-implementation-drift",
    "guard-deletion-remote-use",
    "logic-bug-no-token",
    "cross-file-contract",
    "intentional-pattern",
]

FP_CLASSES = [
    "refactor-restructure",
    "library-factory-new",
    "intentional-swallow",
    "test-fixture-context",
    "framework-exempt-pair",
    "benign-token-use",
    "large-feature-volume",
]


def normalize_rule_id(rid: str) -> str:
    if rid.startswith("GCI") and len(rid) == 7 and rid[3:].isdigit():
        return f"GCI{int(rid[3:]):04d}"
    return rid


def load_corpus_metrics() -> dict[str, dict]:
    stats: dict[str, dict] = {}
    if not CORPUS.exists():
        return stats

    con = sqlite3.connect(CORPUS)
    cur = con.cursor()
    ensure_read_indexes(con)

    # Silver aggregates (rule-level precision/recall/usefulness)
    try:
        rows = cur.execute(
            """
            SELECT rule_id, tier, trigger_rate, precision_score, recall_score, usefulness_score
            FROM aggregates
            """
        ).fetchall()
        for rid, tier, trig, prec, rec, useful in rows:
            rid = normalize_rule_id(rid)
            stats[rid] = {
                "tier": tier,
                "trigger_rate": trig,
                "precision": round(float(prec), 3) if prec is not None else None,
                "recall": round(float(rec), 3) if rec is not None else None,
                "usefulness": round(float(useful), 3) if useful is not None else None,
            }
    except sqlite3.Error:
        pass

    # Latest audit snapshot TP/FP/FN (if present)
    try:
        snap = cur.execute(
            "SELECT id FROM audit_snapshots ORDER BY snapped_at_utc DESC LIMIT 1"
        ).fetchone()
        if snap:
            rows = cur.execute(
                """
                SELECT rule_id, tp, fp, fn, precision_score, recall_score, usefulness_score
                FROM audit_snapshot_rows
                WHERE snapshot_id = ?
                """,
                (snap[0],),
            ).fetchall()
            for rid, tp, fp, fn, prec, rec, useful in rows:
                rid = normalize_rule_id(rid)
                entry = stats.setdefault(rid, {})
                entry.update(
                    {
                        "tp": int(tp or 0),
                        "fp": int(fp or 0),
                        "fn": int(fn or 0),
                        "labeled_tp": int(tp or 0),
                        "labeled_fp": int(fp or 0),
                        "labeled_fn": int(fn or 0),
                        "labeled_precision": round(float(prec), 3) if prec is not None else None,
                        "labeled_recall": round(float(rec), 3) if rec is not None else None,
                        "snapshot_usefulness": round(float(useful), 3) if useful is not None else None,
                    }
                )
                if prec is not None:
                    entry["precision"] = round(float(prec), 3)
                if rec is not None:
                    entry["recall"] = round(float(rec), 3)
    except sqlite3.Error:
        pass

    # Expected vs actual trigger counts (label coverage signal)
    try:
        rows = cur.execute(
            """
            SELECT rule_id, COUNT(*)
            FROM expected_findings
            WHERE should_trigger = 1 AND COALESCE(is_inconclusive, 0) = 0
            GROUP BY rule_id
            """
        ).fetchall()
        for rid, c in rows:
            rid = normalize_rule_id(rid)
            stats.setdefault(rid, {})["expected_triggers"] = int(c)
    except sqlite3.Error:
        pass

    try:
        for rid, count in fixtures_triggered_latest_run(cur).items():
            rid = normalize_rule_id(rid)
            stats.setdefault(rid, {})["fixtures_triggered_latest_run"] = count
    except sqlite3.Error:
        pass

    try:
        rows = cur.execute(
            """
            SELECT rule_id, COUNT(*)
            FROM actual_findings
            WHERE did_trigger = 1
            GROUP BY rule_id
            """
        ).fetchall()
        for rid, c in rows:
            rid = normalize_rule_id(rid)
            stats.setdefault(rid, {})["actual_triggers_all_runs"] = int(c)
    except sqlite3.Error:
        pass

    # Per-fixture labeled TP/FN/FP: use audit snapshot when available (fast path).
    # Full recompute from actual_findings is optional via --full-corpus flag.
    con.close()
    return stats


def load_labeled_classifier_metrics() -> dict[str, dict]:
    """Recompute labeled TP/FP/FN from expected_findings (same logic as audit snapshot)."""
    if not CORPUS.exists():
        return {}
    con = sqlite3.connect(CORPUS)
    ensure_read_indexes(con)
    try:
        labeled = compute_labeled_rule_metrics(con.cursor())
    except sqlite3.Error:
        labeled = {}
    con.close()
    return {normalize_rule_id(rid): metrics for rid, metrics in labeled.items()}


def load_fixture_rule_frequency() -> dict[str, int]:
    freq: dict[str, int] = {}
    if not FIXTURES_CSV.exists():
        return freq
    for line in FIXTURES_CSV.read_text(encoding="utf-8").splitlines()[1:]:
        parts = line.split(",")
        if len(parts) < 8:
            continue
        rules_field = parts[7]
        for token in rules_field.split(";"):
            token = token.strip()
            if token.startswith("GCI"):
                rid = normalize_rule_id(token)
                freq[rid] = freq.get(rid, 0) + 1
    return freq


def tag_patterns(text: str) -> list[str]:
    tags: list[str] = []
    if "AddedLines" in text or "DiffLineKind.Added" in text:
        tags.append("P1")
    if "RemovedLines" in text or "DiffLineKind.Removed" in text:
        tags.append("P2")
    if re.search(r"CreateFinding\(\s*\n\s*(string|\$)", text) or "CheckLogicRemovedWithoutTests" in text:
        tags.append("P3")
    if "Contains(" in text or "Regex" in text or "IsMatch" in text:
        tags.append("P4")
    if any(k in text for k in ("removedContent", "addedContent", "Compare", "SingleNode", "MultiNode")):
        tags.append("P5")
    if any(
        k in text
        for k in (
            "HttpPost",
            "AddSingleton",
            "AddScoped",
            "GetService",
            "ServiceLocator",
            "INSERT INTO",
            "AddTransient",
        )
    ):
        tags.append("P6")
    if any(k in text for k in ("AddRoslynFindings", "context.StaticAnalysis", "context.Syntax", "CSharpSyntaxTree")):
        tags.append("P7")
    if "removedContent" in text and "addedContent" in text:
        tags.append("P8")
    if "hunk" in text.lower() and ("removedLine" in text or "addedLine" in text):
        tags.append("P9")
    return [p for p in ("P1", "P2", "P3", "P4", "P5", "P6", "P7", "P8", "P9") if p in tags]


def infer_domain(text: str, name: str) -> str:
    if any(k in text for k in ("HttpPost", "AddSingleton", "GetService", "ServiceLocator")):
        return "web-di"
    if "INSERT INTO" in text or "Sql" in name or "Schema" in name:
        return "db"
    if "Layer" in name or "Architecture" in name:
        return "layered-app"
    if "Test" in name or "Lockfile" in name:
        return "repo-meta"
    return "general"


def infer_root_causes(tags: list[str], text: str, domain: str, uses_roslyn: bool) -> list[str]:
    rcs: list[str] = []
    if "P1" in tags:
        rcs.extend(["RC-1", "RC-8"])
    if "P4" in tags and "P7" not in tags:
        rcs.append("RC-2")
    if "P5" not in tags or ("override" not in text and "removedContent" not in text):
        if any(k in text for k in ("if (", "while (", "catch", "IsSubscriberConnected")):
            rcs.append("RC-3")
    if domain in ("web-di", "layered-app"):
        rcs.append("RC-4")
    if "foreach (var line in file.AddedLines)" in text or text.count("CreateFinding") >= 4:
        rcs.append("RC-5")
    rcs.append("RC-6")
    if not uses_roslyn:
        rcs.append("RC-7")
    rcs.append("RC-8")
    return sorted(set(rcs))


def requires_cross_method(tags: list[str], text: str) -> bool:
    if any(k in text.lower() for k in ("condition", "catch", "async", "lock", "subscribe", "remove")):
        return "P5" not in tags or tags.count("P5") == 1 and "removedContent" not in text
    return False


def head_to_head_risk(tags: list[str], domain: str, corpus: dict, fixture_freq: int) -> str:
    score = 0
    if "P1" in tags and "P5" not in tags:
        score += 2
    if "P4" in tags and "P7" not in tags:
        score += 2
    if domain == "web-di":
        score += 2
    prec = corpus.get("labeled_precision", corpus.get("precision"))
    fp = corpus.get("labeled_fp", corpus.get("fp", 0))
    if prec is not None and prec < 0.25:
        score += 2
    elif fp and fp > 20:
        score += 1
    if fixture_freq > 100:
        score += 1
    if corpus.get("fixtures_triggered_latest_run", 0) > 80 and (prec is None or prec < 0.5):
        score += 1
    if score >= 5:
        return "critical"
    if score >= 3:
        return "high"
    if score >= 1:
        return "medium"
    return "low"


def known_fp_classes(tags: list[str], domain: str, name: str) -> list[str]:
    fps: list[str] = []
    if "P1" in tags:
        fps.append("refactor-restructure")
    if domain == "web-di":
        fps.extend(["library-factory-new", "large-feature-volume"])
    if "Error Handling" in name or "GCI0007" in name:
        fps.append("intentional-swallow")
    if "Pattern Consistency" in name or "Idempotency" in name:
        fps.append("framework-exempt-pair")
    if "P4" in tags:
        fps.append("benign-token-use")
    return sorted(set(fps))


def known_fn_classes(tags: list[str], requires_xmethod: bool) -> list[str]:
    fns: list[str] = []
    if requires_xmethod:
        fns.extend(["inverted-condition", "sibling-implementation-drift", "logic-bug-no-token"])
    if "P2" in tags and "P8" not in tags:
        fns.append("guard-deletion-remote-use")
    return sorted(set(fns))


def priority_tier(risk: str, corpus: dict) -> int:
    base = {"critical": 1, "high": 2, "medium": 3, "low": 4}[risk]
    fp = corpus.get("labeled_fp", corpus.get("fp", 0))
    if fp and fp > 50:
        return max(1, base - 1)
    return base


def analyze_rule(path: Path, corpus_stats: dict, fixture_freq: dict) -> dict:
    text = path.read_text(encoding="utf-8")
    rid_m = re.search(r'Id => "(GCI\d+)"', text)
    name_m = re.search(r'Name => "([^"]+)"', text)
    rid = normalize_rule_id(rid_m.group(1)) if rid_m else path.stem.split("_")[0]
    name = name_m.group(1) if name_m else path.stem
    tags = tag_patterns(text)
    domain = infer_domain(text, name)
    uses_roslyn = "AddRoslynFindings" in text or "context.Syntax" in text
    added_only = "AddedLines" in text and "RemovedLines" not in text
    cross_method = requires_cross_method(tags, text)
    corpus = corpus_stats.get(rid, {})
    ff = fixture_freq.get(rid, 0)
    h2h = head_to_head_risk(tags, domain, corpus, ff)
    fanout = "low"
    if "foreach (var line in file.AddedLines)" in text:
        fanout = "high"
    elif "AddedLines" in text and text.count("CreateFinding") > 3:
        fanout = "medium"

    return {
        "rule_id": rid,
        "name": name,
        "source_file": str(path.relative_to(REPO)).replace("\\", "/"),
        "primary_patterns": tags,
        "pattern_notes": {p: PATTERN_DEFS[p] for p in tags},
        "domain": domain,
        "uses_roslyn": uses_roslyn,
        "added_lines_only": added_only,
        "requires_cross_method": cross_method,
        "requires_removed_added_pair": "P8" in tags or "P9" in tags,
        "typical_fanout": fanout,
        "root_causes": infer_root_causes(tags, text, domain, uses_roslyn),
        "known_fp_classes": known_fp_classes(tags, domain, name),
        "known_fn_classes": known_fn_classes(tags, cross_method),
        "head_to_head_risk": h2h,
        "priority_tier": priority_tier(h2h, corpus),
        "corpus": corpus,
        "fixture_trigger_count": ff,
        "audit_status": "auto-tagged",
        "human_review_required": cross_method or h2h in ("critical", "high"),
    }


def main() -> None:
    import sys

    full_corpus = "--full-corpus" in sys.argv
    corpus_stats = load_corpus_metrics()
    if full_corpus:
        labeled = load_labeled_classifier_metrics()
        for rid, extra in labeled.items():
            corpus_stats.setdefault(rid, {}).update(extra)
    fixture_freq = load_fixture_rule_frequency()
    rules = [analyze_rule(p, corpus_stats, fixture_freq) for p in sorted(RULES_DIR.glob("GCI*.cs"))]
    rules.sort(key=lambda r: (r["priority_tier"], r["rule_id"]))

    # Corpus-derived systemic issues
    core_issues: list[dict] = []
    by_fp = sorted(
        [r for r in rules if r["corpus"].get("labeled_fp")],
        key=lambda r: r["corpus"]["labeled_fp"],
        reverse=True,
    )
    for r in by_fp[:8]:
        core_issues.append(
            {
                "issue": "high_labeled_false_positives",
                "rule_id": r["rule_id"],
                "name": r["name"],
                "labeled_fp": r["corpus"]["labeled_fp"],
                "labeled_precision": r["corpus"].get("labeled_precision"),
                "actual_triggers": r["corpus"].get("fixtures_triggered_latest_run"),
                "primary_patterns": r["primary_patterns"],
            }
        )

    low_prec = sorted(
        [
            r
            for r in rules
            if r["corpus"].get("labeled_precision") is not None
            and r["corpus"]["labeled_precision"] < 0.2
            and r["corpus"].get("labeled_fp", 0) >= 5
        ],
        key=lambda r: r["corpus"]["labeled_precision"],
    )
    for r in low_prec[:8]:
        core_issues.append(
            {
                "issue": "low_labeled_precision",
                "rule_id": r["rule_id"],
                "name": r["name"],
                "labeled_precision": r["corpus"]["labeled_precision"],
                "labeled_recall": r["corpus"].get("labeled_recall"),
                "labeled_fp": r["corpus"].get("labeled_fp"),
            }
        )

    high_trigger_noise = sorted(
        [
            r
            for r in rules
            if r["corpus"].get("fixtures_triggered_latest_run", 0) > 80
            and (r["corpus"].get("usefulness") or 0) < 0.5
        ],
        key=lambda r: r["corpus"].get("fixtures_triggered_latest_run", 0),
        reverse=True,
    )
    for r in high_trigger_noise[:6]:
        core_issues.append(
            {
                "issue": "high_volume_low_usefulness",
                "rule_id": r["rule_id"],
                "name": r["name"],
                "fixtures_triggered": r["corpus"].get("fixtures_triggered_latest_run"),
                "usefulness": r["corpus"].get("usefulness"),
                "typical_fanout": r["typical_fanout"],
            }
        )

    cross_method_no_p5 = [
        r["rule_id"]
        for r in rules
        if r["requires_cross_method"] and "P5" not in r["primary_patterns"]
    ]
    core_issues.append(
        {
            "issue": "structural_fn_risk_cross_method_without_p5",
            "rule_count": len(cross_method_no_p5),
            "rule_ids": cross_method_no_p5,
            "description": "Rules touching control-flow semantics but lacking cross-entity compare (Redis MultiNode class)",
        }
    )

    domain_mismatch = [
        {
            "rule_id": r["rule_id"],
            "name": r["name"],
            "domain": r["domain"],
            "labeled_fp": r["corpus"].get("labeled_fp"),
        }
        for r in rules
        if r["domain"] == "web-di" and r["corpus"].get("labeled_fp", 0) > 10
    ]
    core_issues.append(
        {
            "issue": "web_di_domain_mismatch_on_general_repos",
            "rules": domain_mismatch,
            "description": "DI/HTTP rules firing heavily on non-web corpus fixtures (library/infra PRs)",
        }
    )

    priority_fix_order = [
        {
            "rank": i + 1,
            "rule_id": r["rule_id"],
            "name": r["name"],
            "head_to_head_risk": r["head_to_head_risk"],
            "priority_tier": r["priority_tier"],
            "top_root_causes": r["root_causes"][:4],
            "corpus_fp": r["corpus"].get("labeled_fp", r["corpus"].get("fp")),
            "corpus_precision": r["corpus"].get("labeled_precision", r["corpus"].get("precision")),
            "corpus_recall": r["corpus"].get("labeled_recall", r["corpus"].get("recall")),
            "fixtures_triggered": r["corpus"].get("fixtures_triggered_latest_run"),
            "actual_triggers_all_runs": r["corpus"].get("actual_triggers_all_runs"),
        }
        for i, r in enumerate(rules)
        if r["head_to_head_risk"] in ("critical", "high") or r["priority_tier"] <= 2
    ][:20]

    platform_gaps = [
        {
            "gap_id": "PG-RELATION",
            "title": "Sibling / paired-implementation compare",
            "root_cause": "RC-3",
            "rules_blocked": [r["rule_id"] for r in rules if r["requires_cross_method"]],
            "fix": "New cross-method layer: same override name, opposite boolean polarity, method name vs condition",
        },
        {
            "gap_id": "PG-PROVENANCE",
            "title": "Line provenance (move vs net-new)",
            "root_cause": "RC-1",
            "rules_blocked": [r["rule_id"] for r in rules if "P1" in r["primary_patterns"]],
            "fix": "Similarity match added hunks to removed hunks before firing",
        },
        {
            "gap_id": "PG-DOMAIN",
            "title": "Repo/domain classifier",
            "root_cause": "RC-4",
            "rules_blocked": [r["rule_id"] for r in rules if r["domain"] in ("web-di", "layered-app")],
            "fix": "Suppress or downgrade DI/HTTP rules on library/infrastructure repos",
        },
        {
            "gap_id": "PG-DELIVERY",
            "title": "Ranked actionable output",
            "root_cause": "RC-5, RC-6",
            "rules_blocked": ["ALL"],
            "fix": "Per-rule caps + move SilverLabel coordinations to RuleOrchestrator post-process",
        },
        {
            "gap_id": "PG-SEMANTICS",
            "title": "Method-scoped Roslyn + counterfactuals",
            "root_cause": "RC-7",
            "rules_blocked": [r["rule_id"] for r in rules if not r["uses_roslyn"] and "P4" in r["primary_patterns"]],
            "fix": "Wire PatchCounterfactualGenerator; bind P4 tokens to control-flow sites",
        },
    ]

    doc = {
        "schema_version": "1.0.0",
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "corpus_path": CORPUS_PATH_DISPLAY,
        "corpus_loaded": CORPUS.exists(),
        "rule_count": len(rules),
        "pattern_taxonomy": PATTERN_DEFS,
        "root_cause_taxonomy": ROOT_CAUSES,
        "fn_class_taxonomy": FN_CLASSES,
        "fp_class_taxonomy": FP_CLASSES,
        "platform_gaps": platform_gaps,
        "core_issues_from_corpus": core_issues,
        "priority_fix_order": priority_fix_order,
        "rules": rules,
        "eval_notes": {
            "regenerate": "python scripts/build-rule-audit.py",
            "regenerate_full_labeled_metrics": "python scripts/build-rule-audit.py --full-corpus",
            "corpus_default": "%USERPROFILE%\\.gauntletci\\corpus.db",
            "metrics_sources": [
                "aggregates (precision/recall/usefulness)",
                "audit_snapshot_rows (labeled tp/fp/fn when snapshot exists)",
                "fixtures_triggered_latest_run (distinct fixtures, latest completed run)",
                "corpus_db_read.ensure_read_indexes (agent DB read performance)",
            ],
            "redis_2995_regression": {
                "adjudicated_defect": "MultiNodeSubscription.RemoveDisconnectedEndpoints inverted IsSubscriberConnected",
                "miss_pattern": "P5 sibling-implementation (gap PG-RELATION)",
                "gold_fixture_id": "redis-2995-multinode-inverted",
                "primary_rule": "GCI0058",
                "noise_example_rules": ["GCI0038", "GCI0006", "GCI0043"],
            },
            "audit_checklist_per_rule": [
                "Tag P1-P9",
                "Set requires_cross_method",
                "Record corpus precision/fp",
                "Add TP/FP/FN fixture triple",
                "Score head_to_head_risk after human review",
            ],
        },
    }

    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(json.dumps(doc, indent=2) + "\n", encoding="utf-8")
    print(f"Wrote {OUT} ({len(rules)} rules, corpus rules: {len(corpus_stats)})")


if __name__ == "__main__":
    main()

#!/usr/bin/env python3
"""One-off corpus stats for adversarial audit. Reads ~/.gauntletci/corpus.db by default."""
import argparse
import json
import os
import sqlite3
from collections import Counter
from pathlib import Path

parser = argparse.ArgumentParser(description="Summarize corpus.db metrics for audits.")
parser.add_argument(
    "--db",
    default=str(Path.home() / ".gauntletci" / "corpus.db"),
    help="Path to corpus SQLite database (default: ~/.gauntletci/corpus.db)",
)
args = parser.parse_args()
db = args.db
if not os.path.exists(db):
    print(json.dumps({"error": "corpus.db not found", "path": db}))
    raise SystemExit(1)

conn = sqlite3.connect(db)
cur = conn.cursor()

def table_exists(name: str) -> bool:
    return cur.execute(
        "SELECT 1 FROM sqlite_master WHERE type='table' AND name=?", (name,)
    ).fetchone() is not None

out: dict = {}
out["tables"] = [
    r[0] for r in cur.execute(
        "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name"
    ).fetchall()
]

if table_exists("fixtures"):
    out["fixture_count"] = cur.execute("SELECT COUNT(*) FROM fixtures").fetchone()[0]

# Discover label-related tables
label_tables = [
    t for t in out.get("tables", [])
    if "label" in t.lower() or "finding" in t.lower() or "rule" in t.lower()
]
out["label_related_tables"] = label_tables

if table_exists("labeled_findings"):
    rows = cur.execute(
        "SELECT rule_id, label, COUNT(*) FROM labeled_findings GROUP BY rule_id, label"
    ).fetchall()
    by_rule: dict = {}
    fp_rules: dict = {}
    tp_rules: dict = {}
    for rule_id, label, count in rows:
        lab = (label or "").lower()
        by_rule.setdefault(rule_id, {})[label] = count
        if lab in ("fp", "false_positive", "false positive"):
            fp_rules[rule_id] = fp_rules.get(rule_id, 0) + count
        if lab in ("tp", "true_positive", "true positive"):
            tp_rules[rule_id] = tp_rules.get(rule_id, 0) + count

    out["rules_with_labels"] = len(by_rule)
    out["top_fp_rules"] = dict(Counter(fp_rules).most_common(15))
    out["top_tp_rules"] = dict(Counter(tp_rules).most_common(15))
    out["rules_low_tp_high_fp"] = []
    for rule_id in sorted(by_rule.keys()):
        tp = tp_rules.get(rule_id, 0)
        fp = fp_rules.get(rule_id, 0)
        total = tp + fp
        if total >= 5 and fp > tp:
            out["rules_low_tp_high_fp"].append(
                {"rule_id": rule_id, "tp": tp, "fp": fp, "fp_rate": round(fp / total, 2)}
            )
    out["rules_low_tp_high_fp"].sort(key=lambda x: x["fp_rate"], reverse=True)

if table_exists("aggregates"):
    rows = cur.execute(
        """SELECT rule_id, tier, precision_score, recall_score, trigger_rate
           FROM aggregates ORDER BY precision_score ASC, trigger_rate DESC"""
    ).fetchall()
    out["aggregates_low_precision"] = [
        {
            "rule_id": r[0],
            "tier": r[1],
            "precision": r[2],
            "recall": r[3],
            "trigger_rate": r[4],
        }
        for r in rows
        if r[2] is not None and r[2] < 0.25
    ][:20]
    out["aggregates_all_count"] = len(rows)

if table_exists("expected_findings"):
    exp = cur.execute(
        """SELECT rule_id,
                  SUM(CASE WHEN should_trigger=1 THEN 1 ELSE 0 END) as should,
                  COUNT(*) as total
           FROM expected_findings GROUP BY rule_id ORDER BY should DESC"""
    ).fetchall()
    out["expected_should_trigger_top"] = {
        r[0]: {"should_trigger": r[1], "total_labels": r[2]} for r in exp[:20]
    }

if table_exists("actual_findings"):
    act = cur.execute(
        """SELECT rule_id, COUNT(*) FROM actual_findings
           WHERE did_trigger=1 GROUP BY rule_id ORDER BY 2 DESC LIMIT 20"""
    ).fetchall()
    out["actual_triggers_top"] = dict(act)

if table_exists("evaluations"):
    out["evaluation_count"] = cur.execute("SELECT COUNT(*) FROM evaluations").fetchone()[0]

conn.close()
print(json.dumps(out, indent=2))

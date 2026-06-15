#!/usr/bin/env python3
"""Shared helpers for eval/rule-audit.json export and drift checks."""
from __future__ import annotations

import json
import re
import sqlite3
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
RULE_AUDIT_JSON = REPO / "eval" / "rule-audit.json"

RULE_ID_RE = re.compile(r"^GCI\d{4}$")


def load_rule_audit_json(path: Path = RULE_AUDIT_JSON) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def validate_rule_audit_json(doc: dict) -> list[str]:
    """Structural checks for eval/rule-audit.json (CI-safe, no DB)."""
    errors: list[str] = []

    schema_version = doc.get("schema_version")
    if not isinstance(schema_version, str) or not schema_version:
        errors.append("schema_version must be a non-empty string")

    if not isinstance(doc.get("generated_at"), str) or not doc["generated_at"]:
        errors.append("generated_at must be a non-empty ISO timestamp string")

    rule_count = doc.get("rule_count")
    rules = doc.get("rules")
    if not isinstance(rules, list) or not rules:
        errors.append("rules must be a non-empty list")
    else:
        if isinstance(rule_count, int) and rule_count != len(rules):
            errors.append(f"rule_count ({rule_count}) must match len(rules) ({len(rules)})")

        seen_ids: set[str] = set()
        for index, rule in enumerate(rules):
            if not isinstance(rule, dict):
                errors.append(f"rules[{index}] must be an object")
                continue
            rule_id = rule.get("rule_id")
            if not isinstance(rule_id, str) or not RULE_ID_RE.match(rule_id):
                errors.append(f"rules[{index}].rule_id must match GCI####")
            elif rule_id in seen_ids:
                errors.append(f"duplicate rules rule_id {rule_id}")
            else:
                seen_ids.add(rule_id)

            corpus = rule.get("corpus")
            if corpus is not None and not isinstance(corpus, dict):
                errors.append(f"rules[{index}].corpus must be an object when present")

            labeled_precision = (corpus or {}).get("labeled_precision")
            if labeled_precision is not None and not isinstance(labeled_precision, (int, float)):
                errors.append(f"rules[{index}].corpus.labeled_precision must be numeric")

    return errors


def extract_labeled_metrics(doc: dict) -> dict[str, dict]:
    """Labeled TP/FP/FN/precision from each rule's corpus block."""
    metrics: dict[str, dict] = {}
    for rule in doc.get("rules", []):
        if not isinstance(rule, dict):
            continue
        rule_id = rule.get("rule_id")
        corpus = rule.get("corpus")
        if not isinstance(rule_id, str) or not isinstance(corpus, dict):
            continue
        if corpus.get("labeled") is None and corpus.get("labeled_tp") is None:
            continue
        entry: dict = {}
        for key in ("labeled", "labeled_tp", "labeled_fp", "labeled_fn", "labeled_precision"):
            if key in corpus and corpus[key] is not None:
                entry[key] = corpus[key]
        if entry:
            metrics[rule_id] = entry
    return metrics


def load_db_labeled_metrics(db_path: Path) -> dict[str, dict]:
    import sys

    sys.path.insert(0, str(REPO / "scripts"))
    from corpus_db_read import compute_labeled_rule_metrics, ensure_read_indexes  # noqa: E402

    con = sqlite3.connect(db_path)
    ensure_read_indexes(con)
    labeled = compute_labeled_rule_metrics(con.cursor())
    con.close()
    return labeled

#!/usr/bin/env python3
"""Check eval/rule-audit.json labeled metrics match agent corpus DB (when present)."""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
DEFAULT_DB = Path.home() / ".gauntletci" / "corpus.db"

sys.path.insert(0, str(REPO / "scripts"))
from rule_audit_lib import (  # noqa: E402
    RULE_AUDIT_JSON,
    extract_labeled_metrics,
    load_db_labeled_metrics,
    load_rule_audit_json,
    validate_rule_audit_json,
)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--db", default=str(DEFAULT_DB))
    parser.add_argument(
        "--json",
        default=str(RULE_AUDIT_JSON),
        help="Rule audit JSON path",
    )
    parser.add_argument(
        "--skip-if-missing-db",
        action="store_true",
        help="Exit 0 when agent corpus DB is absent (CI)",
    )
    args = parser.parse_args()

    json_path = Path(args.json)
    if not json_path.exists():
        raise SystemExit(f"Missing rule audit JSON: {json_path}")

    doc = load_rule_audit_json(json_path)
    schema_errors = validate_rule_audit_json(doc)
    if schema_errors:
        for line in schema_errors:
            print(f"FAIL {line}", file=sys.stderr)
        raise SystemExit(1)

    expected = extract_labeled_metrics(doc)

    db_path = Path(args.db)
    if not db_path.exists():
        if args.skip_if_missing_db:
            print(
                f"OK rule audit JSON schema ({len(doc.get('rules', []))} rules, "
                f"{len(expected)} with labeled metrics); skip DB compare (no agent corpus in CI)"
            )
            return
        raise SystemExit(f"Corpus DB not found: {db_path}")

    actual = load_db_labeled_metrics(db_path)
    errors: list[str] = []

    for rule_id, exp in expected.items():
        db_entry = actual.get(rule_id)
        if not db_entry:
            continue
        for field in ("labeled_tp", "labeled_fp", "labeled_fn", "labeled_precision"):
            if field not in exp:
                continue
            exp_val = exp[field]
            db_val = db_entry.get(field)
            if db_val is None and field == "labeled_precision" and exp_val == 0:
                continue
            if db_val is not None and exp_val != db_val:
                errors.append(f"{rule_id} {field} json={exp_val} db={db_val}")

    if errors:
        for line in errors:
            print(f"FAIL {line}", file=sys.stderr)
        print(
            "Regenerate with: python scripts/build-rule-audit.py --full-corpus",
            file=sys.stderr,
        )
        raise SystemExit(1)

    print(
        f"OK rule audit labeled metrics match {db_path} "
        f"({len(expected)} rules with labeled corpus metrics)"
    )


if __name__ == "__main__":
    main()

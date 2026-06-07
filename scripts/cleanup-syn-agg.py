#!/usr/bin/env python3
"""Remove stale GCI_SYN_AGG from corpus-fixtures.csv and recompute derived columns."""
from __future__ import annotations

import csv
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
CSV_PATH = REPO / "data" / "corpus-fixtures.csv"
FIELDNAMES = [
    "repo",
    "pr_number",
    "pr_size_bucket",
    "has_tests_changed",
    "has_review_comments",
    "total_bcr_findings",
    "high_confidence_bcr_findings",
    "distinct_rules_triggered",
    "rule_ids_triggered",
    "high_confidence_rule_ids_triggered",
    "analyzed_at_utc",
]


def clean_row(row: dict[str, str]) -> bool:
    """Returns True when the row was modified."""
    rule_ids = [part for part in row["rule_ids_triggered"].split(";") if part]
    if "GCI_SYN_AGG" not in rule_ids:
        return False

    rule_ids = [part for part in rule_ids if part != "GCI_SYN_AGG"]
    row["rule_ids_triggered"] = ";".join(rule_ids)
    row["distinct_rules_triggered"] = str(len(rule_ids))
    row["total_bcr_findings"] = str(max(0, int(row["total_bcr_findings"]) - 1))

    if not rule_ids:
        row["total_bcr_findings"] = "0"
        row["high_confidence_bcr_findings"] = "0"

    return True


def main() -> None:
    with CSV_PATH.open(newline="", encoding="utf-8") as handle:
        rows = list(csv.DictReader(handle))

    changed = sum(clean_row(row) for row in rows)
    if changed == 0:
        print("No GCI_SYN_AGG rows found; nothing to do.")
        return

    with CSV_PATH.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(handle, fieldnames=FIELDNAMES, lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)

    print(f"Updated {changed} rows in {CSV_PATH.relative_to(REPO)}")


if __name__ == "__main__":
    main()

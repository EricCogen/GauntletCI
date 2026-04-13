from __future__ import annotations

import argparse
import math
import sqlite3
from pathlib import Path

from config_loader import load_config
from db import connect
from store import init_app_tables


def main() -> None:
    parser = argparse.ArgumentParser(description="Seed label_queue from actual_findings.")
    parser.add_argument("--limit", type=int, default=150, help="Total fired items to queue.")
    parser.add_argument("--include-nonfired", type=int, default=30, help="How many non-fired tasks to synthesize.")
    parser.add_argument("--rules", default="", help="Comma-separated rule ids to target.")
    parser.add_argument("--language", default="C#", help="Only queue fixtures whose primary language matches (default: C#). Pass empty string to disable filter.")
    args = parser.parse_args()

    base_dir = Path(__file__).resolve().parent
    config = load_config(base_dir)
    conn = connect(config["database_path"])
    init_app_tables(conn)

    target_rules = [x.strip() for x in args.rules.split(",") if x.strip()]
    lang_filter = args.language.strip()

    # Build WHERE clause for fired query
    conditions: list[str] = []
    params: list[object] = []
    if target_rules:
        conditions.append(f"af.rule_id IN ({','.join(['?'] * len(target_rules))})")
        params.extend(target_rules)
    if lang_filter:
        conditions.append("LOWER(f.language) = LOWER(?)")
        params.append(lang_filter)
    conditions.append("af.did_trigger = 1")
    where = "WHERE " + " AND ".join(conditions)

    fired_sql = f"""
    WITH grouped AS (
        SELECT af.fixture_id,
               af.rule_id,
               COUNT(*) AS finding_count,
               MAX(af.actual_confidence) AS max_confidence,
               f.has_tests_changed,
               f.has_review_comments,
               f.pr_size_bucket
        FROM actual_findings af
        JOIN fixtures f ON f.fixture_id = af.fixture_id
        {where}
        GROUP BY af.fixture_id, af.rule_id
    )
    SELECT *
    FROM grouped
    ORDER BY finding_count DESC, max_confidence DESC
    LIMIT ?
    """

    sql_params = params.copy()
    sql_params.append(args.limit)
    fired_rows = conn.execute(fired_sql, tuple(sql_params)).fetchall()

    for row in fired_rows:
        bucket = "fired_high_signal" if row["finding_count"] >= 3 else "fired"
        priority = int(row["finding_count"] * 10 + row["max_confidence"] * 10)
        conn.execute(
            """
            INSERT OR IGNORE INTO label_queue (fixture_id, rule_id, queue_bucket, fired, priority)
            VALUES (?, ?, ?, 1, ?)
            """,
            (row["fixture_id"], row["rule_id"], bucket, priority),
        )

    nonfired_limit = max(args.include_nonfired, 0)
    if nonfired_limit:
        if target_rules:
            rules = target_rules
        else:
            rules = [r[0] for r in conn.execute("SELECT DISTINCT rule_id FROM actual_findings ORDER BY rule_id").fetchall()]
        per_rule = max(1, math.ceil(nonfired_limit / max(len(rules), 1)))

        # Build language filter clause for nonfired probes
        nonfired_lang_clause = "AND LOWER(f.language) = LOWER(?)" if lang_filter else ""
        nonfired_lang_params: list[object] = [lang_filter] if lang_filter else []

        for rule_id in rules:
            rows = conn.execute(
                f"""
                SELECT f.fixture_id
                FROM fixtures f
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM actual_findings af
                    WHERE af.fixture_id = f.fixture_id AND af.rule_id = ? AND af.did_trigger = 1
                )
                {nonfired_lang_clause}
                ORDER BY f.has_review_comments DESC, f.has_tests_changed ASC, f.created_at_utc DESC
                LIMIT ?
                """,
                (rule_id, *nonfired_lang_params, per_rule),
            ).fetchall()
            for row in rows:
                conn.execute(
                    """
                    INSERT OR IGNORE INTO label_queue (fixture_id, rule_id, queue_bucket, fired, priority)
                    VALUES (?, ?, 'nonfired_probe', 0, 1)
                    """,
                    (row["fixture_id"], rule_id),
                )

    conn.commit()
    total = conn.execute("SELECT COUNT(*) FROM label_queue").fetchone()[0]
    print(f"Queue ready. Total tasks in label_queue: {total}")


if __name__ == "__main__":
    main()

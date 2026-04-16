from __future__ import annotations

import argparse
import math

from pathlib import Path

from config_loader import load_config
from db import connect
from store import init_app_tables


def _build_fixture_filters(language: str, tiers: list[str], alias: str = "f") -> tuple[list[str], list[object]]:
    clauses: list[str] = []
    params: list[object] = []

    if language:
        clauses.append(f"{alias}.language COLLATE NOCASE = ?")
        params.append(language)

    if tiers:
        clauses.append(f"{alias}.tier COLLATE NOCASE IN ({','.join(['?'] * len(tiers))})")
        params.extend(tiers)

    return clauses, params


def _build_rule_filter(target_rules: list[str], alias: str = "af") -> tuple[str, list[object]]:
    if not target_rules:
        return "", []

    return f"{alias}.rule_id IN ({','.join(['?'] * len(target_rules))})", list(target_rules)


def _active_queue_counts(
    conn,
    target_rules: list[str],
    language: str,
    tiers: list[str],
) -> tuple[int, int]:
    clauses = ["lq.status IN ('pending', 'in_progress')"]
    params: list[object] = []

    if target_rules:
        clauses.append(f"lq.rule_id IN ({','.join(['?'] * len(target_rules))})")
        params.extend(target_rules)

    fixture_clauses, fixture_params = _build_fixture_filters(language, tiers)
    clauses.extend(fixture_clauses)
    params.extend(fixture_params)

    row = conn.execute(
        f"""
        SELECT
            COALESCE(SUM(CASE WHEN lq.fired = 1 THEN 1 ELSE 0 END), 0) AS active_fired,
            COALESCE(SUM(CASE WHEN lq.fired = 0 THEN 1 ELSE 0 END), 0) AS active_nonfired
        FROM label_queue lq
        JOIN fixtures f ON f.fixture_id = lq.fixture_id
        WHERE {' AND '.join(clauses)}
        """,
        tuple(params),
    ).fetchone()

    return int(row["active_fired"]), int(row["active_nonfired"])


def seed(
    conn,
    limit: int = 150,
    include_nonfired: int = 30,
    rules: list[str] | None = None,
    language: str = "C#",
    tiers: str = "all",
) -> int:
    """Seed label_queue from actual_findings. Returns total tasks in queue after seeding.

    Safe to call on every startup — uses INSERT OR IGNORE so existing entries are preserved.
    """
    init_app_tables(conn)

    target_rules = rules or []
    lang_filter = language.strip()
    tier_filter: list[str] = []
    if tiers.strip().lower() != "all":
        tier_filter = [t.strip().lower() for t in tiers.split(",") if t.strip()]

    active_fired, active_nonfired = _active_queue_counts(conn, target_rules, lang_filter, tier_filter)
    fired_limit = max(limit - active_fired, 0)
    nonfired_limit = max(include_nonfired - active_nonfired, 0)

    if fired_limit == 0 and nonfired_limit == 0:
        total = conn.execute("SELECT COUNT(*) FROM label_queue").fetchone()[0]
        print(
            f"[seed] Queue already has enough active work (fired={active_fired}, nonfired={active_nonfired}). Skipping reseed."
        )
        return total

    if active_fired or active_nonfired:
        print(
            f"[seed] Active queue already contains fired={active_fired}, nonfired={active_nonfired}. "
            f"Topping off fired={fired_limit}, nonfired={nonfired_limit}."
        )

    rule_clause, rule_params = _build_rule_filter(target_rules)
    fixture_clauses, fixture_params = _build_fixture_filters(lang_filter, tier_filter)

    fired_conditions = ["af.did_trigger = 1"]
    fired_params: list[object] = []
    if rule_clause:
        fired_conditions.append(rule_clause)
        fired_params.extend(rule_params)
    fired_conditions.extend(fixture_clauses)
    fired_params.extend(fixture_params)
    fired_where = " AND ".join(fired_conditions)

    if fired_limit > 0:
        conn.execute(
            f"""
            WITH grouped AS (
                SELECT
                    af.fixture_id,
                    af.rule_id,
                    COUNT(*) AS finding_count,
                    COALESCE(MAX(af.actual_confidence), 0) AS max_confidence
                FROM actual_findings af
                JOIN fixtures f ON f.fixture_id = af.fixture_id
                WHERE {fired_where}
                  AND NOT EXISTS (
                      SELECT 1
                      FROM label_queue lq
                      WHERE lq.fixture_id = af.fixture_id
                        AND lq.rule_id = af.rule_id
                        AND lq.fired = 1
                  )
                GROUP BY af.fixture_id, af.rule_id
                ORDER BY finding_count DESC, max_confidence DESC
                LIMIT ?
            )
            INSERT OR IGNORE INTO label_queue (fixture_id, rule_id, queue_bucket, fired, priority)
            SELECT
                fixture_id,
                rule_id,
                CASE WHEN finding_count >= 3 THEN 'fired_high_signal' ELSE 'fired' END,
                1,
                CAST(finding_count * 10 + max_confidence * 10 AS INTEGER)
            FROM grouped
            """,
            (*fired_params, fired_limit),
        )

    if nonfired_limit > 0:
        all_rules = target_rules or [
            r[0] for r in conn.execute("SELECT DISTINCT rule_id FROM actual_findings ORDER BY rule_id").fetchall()
        ]

        if all_rules:
            per_rule = max(1, math.ceil(nonfired_limit / len(all_rules)))
            values_clause = ",".join(["(?)"] * len(all_rules))
            fixture_where = " AND ".join(fixture_clauses) if fixture_clauses else "1=1"

            conn.execute(
                f"""
                WITH selected_rules(rule_id) AS (
                    VALUES {values_clause}
                ),
                candidates AS (
                    SELECT
                        f.fixture_id,
                        sr.rule_id,
                        ROW_NUMBER() OVER (
                            PARTITION BY sr.rule_id
                            ORDER BY f.has_review_comments DESC, f.has_tests_changed ASC, f.created_at_utc DESC, f.fixture_id ASC
                        ) AS per_rule_rank
                    FROM selected_rules sr
                    JOIN fixtures f ON {fixture_where}
                    LEFT JOIN actual_findings af
                        ON af.fixture_id = f.fixture_id
                       AND af.rule_id = sr.rule_id
                       AND af.did_trigger = 1
                    LEFT JOIN label_queue lq
                        ON lq.fixture_id = f.fixture_id
                       AND lq.rule_id = sr.rule_id
                       AND lq.fired = 0
                    WHERE af.fixture_id IS NULL
                      AND lq.id IS NULL
                ),
                ranked AS (
                    SELECT
                        fixture_id,
                        rule_id,
                        ROW_NUMBER() OVER (
                            ORDER BY per_rule_rank ASC, rule_id ASC, fixture_id ASC
                        ) AS global_rank
                    FROM candidates
                    WHERE per_rule_rank <= ?
                )
                INSERT OR IGNORE INTO label_queue (fixture_id, rule_id, queue_bucket, fired, priority)
                SELECT fixture_id, rule_id, 'nonfired_probe', 0, 1
                FROM ranked
                WHERE global_rank <= ?
                """,
                (*all_rules, *fixture_params, per_rule, nonfired_limit),
            )

    conn.commit()
    return conn.execute("SELECT COUNT(*) FROM label_queue").fetchone()[0]


def main() -> None:
    parser = argparse.ArgumentParser(description="Seed label_queue from actual_findings.")
    parser.add_argument("--limit", type=int, default=150, help="Total fired items to queue.")
    parser.add_argument("--include-nonfired", type=int, default=30, help="How many non-fired tasks to synthesize.")
    parser.add_argument("--rules", default="", help="Comma-separated rule ids to target.")
    parser.add_argument("--language", default="C#", help="Only queue fixtures whose primary language matches (default: C#). Pass empty string to disable filter.")
    parser.add_argument("--tiers", default="all", help="Comma-separated tiers to include (default: all). Use 'gold,silver' to restrict to vetted tiers only.")
    args = parser.parse_args()

    base_dir = Path(__file__).resolve().parent
    config = load_config(base_dir)
    conn = connect(config["database_path"])
    try:
        target_rules = [x.strip() for x in args.rules.split(",") if x.strip()]
        total = seed(
            conn,
            limit=args.limit,
            include_nonfired=args.include_nonfired,
            rules=target_rules or None,
            language=args.language,
            tiers=args.tiers,
        )
        print(f"Queue ready. Total tasks in label_queue: {total}")
    finally:
        conn.close()


if __name__ == "__main__":
    main()

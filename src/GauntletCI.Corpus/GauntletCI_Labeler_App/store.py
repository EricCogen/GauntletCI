from __future__ import annotations

import json
import uuid
from datetime import datetime, timezone
from pathlib import Path
import sqlite3

APP_SCHEMA = """
CREATE TABLE IF NOT EXISTS label_queue (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    fixture_id TEXT NOT NULL,
    rule_id TEXT NOT NULL,
    queue_bucket TEXT NOT NULL DEFAULT 'manual',
    fired INTEGER NOT NULL DEFAULT 1,
    priority INTEGER NOT NULL DEFAULT 0,
    status TEXT NOT NULL DEFAULT 'pending',
    assigned_to TEXT,
    seeded_at_utc TEXT NOT NULL DEFAULT (datetime('now')),
    started_at_utc TEXT,
    completed_at_utc TEXT,
    notes TEXT,
    UNIQUE (fixture_id, rule_id, fired, queue_bucket)
);

CREATE TABLE IF NOT EXISTS rule_rubrics (
    rule_id TEXT PRIMARY KEY,
    intent TEXT,
    trigger_conditions TEXT,
    non_trigger_conditions TEXT,
    inconclusive_conditions TEXT,
    examples TEXT,
    updated_at_utc TEXT NOT NULL DEFAULT (datetime('now'))
);
"""


def init_app_tables(conn: sqlite3.Connection) -> None:
    conn.executescript(APP_SCHEMA)
    conn.commit()


def upsert_label(
    conn: sqlite3.Connection,
    fixture_id: str,
    rule_id: str,
    decision: str,
    confidence: float,
    reason: str,
    label_source: str,
    usefulness: float | None,
    reviewer_notes: str,
    reviewer: str,
) -> None:
    if decision not in {"yes", "no", "inconclusive"}:
        raise ValueError("Unsupported decision")

    should_trigger = 1 if decision == "yes" else 0
    is_inconclusive = 1 if decision == "inconclusive" else 0

    existing = conn.execute(
        "SELECT id FROM expected_findings WHERE fixture_id = ? AND rule_id = ?",
        (fixture_id, rule_id),
    ).fetchone()
    now = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    if existing:
        conn.execute(
            """
            UPDATE expected_findings
            SET should_trigger = ?,
                expected_confidence = ?,
                reason = ?,
                label_source = ?,
                is_inconclusive = ?
            WHERE id = ?
            """,
            (should_trigger, confidence, reason, label_source, is_inconclusive, existing["id"]),
        )
    else:
        conn.execute(
            """
            INSERT INTO expected_findings
            (id, fixture_id, rule_id, should_trigger, expected_confidence, reason, label_source, is_inconclusive)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?)
            """,
            (str(uuid.uuid4()), fixture_id, rule_id, should_trigger, confidence, reason, label_source, is_inconclusive),
        )

    if usefulness is not None:
        evaluation = conn.execute(
            "SELECT id FROM evaluations WHERE fixture_id = ? AND rule_id = ? AND reviewer = ?",
            (fixture_id, rule_id, reviewer),
        ).fetchone()
        if evaluation:
            conn.execute(
                """
                UPDATE evaluations
                SET usefulness = ?, reviewer_notes = ?, evaluated_at_utc = ?
                WHERE id = ?
                """,
                (usefulness, reviewer_notes, now, evaluation["id"]),
            )
        else:
            conn.execute(
                """
                INSERT INTO evaluations
                (id, fixture_id, rule_id, usefulness, reviewer_notes, evaluated_at_utc, reviewer)
                VALUES (?, ?, ?, ?, ?, ?, ?)
                """,
                (str(uuid.uuid4()), fixture_id, rule_id, usefulness, reviewer_notes, now, reviewer),
            )
    conn.commit()


def update_queue_status(conn: sqlite3.Connection, queue_id: int, status: str, notes: str = "") -> None:
    fields = {"status": status}
    now = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    if status == "in_progress":
        conn.execute(
            "UPDATE label_queue SET status = ?, started_at_utc = ?, notes = ? WHERE id = ?",
            (status, now, notes, queue_id),
        )
    elif status in {"labeled", "skipped"}:
        conn.execute(
            "UPDATE label_queue SET status = ?, completed_at_utc = ?, notes = ? WHERE id = ?",
            (status, now, notes, queue_id),
        )
    else:
        conn.execute("UPDATE label_queue SET status = ?, notes = ? WHERE id = ?", (status, notes, queue_id))
    conn.commit()


def save_rubric(
    conn: sqlite3.Connection,
    rule_id: str,
    intent: str,
    trigger_conditions: str,
    non_trigger_conditions: str,
    inconclusive_conditions: str,
    examples: str,
) -> None:
    conn.execute(
        """
        INSERT INTO rule_rubrics
        (rule_id, intent, trigger_conditions, non_trigger_conditions, inconclusive_conditions, examples, updated_at_utc)
        VALUES (?, ?, ?, ?, ?, ?, datetime('now'))
        ON CONFLICT(rule_id) DO UPDATE SET
            intent = excluded.intent,
            trigger_conditions = excluded.trigger_conditions,
            non_trigger_conditions = excluded.non_trigger_conditions,
            inconclusive_conditions = excluded.inconclusive_conditions,
            examples = excluded.examples,
            updated_at_utc = datetime('now')
        """,
        (rule_id, intent, trigger_conditions, non_trigger_conditions, inconclusive_conditions, examples),
    )
    conn.commit()


def bulk_apply_label_to_fixture(
    conn: sqlite3.Connection,
    fixture_id: str,
    exclude_rule_id: str,
    decision: str,
    confidence: float,
    reason: str,
    label_source: str,
    usefulness: float | None,
    reviewer_notes: str,
    reviewer: str,
) -> int:
    rows = conn.execute(
        """
        SELECT id, rule_id
        FROM label_queue
        WHERE fixture_id = ?
          AND rule_id <> ?
          AND status = 'pending'
        ORDER BY priority DESC, id ASC
        """,
        (fixture_id, exclude_rule_id),
    ).fetchall()

    count = 0
    for row in rows:
        upsert_label(
            conn,
            fixture_id=fixture_id,
            rule_id=row["rule_id"],
            decision=decision,
            confidence=confidence,
            reason=reason,
            label_source=label_source,
            usefulness=usefulness,
            reviewer_notes=reviewer_notes,
            reviewer=reviewer,
        )
        update_queue_status(conn, row["id"], "labeled", notes=reviewer_notes)
        count += 1
    return count

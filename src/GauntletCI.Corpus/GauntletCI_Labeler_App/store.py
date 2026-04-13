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

AUDIT_SNAPSHOTS_DDL = """
CREATE TABLE IF NOT EXISTS audit_snapshots (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    snapped_at_utc TEXT NOT NULL DEFAULT (datetime('now')),
    rules_snapped INTEGER NOT NULL DEFAULT 0,
    notes TEXT
);
CREATE TABLE IF NOT EXISTS audit_snapshot_rows (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    snapshot_id INTEGER NOT NULL REFERENCES audit_snapshots(id),
    rule_id TEXT NOT NULL,
    labeled INTEGER DEFAULT 0,
    tp INTEGER DEFAULT 0,
    fp INTEGER DEFAULT 0,
    fn INTEGER DEFAULT 0,
    precision_score REAL,
    recall_score REAL,
    usefulness_score REAL
);
"""


def init_app_tables(conn: sqlite3.Connection) -> None:
    conn.executescript(APP_SCHEMA)
    _ensure_snapshot_tables(conn)
    conn.commit()


def _ensure_snapshot_tables(conn: sqlite3.Connection) -> None:
    conn.executescript(AUDIT_SNAPSHOTS_DDL)
    conn.commit()


def _table_exists(conn: sqlite3.Connection, name: str) -> bool:
    return (
        conn.execute(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=?", (name,)
        ).fetchone()[0]
        > 0
    )


def _labels_since_last_snapshot(conn: sqlite3.Connection) -> int:
    if not _table_exists(conn, "audit_snapshots"):
        return 0
    last = conn.execute("SELECT MAX(snapped_at_utc) FROM audit_snapshots").fetchone()[0]
    if not last:
        return conn.execute("SELECT COUNT(*) FROM expected_findings").fetchone()[0]
    return conn.execute(
        "SELECT COUNT(*) FROM label_queue WHERE status='labeled' AND completed_at_utc > ?",
        (last,),
    ).fetchone()[0]


def _snapshot_aggregates(conn: sqlite3.Connection) -> None:
    _ensure_snapshot_tables(conn)
    rows = conn.execute(
        """
        WITH fired AS (
            SELECT fixture_id, rule_id, MAX(did_trigger) AS fired FROM actual_findings GROUP BY fixture_id, rule_id
        )
        SELECT ef.rule_id, COUNT(*) AS labeled,
               SUM(CASE WHEN f.fired=1 AND ef.should_trigger=1 AND ef.is_inconclusive=0 THEN 1 ELSE 0 END) AS tp,
               SUM(CASE WHEN f.fired=1 AND ef.should_trigger=0 AND ef.is_inconclusive=0 THEN 1 ELSE 0 END) AS fp,
               SUM(CASE WHEN f.fired=0 AND ef.should_trigger=1 AND ef.is_inconclusive=0 THEN 1 ELSE 0 END) AS fn,
               CASE WHEN SUM(CASE WHEN f.fired=1 AND ef.is_inconclusive=0 THEN 1 ELSE 0 END)>0
                    THEN CAST(SUM(CASE WHEN f.fired=1 AND ef.should_trigger=1 AND ef.is_inconclusive=0 THEN 1 ELSE 0 END) AS REAL)/
                         SUM(CASE WHEN f.fired=1 AND ef.is_inconclusive=0 THEN 1 ELSE 0 END) ELSE NULL END AS precision_score,
               AVG(ev.usefulness) AS usefulness_score
        FROM expected_findings ef
        LEFT JOIN fired f ON f.fixture_id=ef.fixture_id AND f.rule_id=ef.rule_id
        LEFT JOIN evaluations ev ON ev.fixture_id=ef.fixture_id AND ev.rule_id=ef.rule_id
        GROUP BY ef.rule_id
        """
    ).fetchall()
    snap_id = conn.execute(
        "INSERT INTO audit_snapshots (rules_snapped) VALUES (?)", (len(rows),)
    ).lastrowid
    for row in rows:
        conn.execute(
            "INSERT INTO audit_snapshot_rows (snapshot_id, rule_id, labeled, tp, fp, fn, precision_score, usefulness_score) VALUES (?,?,?,?,?,?,?,?)",
            (
                snap_id,
                row["rule_id"],
                row["labeled"],
                row["tp"],
                row["fp"],
                row["fn"],
                row["precision_score"],
                row["usefulness_score"],
            ),
        )
    conn.commit()


def refresh_aggregates(conn) -> int:
    """Recompute aggregates table from actual_findings + expected_findings. Returns rule count updated."""
    rows = conn.execute(
        """
        WITH labeled AS (
            SELECT ef.rule_id,
                   COUNT(*) AS total,
                   AVG(ef.should_trigger) AS trigger_rate
            FROM expected_findings ef GROUP BY ef.rule_id
        ),
        scored AS (
            SELECT ef.rule_id,
                   CAST(SUM(CASE WHEN af_fired.fired=1 AND ef.should_trigger=1 AND ef.is_inconclusive=0 THEN 1 ELSE 0 END) AS REAL) AS tp,
                   CAST(SUM(CASE WHEN af_fired.fired=1 AND ef.should_trigger=0 AND ef.is_inconclusive=0 THEN 1 ELSE 0 END) AS REAL) AS fp,
                   CAST(SUM(CASE WHEN af_fired.fired=0 AND ef.should_trigger=1 AND ef.is_inconclusive=0 THEN 1 ELSE 0 END) AS REAL) AS fn,
                   AVG(ev.usefulness) AS avg_usefulness
            FROM expected_findings ef
            LEFT JOIN (
                SELECT fixture_id, rule_id, MAX(did_trigger) AS fired
                FROM actual_findings GROUP BY fixture_id, rule_id
            ) af_fired ON af_fired.fixture_id=ef.fixture_id AND af_fired.rule_id=ef.rule_id
            LEFT JOIN evaluations ev ON ev.fixture_id=ef.fixture_id AND ev.rule_id=ef.rule_id
            GROUP BY ef.rule_id
        )
        SELECT l.rule_id, l.trigger_rate,
               CASE WHEN s.tp+s.fp>0 THEN s.tp/(s.tp+s.fp) ELSE NULL END AS precision_score,
               CASE WHEN s.tp+s.fn>0 THEN s.tp/(s.tp+s.fn) ELSE NULL END AS recall_score,
               COALESCE(s.avg_usefulness, 0.0) AS usefulness_score
        FROM labeled l LEFT JOIN scored s ON s.rule_id=l.rule_id
        """
    ).fetchall()
    for row in rows:
        conn.execute(
            """
            INSERT INTO aggregates (rule_id, tier, trigger_rate, precision_score, recall_score, usefulness_score, last_updated_utc)
            VALUES (?, 'all', ?, ?, ?, ?, datetime('now'))
            ON CONFLICT(rule_id, tier) DO UPDATE SET
                trigger_rate=excluded.trigger_rate,
                precision_score=excluded.precision_score,
                recall_score=excluded.recall_score,
                usefulness_score=excluded.usefulness_score,
                last_updated_utc=excluded.last_updated_utc
            """,
            (
                row["rule_id"],
                row["trigger_rate"],
                row["precision_score"],
                row["recall_score"],
                row["usefulness_score"],
            ),
        )
    conn.commit()
    return len(rows)


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

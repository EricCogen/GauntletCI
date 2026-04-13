from __future__ import annotations

import json
import sqlite3
from typing import Any


def _try_json(value: str | None, default: Any) -> Any:
    if not value:
        return default
    try:
        return json.loads(value)
    except Exception:
        return default


def dashboard_stats(conn: sqlite3.Connection) -> dict[str, Any]:
    q = lambda sql: conn.execute(sql).fetchone()[0]
    return {
        "fixtures": q("SELECT COUNT(*) FROM fixtures"),
        "rules": q("SELECT COUNT(DISTINCT rule_id) FROM actual_findings"),
        "findings": q("SELECT COUNT(*) FROM actual_findings"),
        "queued": q("SELECT COUNT(*) FROM label_queue"),
        "pending": q("SELECT COUNT(*) FROM label_queue WHERE status = 'pending'"),
        "labeled": q("SELECT COUNT(*) FROM expected_findings"),
        "evaluations": q("SELECT COUNT(*) FROM evaluations"),
    }


def queue_rows(conn: sqlite3.Connection, status: str = "pending", rule_id: str = "", bucket: str = "", language: str = "C#") -> list[sqlite3.Row]:
    sql = """
    SELECT lq.*, f.repo, f.pr_number, f.tier, f.pr_size_bucket,
           f.has_tests_changed, f.has_review_comments,
           COALESCE(af.finding_count, 0) AS finding_count,
           COALESCE(lbl.expected_confidence, NULL) AS expected_confidence,
           lbl.should_trigger,
           lbl.is_inconclusive
    FROM label_queue lq
    JOIN fixtures f ON f.fixture_id = lq.fixture_id
    LEFT JOIN (
        SELECT fixture_id, rule_id, COUNT(*) AS finding_count
        FROM actual_findings
        WHERE did_trigger = 1
        GROUP BY fixture_id, rule_id
    ) af ON af.fixture_id = lq.fixture_id AND af.rule_id = lq.rule_id
    LEFT JOIN expected_findings lbl ON lbl.fixture_id = lq.fixture_id AND lbl.rule_id = lq.rule_id
    WHERE 1 = 1
    """
    params: list[Any] = []
    if status:
        sql += " AND lq.status = ?"
        params.append(status)
    if rule_id:
        sql += " AND lq.rule_id = ?"
        params.append(rule_id)
    if bucket:
        sql += " AND lq.queue_bucket = ?"
        params.append(bucket)
    if language:
        sql += " AND LOWER(f.language) = LOWER(?)"
        params.append(language)
    sql += " ORDER BY lq.priority DESC, lq.id ASC"
    return conn.execute(sql, tuple(params)).fetchall()


def grouped_findings_for_task(conn: sqlite3.Connection, fixture_id: str, rule_id: str) -> dict[str, Any]:
    rows = conn.execute(
        """
        SELECT *
        FROM actual_findings
        WHERE fixture_id = ? AND rule_id = ?
        ORDER BY actual_confidence DESC, id ASC
        """,
        (fixture_id, rule_id),
    ).fetchall()
    items = []
    for row in rows:
        evidence = _try_json(row["evidence_json"], row["evidence_json"])
        items.append(
            {
                "id": row["id"],
                "confidence": row["actual_confidence"],
                "message": row["message"] or "",
                "change_implication": row["change_implication"] or "",
                "evidence": evidence,
                "execution_time_ms": row["execution_time_ms"],
            }
        )
    messages = [x["message"] for x in items if x["message"]]
    implications = [x["change_implication"] for x in items if x["change_implication"]]
    return {
        "count": len(items),
        "max_confidence": max((x["confidence"] for x in items), default=0.0),
        "messages": messages[:10],
        "implications": implications[:10],
        "finding_rows": items[:25],
    }


def fixture_by_id(conn: sqlite3.Connection, fixture_id: str) -> dict[str, Any] | None:
    row = conn.execute("SELECT * FROM fixtures WHERE fixture_id = ?", (fixture_id,)).fetchone()
    return dict(row) if row else None


def rubric_for_rule(conn: sqlite3.Connection, rule_id: str) -> dict[str, Any] | None:
    row = conn.execute("SELECT * FROM rule_rubrics WHERE rule_id = ?", (rule_id,)).fetchone()
    return dict(row) if row else None


def current_label(conn: sqlite3.Connection, fixture_id: str, rule_id: str, reviewer: str) -> dict[str, Any]:
    label = conn.execute(
        "SELECT * FROM expected_findings WHERE fixture_id = ? AND rule_id = ?",
        (fixture_id, rule_id),
    ).fetchone()
    evaluation = conn.execute(
        "SELECT * FROM evaluations WHERE fixture_id = ? AND rule_id = ? AND reviewer = ?",
        (fixture_id, rule_id, reviewer),
    ).fetchone()
    result: dict[str, Any] = {"label": dict(label) if label else None, "evaluation": dict(evaluation) if evaluation else None}
    return result


def top_rule_metrics(conn: sqlite3.Connection) -> list[sqlite3.Row]:
    return conn.execute(
        """
        SELECT a.rule_id,
               a.tier,
               a.trigger_rate,
               a.precision_score,
               a.recall_score,
               a.usefulness_score,
               COALESCE(f.total_fired, 0) AS total_fired,
               COALESCE(lbl.total_labels, 0) AS total_labels
        FROM aggregates a
        LEFT JOIN (
            SELECT rule_id, COUNT(*) AS total_fired
            FROM actual_findings
            WHERE did_trigger = 1
            GROUP BY rule_id
        ) f ON f.rule_id = a.rule_id
        LEFT JOIN (
            SELECT rule_id, COUNT(*) AS total_labels
            FROM expected_findings
            GROUP BY rule_id
        ) lbl ON lbl.rule_id = a.rule_id
        ORDER BY a.trigger_rate DESC, a.rule_id ASC
        """
    ).fetchall()


def evaluation_rows(conn: sqlite3.Connection) -> list[sqlite3.Row]:
    return conn.execute(
        """
        WITH fired AS (
            SELECT fixture_id, rule_id, MAX(CASE WHEN did_trigger = 1 THEN 1 ELSE 0 END) AS fired
            FROM actual_findings
            GROUP BY fixture_id, rule_id
        )
        SELECT ef.rule_id,
               COUNT(*) AS labeled_cases,
               SUM(CASE WHEN fired.fired = 1 AND ef.should_trigger = 1 AND ef.is_inconclusive = 0 THEN 1 ELSE 0 END) AS tp,
               SUM(CASE WHEN fired.fired = 1 AND ef.should_trigger = 0 AND ef.is_inconclusive = 0 THEN 1 ELSE 0 END) AS fp,
               SUM(CASE WHEN fired.fired = 0 AND ef.should_trigger = 1 AND ef.is_inconclusive = 0 THEN 1 ELSE 0 END) AS fn,
               SUM(CASE WHEN ef.is_inconclusive = 1 THEN 1 ELSE 0 END) AS inconclusive,
               ROUND(AVG(ev.usefulness), 2) AS avg_usefulness
        FROM expected_findings ef
        LEFT JOIN fired ON fired.fixture_id = ef.fixture_id AND fired.rule_id = ef.rule_id
        LEFT JOIN evaluations ev ON ev.fixture_id = ef.fixture_id AND ev.rule_id = ef.rule_id
        GROUP BY ef.rule_id
        ORDER BY labeled_cases DESC, ef.rule_id ASC
        """
    ).fetchall()


def find_exact_duplicates(conn, current_queue_id: int, rule_id: str, message: str) -> list:
    if not message:
        return []
    return conn.execute(
        """
        SELECT lq.id, lq.fixture_id, f.repo, f.pr_number
        FROM label_queue lq
        JOIN fixtures f ON f.fixture_id = lq.fixture_id
        JOIN actual_findings af
          ON af.fixture_id = lq.fixture_id AND af.rule_id = lq.rule_id AND af.did_trigger = 1
        WHERE lq.rule_id = ? AND lq.status = 'pending' AND lq.id != ? AND af.message = ?
        GROUP BY lq.id
        ORDER BY lq.priority DESC, lq.id ASC
        LIMIT 50
        """,
        (rule_id, current_queue_id, message),
    ).fetchall()


def audit_report_rows(conn) -> list:
    return conn.execute(
        """
        WITH fired AS (
            SELECT fixture_id, rule_id, MAX(did_trigger) AS fired
            FROM actual_findings GROUP BY fixture_id, rule_id
        ),
        stats AS (
            SELECT ef.rule_id,
                   COUNT(*) AS labeled,
                   SUM(CASE WHEN f.fired=1 AND ef.should_trigger=1 AND ef.is_inconclusive=0 THEN 1 ELSE 0 END) AS tp,
                   SUM(CASE WHEN f.fired=1 AND ef.should_trigger=0 AND ef.is_inconclusive=0 THEN 1 ELSE 0 END) AS fp,
                   SUM(CASE WHEN f.fired=0 AND ef.should_trigger=1 AND ef.is_inconclusive=0 THEN 1 ELSE 0 END) AS fn,
                   SUM(CASE WHEN ef.is_inconclusive=1 THEN 1 ELSE 0 END) AS inconclusive,
                   AVG(ev.usefulness) AS avg_usefulness
            FROM expected_findings ef
            LEFT JOIN fired f ON f.fixture_id=ef.fixture_id AND f.rule_id=ef.rule_id
            LEFT JOIN evaluations ev ON ev.fixture_id=ef.fixture_id AND ev.rule_id=ef.rule_id
            GROUP BY ef.rule_id
        )
        SELECT s.*,
               CASE WHEN s.tp+s.fp>0 THEN CAST(s.tp AS REAL)/(s.tp+s.fp) ELSE NULL END AS precision_score,
               CASE WHEN s.tp+s.fn>0 THEN CAST(s.tp AS REAL)/(s.tp+s.fn) ELSE NULL END AS recall_score
        FROM stats s
        ORDER BY s.labeled DESC, s.rule_id ASC
        """
    ).fetchall()

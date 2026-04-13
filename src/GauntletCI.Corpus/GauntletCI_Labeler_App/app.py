from __future__ import annotations

import json
import math
from pathlib import Path
from typing import Any

from flask import Flask, abort, flash, redirect, render_template, request, url_for

from config_loader import load_config
from db import connect
from services.fixture_service import extract_diff_snippet, load_fixture_artifacts
from services.query_service import (
    audit_report_rows,
    current_label,
    dashboard_stats,
    evaluation_rows,
    find_exact_duplicates,
    fixture_by_id,
    grouped_findings_for_task,
    queue_rows,
    rubric_for_rule,
    top_rule_metrics,
)
from services.rules_doc_parser import load_rule_definitions
from store import (
    _labels_since_last_snapshot,
    _snapshot_aggregates,
    _table_exists,
    init_app_tables,
    refresh_aggregates,
    save_rubric,
    update_queue_status,
    upsert_label,
)

BASE_DIR = Path(__file__).resolve().parent
CONFIG = load_config(BASE_DIR)

app = Flask(__name__)
app.secret_key = CONFIG["secret_key"]

# Resolve relative to repo root (two levels up from app.py)
_RULES_MD = Path(__file__).resolve().parent.parent.parent.parent / "docs" / "rules.md"
RULE_DEFINITIONS = load_rule_definitions(str(_RULES_MD))


def get_conn():
    conn = connect(CONFIG["database_path"])
    init_app_tables(conn)
    return conn


@app.template_filter("pct")
def pct_filter(value: Any) -> str:
    try:
        return f"{float(value) * 100:.1f}%"
    except Exception:
        return "-"


@app.template_filter("yesno")
def yesno_filter(value: Any) -> str:
    return "Yes" if str(value) in {"1", "True", "true"} else "No"


@app.route("/")
def dashboard():
    with get_conn() as conn:
        stats = dashboard_stats(conn)
        rules = top_rule_metrics(conn)
        evaluations = evaluation_rows(conn)
    return render_template("dashboard.html", stats=stats, rules=rules[:12], evaluations=evaluations[:12])


@app.post("/refresh-metrics")
def refresh_metrics():
    with get_conn() as conn:
        n = refresh_aggregates(conn)
        _snapshot_aggregates(conn)
    flash(f"Metrics refreshed for {n} rule(s).", "success")
    return redirect(url_for("audit"))


@app.get("/audit")
def audit():
    with get_conn() as conn:
        rows = audit_report_rows(conn)
        snapshots = (
            conn.execute("SELECT * FROM audit_snapshots ORDER BY snapped_at_utc DESC LIMIT 10").fetchall()
            if _table_exists(conn, "audit_snapshots")
            else []
        )
        labels_since = _labels_since_last_snapshot(conn)

    enriched = []
    for row in rows:
        p = row["precision_score"]
        r = row["recall_score"]
        u = row["avg_usefulness"] or 0
        labeled = row["labeled"]
        if labeled < 5:
            verdict = "insufficient_data"
        elif p is not None and p >= 0.75 and u >= 3.0:
            verdict = "keep"
        elif p is not None and p < 0.4:
            verdict = "kill_or_demote"
        elif p is not None:
            verdict = "rewrite"
        else:
            verdict = "insufficient_data"
        enriched.append({**dict(row), "precision_score": p, "recall_score": r, "verdict": verdict})

    return render_template("audit.html", rows=enriched, snapshots=snapshots, labels_since=labels_since)


@app.route("/queue")
def queue():
    status = request.args.get("status", "pending")
    rule_id = request.args.get("rule_id", "")
    bucket = request.args.get("bucket", "")
    language = request.args.get("language", "C#")
    has_tests = request.args.get("has_tests", "")
    has_comments = request.args.get("has_comments", "")
    fired_filter = request.args.get("fired_filter", "")
    sort = request.args.get("sort", "")
    sort_dir = request.args.get("sort_dir", "asc")
    with get_conn() as conn:
        rows = queue_rows(
            conn,
            status=status,
            rule_id=rule_id,
            bucket=bucket,
            language=language,
            has_tests=has_tests,
            has_comments=has_comments,
            fired_filter=fired_filter,
            sort=sort,
            sort_dir=sort_dir,
        )
        rules = [r[0] for r in conn.execute("SELECT DISTINCT rule_id FROM actual_findings ORDER BY rule_id").fetchall()]
        buckets = [r[0] for r in conn.execute("SELECT DISTINCT queue_bucket FROM label_queue ORDER BY queue_bucket").fetchall()]
    return render_template(
        "queue.html",
        rows=rows,
        status=status,
        rule_id=rule_id,
        bucket=bucket,
        language=language,
        has_tests=has_tests,
        has_comments=has_comments,
        fired_filter=fired_filter,
        sort=sort,
        sort_dir=sort_dir,
        rules=rules,
        buckets=buckets,
    )


@app.route("/task/<int:queue_id>")
def task(queue_id: int):
    with get_conn() as conn:
        task_row = conn.execute(
            """
            SELECT lq.*, f.repo, f.pr_number, f.tier, f.language, f.pr_size_bucket,
                   f.has_tests_changed, f.has_review_comments
            FROM label_queue lq
            JOIN fixtures f ON f.fixture_id = lq.fixture_id
            WHERE lq.id = ?
            """,
            (queue_id,),
        ).fetchone()
        if not task_row:
            abort(404)
        if task_row["status"] == "pending":
            update_queue_status(conn, queue_id, "in_progress")
            task_row = conn.execute(
                """
                SELECT lq.*, f.repo, f.pr_number, f.tier, f.language, f.pr_size_bucket,
                       f.has_tests_changed, f.has_review_comments
                FROM label_queue lq
                JOIN fixtures f ON f.fixture_id = lq.fixture_id
                WHERE lq.id = ?
                """,
                (queue_id,),
            ).fetchone()

        fixture = fixture_by_id(conn, task_row["fixture_id"])
        if not fixture:
            abort(404)

        grouped = grouped_findings_for_task(conn, task_row["fixture_id"], task_row["rule_id"])
        rubric = rubric_for_rule(conn, task_row["rule_id"])
        label_state = current_label(conn, task_row["fixture_id"], task_row["rule_id"], CONFIG["reviewer_name"])

        top_message = grouped["messages"][0] if grouped["messages"] else ""
        top_evidence = ""
        if grouped["finding_rows"]:
            ev = grouped["finding_rows"][0].get("evidence")
            if isinstance(ev, (dict, list)):
                top_evidence = json.dumps(ev, ensure_ascii=False)
            else:
                top_evidence = str(ev or "")

        duplicates = find_exact_duplicates(conn, queue_id, task_row["rule_id"], top_message)
        pending_count = conn.execute("SELECT COUNT(*) FROM label_queue WHERE status='pending'").fetchone()[0]

    artifacts = load_fixture_artifacts(CONFIG["fixtures_root"], fixture)
    diff_patch = artifacts.get("diff_patch") or ""
    diff_snippet = extract_diff_snippet(diff_patch, (top_message + " " + top_evidence).strip())

    review_comments_raw = artifacts.get("review_comments") or []
    review_comments: list[dict[str, str]] = []
    if isinstance(review_comments_raw, list):
        for c in review_comments_raw[:5]:
            if not isinstance(c, dict):
                continue
            path = c.get("path") or c.get("original_path") or "-"
            body = (c.get("body") or "").strip()
            user = "-"
            u = c.get("user")
            if isinstance(u, dict):
                user = u.get("login") or u.get("name") or user
            review_comments.append({"path": path, "body": body, "user": user})

    rule_def = RULE_DEFINITIONS.get(task_row["rule_id"])

    return render_template(
        "task.html",
        task=task_row,
        fixture=fixture,
        grouped=grouped,
        rubric=rubric,
        label_state=label_state,
        rule_def=rule_def,
        diff_snippet=diff_snippet,
        review_comments=review_comments,
        duplicates=duplicates,
        pending_count=pending_count,
    )


@app.post("/task/<int:queue_id>/label")
def submit_label(queue_id: int):
    decision = request.form.get("decision", "").strip()
    confidence = float(request.form.get("confidence", "0.75") or 0.75)
    reason = request.form.get("reason", "").strip()
    fixture_id = request.form.get("fixture_id", "").strip()
    rule_id = request.form.get("rule_id", "").strip()

    apply_to_duplicates = request.form.get("apply_to_duplicates") == "1"
    duplicate_ids_raw = request.form.get("duplicate_ids", "") if apply_to_duplicates else ""

    if decision not in {"yes", "no", "inconclusive"}:
        flash("Choose Yes, No, or Inconclusive.", "error")
        return redirect(url_for("task", queue_id=queue_id))

    source = "human_gold" if confidence >= 0.75 and decision != "inconclusive" else "human_silver"

    dup_ids: list[int] = []
    for part in duplicate_ids_raw.split(","):
        part = part.strip()
        if part.isdigit():
            dup_ids.append(int(part))

    applied_count = 0
    should_snapshot_prompt = False

    with get_conn() as conn:
        upsert_label(
            conn,
            fixture_id=fixture_id,
            rule_id=rule_id,
            decision=decision,
            confidence=confidence,
            reason=reason,
            label_source=source,
            usefulness=None,
            reviewer_notes="",
            reviewer=CONFIG["reviewer_name"],
        )
        update_queue_status(conn, queue_id, "labeled", notes="")

        if apply_to_duplicates and dup_ids:
            for dup_id in dup_ids:
                row = conn.execute(
                    "SELECT fixture_id, rule_id FROM label_queue WHERE id = ?",
                    (dup_id,),
                ).fetchone()
                if not row:
                    continue
                upsert_label(
                    conn,
                    fixture_id=row["fixture_id"],
                    rule_id=row["rule_id"],
                    decision=decision,
                    confidence=confidence,
                    reason=reason,
                    label_source=source,
                    usefulness=None,
                    reviewer_notes="",
                    reviewer=CONFIG["reviewer_name"],
                )
                update_queue_status(conn, dup_id, "labeled", notes="")
                applied_count += 1

        labels_since = _labels_since_last_snapshot(conn)
        should_snapshot_prompt = labels_since > 0 and labels_since % 25 == 0

        next_row = conn.execute(
            "SELECT id FROM label_queue WHERE status = 'pending' ORDER BY priority DESC, id ASC LIMIT 1"
        ).fetchone()

    if applied_count:
        flash(f"Saved + applied to {applied_count} duplicate(s).", "success")
    else:
        flash("Saved.", "success")

    if should_snapshot_prompt:
        flash(
            f"You've labeled {labels_since} items since the last audit snapshot. Consider refreshing metrics from the Audit page.",
            "info",
        )

    if next_row:
        return redirect(url_for("task", queue_id=next_row["id"]))
    return redirect(url_for("queue"))


@app.post("/task/<int:queue_id>/skip")
def skip_task(queue_id: int):
    notes = request.form.get("notes", "").strip()
    with get_conn() as conn:
        update_queue_status(conn, queue_id, "skipped", notes=notes)
    flash("Task skipped.", "success")
    return redirect(url_for("queue"))


@app.route("/rules")
def rules():
    with get_conn() as conn:
        rows = top_rule_metrics(conn)
    return render_template("rules.html", rows=rows)


@app.route("/rules/<rule_id>", methods=["GET", "POST"])
def rule_detail(rule_id: str):
    if request.method == "POST":
        with get_conn() as conn:
            save_rubric(
                conn,
                rule_id,
                request.form.get("intent", "").strip(),
                request.form.get("trigger_conditions", "").strip(),
                request.form.get("non_trigger_conditions", "").strip(),
                request.form.get("inconclusive_conditions", "").strip(),
                request.form.get("examples", "").strip(),
            )
        flash("Rubric saved.", "success")
        return redirect(url_for("rule_detail", rule_id=rule_id))

    with get_conn() as conn:
        rubric = rubric_for_rule(conn, rule_id) or {
            "rule_id": rule_id,
            "intent": "",
            "trigger_conditions": "",
            "non_trigger_conditions": "",
            "inconclusive_conditions": "",
            "examples": "",
        }
        metrics = [row for row in top_rule_metrics(conn) if row["rule_id"] == rule_id]
        evaluations = [row for row in evaluation_rows(conn) if row["rule_id"] == rule_id]
    return render_template("rule_detail.html", rubric=rubric, metrics=metrics[0] if metrics else None, evaluation=evaluations[0] if evaluations else None)


@app.route("/auto-label/preview")
def auto_label_preview():
    with get_conn() as conn:
        rows = conn.execute("""
            SELECT lq.rule_id, COUNT(*) AS n
            FROM label_queue lq
            WHERE lq.fired = 0 AND lq.status = 'pending'
            GROUP BY lq.rule_id
            ORDER BY lq.rule_id
        """).fetchall()
    total = sum(r["n"] for r in rows)
    # Flag rules where a human eye is worth it even for non-fires
    high_risk = {"GCI0012", "GCI0029", "GCI0040"}
    return render_template(
        "auto_label_preview.html",
        rows=rows,
        total=total,
        high_risk=high_risk,
    )


@app.post("/auto-label/apply")
def auto_label_apply():
    selected = set(request.form.getlist("rule_ids"))
    try:
        confidence = float(request.form.get("confidence", "0.75"))
    except ValueError:
        confidence = 0.75
    reason = "Rule did not fire on this PR; auto-labeled as negative example."

    if not selected:
        flash("No rules selected — nothing applied.", "warning")
        return redirect(url_for("auto_label_preview"))

    applied = 0
    with get_conn() as conn:
        items = conn.execute("""
            SELECT lq.id, lq.fixture_id, lq.rule_id
            FROM label_queue lq
            WHERE lq.fired = 0 AND lq.status = 'pending'
              AND lq.rule_id IN ({})
        """.format(",".join("?" * len(selected))), list(selected)).fetchall()

        for item in items:
            upsert_label(
                conn,
                fixture_id=item["fixture_id"],
                rule_id=item["rule_id"],
                decision="no",
                confidence=confidence,
                reason=reason,
                label_source="auto-negative",
                usefulness=None,
                reviewer_notes="",
                reviewer=CONFIG["reviewer_name"],
            )
            update_queue_status(conn, item["id"], "labeled", notes="auto-negative")
            applied += 1

    flash(f"Auto-labeled {applied} item(s) as No (confidence {confidence}).", "success")
    return redirect(url_for("queue"))


@app.route("/evaluations")
def evaluations():
    with get_conn() as conn:
        rows = evaluation_rows(conn)
    enriched = []
    for row in rows:
        tp = row["tp"] or 0
        fp = row["fp"] or 0
        fn = row["fn"] or 0
        precision = (tp / (tp + fp)) if (tp + fp) else None
        recall = (tp / (tp + fn)) if (tp + fn) else None
        verdict = "observe"
        if precision is not None and recall is not None:
            if precision >= 0.75 and (row["avg_usefulness"] or 0) >= 3.0:
                verdict = "keep"
            elif precision < 0.4 or (row["avg_usefulness"] or 0) < 2.0:
                verdict = "kill_or_demote"
            else:
                verdict = "rewrite"
        enriched.append({**dict(row), "precision": precision, "recall": recall, "verdict": verdict})
    return render_template("evaluations.html", rows=enriched)


if __name__ == "__main__":
    app.run(debug=True, host="127.0.0.1", port=5000)

from __future__ import annotations

import math
from pathlib import Path
from typing import Any

from flask import Flask, abort, flash, redirect, render_template, request, url_for

from config_loader import load_config
from db import connect
from services.fixture_service import load_fixture_artifacts
from services.query_service import (
    current_label,
    dashboard_stats,
    evaluation_rows,
    fixture_by_id,
    grouped_findings_for_task,
    queue_rows,
    rubric_for_rule,
    top_rule_metrics,
)
from store import bulk_apply_label_to_fixture, init_app_tables, save_rubric, update_queue_status, upsert_label

BASE_DIR = Path(__file__).resolve().parent
CONFIG = load_config(BASE_DIR)

app = Flask(__name__)
app.secret_key = CONFIG["secret_key"]


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


@app.route("/queue")
def queue():
    status = request.args.get("status", "pending")
    rule_id = request.args.get("rule_id", "")
    bucket = request.args.get("bucket", "")
    language = request.args.get("language", "C#")
    with get_conn() as conn:
        rows = queue_rows(conn, status=status, rule_id=rule_id, bucket=bucket, language=language)
        rules = [r[0] for r in conn.execute("SELECT DISTINCT rule_id FROM actual_findings ORDER BY rule_id").fetchall()]
        buckets = [r[0] for r in conn.execute("SELECT DISTINCT queue_bucket FROM label_queue ORDER BY queue_bucket").fetchall()]
    return render_template("queue.html", rows=rows, status=status, rule_id=rule_id, bucket=bucket, language=language, rules=rules, buckets=buckets)


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
    artifacts = load_fixture_artifacts(CONFIG["fixtures_root"], fixture)
    files_json = artifacts.get("files_json") or []
    changed_files = (
        [
            f for f in files_json
            if str(f.get("filename") or f.get("path") or "").lower().endswith(".cs")
        ][:25]
        if isinstance(files_json, list) else []
    )
    review_comments = artifacts.get("review_comments") or []
    diff_patch = artifacts.get("diff_patch") or ""
    diff_preview = "\n".join(diff_patch.splitlines()[:350])
    return render_template(
        "task.html",
        task=task_row,
        fixture=fixture,
        grouped=grouped,
        rubric=rubric,
        label_state=label_state,
        artifacts=artifacts,
        changed_files=changed_files,
        review_comments=review_comments,
        diff_preview=diff_preview,
    )


@app.post("/task/<int:queue_id>/label")
def submit_label(queue_id: int):
    decision = request.form.get("decision", "").strip()
    confidence = float(request.form.get("confidence", "0.75") or 0.75)
    usefulness_raw = request.form.get("usefulness", "").strip()
    usefulness = float(usefulness_raw) if usefulness_raw else None
    reason = request.form.get("reason", "").strip()
    reviewer_notes = request.form.get("reviewer_notes", "").strip()
    fixture_id = request.form.get("fixture_id", "").strip()
    rule_id = request.form.get("rule_id", "").strip()
    apply_scope = request.form.get("apply_scope", "current").strip()

    if decision not in {"yes", "no", "inconclusive"}:
        flash("Choose Yes, No, or Inconclusive.", "error")
        return redirect(url_for("task", queue_id=queue_id))

    source = "human_gold" if confidence >= 0.75 and decision != "inconclusive" else "human_silver"
    with get_conn() as conn:
        upsert_label(
            conn,
            fixture_id=fixture_id,
            rule_id=rule_id,
            decision=decision,
            confidence=confidence,
            reason=reason,
            label_source=source,
            usefulness=usefulness,
            reviewer_notes=reviewer_notes,
            reviewer=CONFIG["reviewer_name"],
        )
        update_queue_status(conn, queue_id, "labeled", notes=reviewer_notes)

        bulk_count = 0
        if apply_scope == "fixture_pending":
            bulk_count = bulk_apply_label_to_fixture(
                conn,
                fixture_id=fixture_id,
                exclude_rule_id=rule_id,
                decision=decision,
                confidence=confidence,
                reason=reason,
                label_source=source,
                usefulness=usefulness,
                reviewer_notes=reviewer_notes,
                reviewer=CONFIG["reviewer_name"],
            )

        next_row = conn.execute(
            "SELECT id FROM label_queue WHERE status = 'pending' ORDER BY priority DESC, id ASC LIMIT 1"
        ).fetchone()

    if apply_scope == "fixture_pending" and bulk_count:
        flash(f"Saved and applied to {bulk_count} additional pending item(s) for this fixture.", "success")
    else:
        flash("Saved.", "success")
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

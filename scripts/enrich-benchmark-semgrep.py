#!/usr/bin/env python3
"""Run Semgrep on gold benchmark diffs (added .cs lines) and write competitor artifacts."""
from __future__ import annotations

import argparse
import json
import shutil
import sqlite3
import subprocess
import tempfile
from datetime import datetime, timezone
from pathlib import Path

from benchmark_lib import (
    DB,
    load_suite,
    resolve_diff,
    suite_fixtures,
    write_competitor_artifact,
)


def semgrep_available() -> bool:
    return shutil.which("semgrep") is not None


def parse_added_cs(diff_path: Path) -> dict[str, list[str]]:
    by_file: dict[str, list[str]] = {}
    current: str | None = None
    for line in diff_path.read_text(encoding="utf-8", errors="replace").splitlines():
        if line.startswith("+++ b/"):
            path = line[6:]
            current = path if path.endswith(".cs") else None
            if current and current not in by_file:
                by_file[current] = []
            continue
        if current and line.startswith("+") and not line.startswith("+++"):
            by_file[current].append(line[1:])
    return by_file


def sanitize_name(path: str) -> str:
    return path.replace("/", "__").replace("\\", "__")


def run_semgrep(temp_dir: Path, config: str) -> tuple[list[dict], str | None]:
    proc = subprocess.run(
        ["semgrep", f"--config={config}", "--json", "--lang=csharp", "--quiet", str(temp_dir)],
        capture_output=True,
        text=True,
        check=False,
    )
    raw = proc.stdout.strip()
    if not raw:
        return [], None
    doc = json.loads(raw)
    alerts: list[dict] = []
    for r in doc.get("results", []):
        extra = r.get("extra", {}) or {}
        start = r.get("start", {}) or {}
        alerts.append(
            {
                "file": r.get("path", ""),
                "line": start.get("line"),
                "rule_id": r.get("check_id", ""),
                "message": extra.get("message", ""),
                "severity": extra.get("severity", "INFO"),
            }
        )
    return alerts, raw


def sync_db(con: sqlite3.Connection, fixture_id: str, repo: str, count: int, rules: str | None, severity: str | None, raw: str | None) -> None:
    con.execute(
        """
        CREATE TABLE IF NOT EXISTS semgrep_enrichments (
            fixture_id TEXT NOT NULL,
            repo TEXT NOT NULL,
            finding_count INTEGER NOT NULL,
            rules_fired TEXT,
            highest_severity TEXT,
            findings_json TEXT,
            scanned_at_utc TEXT,
            UNIQUE(fixture_id)
        )
        """
    )
    con.execute(
        """
        INSERT OR REPLACE INTO semgrep_enrichments
        (fixture_id, repo, finding_count, rules_fired, highest_severity, findings_json, scanned_at_utc)
        VALUES (?,?,?,?,?,?,?)
        """,
        (fixture_id, repo, count, rules, severity, raw, datetime.now(timezone.utc).isoformat()),
    )


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--config", default="auto")
    ap.add_argument("--gold-only", action="store_true", default=True)
    ap.add_argument("--all-suite", action="store_true")
    args = ap.parse_args()

    if not semgrep_available():
        raise SystemExit("semgrep not on PATH. Install: pip install semgrep")

    suite = load_suite()
    entries = suite_fixtures(suite, tier="gold") if args.gold_only and not args.all_suite else suite.get("fixtures", [])
    con = sqlite3.connect(DB) if DB.exists() else None
    processed = with_findings = 0

    for entry in entries:
        fid = entry["fixture_id"]
        repo = entry["repo"]
        diff = resolve_diff(entry)
        if not diff:
            print(f"skip {fid}: no diff")
            continue
        added = parse_added_cs(diff)
        if not added:
            doc = {"tool": "Semgrep", "finding_count": 0, "alerts": [], "harvested_at_utc": datetime.now(timezone.utc).isoformat()}
            write_competitor_artifact(fid, "semgrep.json", doc)
            if con:
                sync_db(con, fid, repo, 0, None, None, None)
            continue

        with tempfile.TemporaryDirectory(prefix=f"gci-semgrep-{fid}-") as tmp:
            root = Path(tmp)
            for rel, lines in added.items():
                target = root / sanitize_name(rel)
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_text("\n".join(lines) + "\n", encoding="utf-8")
            alerts, raw = run_semgrep(root, args.config)

        rules = ",".join(sorted({a["rule_id"] for a in alerts if a.get("rule_id")})) or None
        sev_rank = {"CRITICAL": 5, "HIGH": 4, "MEDIUM": 3, "LOW": 2, "INFO": 1}
        highest = max((a.get("severity", "INFO") for a in alerts), key=lambda s: sev_rank.get(s.upper(), 0), default="INFO") if alerts else None

        write_competitor_artifact(
            fid,
            "semgrep.json",
            {
                "tool": "Semgrep",
                "config": args.config,
                "harvested_at_utc": datetime.now(timezone.utc).isoformat(),
                "finding_count": len(alerts),
                "alerts": alerts,
            },
        )
        if con:
            sync_db(con, fid, repo, len(alerts), rules, highest, raw)
        processed += 1
        if alerts:
            with_findings += 1
        print(f"{fid}: {len(alerts)} finding(s)")

    if con:
        con.commit()
        con.close()
    print(f"done processed={processed} with_findings={with_findings}")


if __name__ == "__main__":
    main()

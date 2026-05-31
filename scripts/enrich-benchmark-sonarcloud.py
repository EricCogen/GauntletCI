#!/usr/bin/env python3
"""Enrich gold benchmark fixtures with SonarCloud issues on changed .cs files."""
from __future__ import annotations

import argparse
import json
import sqlite3
import time
import urllib.parse
import urllib.request
from datetime import datetime, timezone

from benchmark_lib import (
    DB,
    load_suite,
    parse_changed_cs,
    resolve_diff,
    suite_fixtures,
    write_competitor_artifact,
)

SONAR_API = "https://sonarcloud.io/api"


def http_get(url: str) -> dict | None:
    try:
        req = urllib.request.Request(url, headers={"Accept": "application/json"})
        with urllib.request.urlopen(req, timeout=60) as resp:
            return json.loads(resp.read().decode("utf-8"))
    except Exception:
        return None


def project_exists(key: str) -> bool:
    data = http_get(f"{SONAR_API}/components/show?component={urllib.parse.quote(key)}")
    return bool(data and data.get("component"))


def find_project_key(owner: str, repo: str) -> str | None:
    conventional = f"{owner.lower()}_{repo.lower()}"
    if project_exists(conventional):
        return conventional
    url = (
        f"{SONAR_API}/components/search?organization={urllib.parse.quote(owner.lower())}"
        f"&q={urllib.parse.quote(repo)}&qualifiers=TRK&ps=50"
    )
    data = http_get(url)
    if not data:
        return None
    for comp in data.get("components", []):
        if (comp.get("name") or "").lower() == repo.lower():
            return comp.get("key")
    return None


def fetch_issues(project_key: str) -> list[dict]:
    issues: list[dict] = []
    page = 1
    while page <= 20:
        url = (
            f"{SONAR_API}/issues/search?componentKeys={urllib.parse.quote(project_key)}"
            "&statuses=OPEN,CONFIRMED&types=BUG,VULNERABILITY"
            f"&ps=500&p={page}"
        )
        data = http_get(url)
        if not data:
            break
        batch = data.get("issues", [])
        if not batch:
            break
        for issue in batch:
            comp = issue.get("component", "")
            file_path = comp.split(":", 1)[-1] if ":" in comp else comp
            issues.append(
                {
                    "changed_file": file_path,
                    "sonar_rule": issue.get("rule", ""),
                    "sonar_type": issue.get("type", ""),
                    "sonar_severity": issue.get("severity", ""),
                    "message": issue.get("message", ""),
                    "start_line": issue.get("line"),
                }
            )
        if len(batch) < 500:
            break
        page += 1
        time.sleep(1.0)
    return issues


def sync_db(con: sqlite3.Connection, fixture_id: str, project_key: str, alerts: list[dict], changed: set[str]) -> int:
    con.execute(
        """
        CREATE TABLE IF NOT EXISTS sonar_matches (
            fixture_id TEXT NOT NULL,
            sonar_project_key TEXT NOT NULL,
            changed_file TEXT NOT NULL,
            sonar_rule TEXT NOT NULL,
            sonar_severity TEXT,
            sonar_type TEXT,
            sonar_message TEXT,
            fetched_at_utc TEXT,
            UNIQUE(fixture_id, changed_file, sonar_rule)
        )
        """
    )
    n = 0
    ts = datetime.now(timezone.utc).isoformat()
    for alert in alerts:
        if alert["changed_file"] not in changed:
            continue
        con.execute(
            """
            INSERT OR IGNORE INTO sonar_matches
            (fixture_id, sonar_project_key, changed_file, sonar_rule, sonar_severity, sonar_type, sonar_message, fetched_at_utc)
            VALUES (?,?,?,?,?,?,?,?)
            """,
            (
                fixture_id,
                project_key,
                alert["changed_file"],
                alert["sonar_rule"],
                alert["sonar_severity"],
                alert["sonar_type"],
                alert["message"],
                ts,
            ),
        )
        n += 1
    return n


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--gold-only", action="store_true", default=True)
    ap.add_argument("--all-suite", action="store_true")
    args = ap.parse_args()
    suite = load_suite()
    entries = suite_fixtures(suite, tier="gold") if args.gold_only and not args.all_suite else suite.get("fixtures", [])

    con = sqlite3.connect(DB) if DB.exists() else None
    cache_projects: dict[str, str | None] = {}
    cache_issues: dict[str, list[dict]] = {}
    processed = matched = total = 0

    for entry in entries:
        fid = entry["fixture_id"]
        repo = entry["repo"]
        diff = resolve_diff(entry)
        if not diff:
            print(f"skip {fid}: no diff")
            continue
        changed = parse_changed_cs(diff)
        if not changed:
            continue
        owner, name = repo.split("/", 1)
        if repo not in cache_projects:
            cache_projects[repo] = find_project_key(owner, name)
            time.sleep(0.3)
        project_key = cache_projects[repo]
        if not project_key:
            print(f"{fid}: no SonarCloud project")
            write_competitor_artifact(
                fid,
                "sonarcloud.json",
                {"tool": "SonarCloud", "project_key": None, "alerts": [], "harvested_at_utc": datetime.now(timezone.utc).isoformat()},
            )
            continue
        if project_key not in cache_issues:
            cache_issues[project_key] = fetch_issues(project_key)
        alerts = cache_issues[project_key]
        on_diff = [a for a in alerts if a["changed_file"] in changed]
        processed += 1
        if con:
            total += sync_db(con, fid, project_key, alerts, changed)
        write_competitor_artifact(
            fid,
            "sonarcloud.json",
            {
                "tool": "SonarCloud",
                "project_key": project_key,
                "harvested_at_utc": datetime.now(timezone.utc).isoformat(),
                "alerts": on_diff,
                "repo_open_issues": len(alerts),
            },
        )
        if on_diff:
            matched += 1
        print(f"{fid}: {len(on_diff)} on-diff ({len(alerts)} project issues)")

    if con:
        con.commit()
        con.close()
    print(f"done processed={processed} fixtures_with_on_diff={matched} db_rows={total}")


if __name__ == "__main__":
    main()

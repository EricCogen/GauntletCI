#!/usr/bin/env python3
"""Enrich code_scanning_matches for eval/benchmark-suite.json fixtures via GitHub API."""
from __future__ import annotations

import json
import os
import sqlite3
import subprocess
import time
from datetime import datetime, timezone
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
DB = Path(os.environ.get("USERPROFILE", "")) / ".gauntletci" / "corpus.db"
SUITE = REPO / "eval" / "benchmark-suite.json"
FIXTURES_ROOT = REPO / "data" / "fixtures"

from benchmark_lib import (  # noqa: E402
    load_github_token,
    load_suite as load_benchmark_suite,
    resolve_diff as lib_resolve_diff,
    suite_fixtures,
    write_competitor_artifact,
)


def gh_env() -> dict[str, str]:
    env = os.environ.copy()
    if not env.get("GH_TOKEN") and not env.get("GITHUB_TOKEN"):
        token = load_github_token()
        if token:
            env["GH_TOKEN"] = token
            env["GITHUB_TOKEN"] = token
    return env


def gh_run(args: list[str]) -> subprocess.CompletedProcess[str]:
    return subprocess.run(["gh"] + args, capture_output=True, text=True, env=gh_env())


def gh_json(args: list[str]) -> list | dict | None:
    proc = gh_run(args)
    if proc.returncode != 0:
        return None
    if not proc.stdout.strip():
        return []
    return json.loads(proc.stdout)


def parse_changed_cs(diff_path: Path) -> set[str]:
    paths: set[str] = set()
    if not diff_path.exists():
        return paths
    for line in diff_path.read_text(encoding="utf-8", errors="replace").splitlines():
        if line.startswith("+++ b/") and line.endswith(".cs"):
            paths.add(line[6:])
    return paths


def resolve_diff(entry: dict) -> Path | None:
    return lib_resolve_diff(entry)


def fetch_alerts(repo: str, cache: dict) -> list[dict]:
    if repo in cache:
        return cache[repo]
    owner, name = repo.split("/", 1)
    data = gh_json([
        "api",
        f"repos/{owner}/{name}/code-scanning/alerts",
        "-f", "state=open",
        "-f", "tool_name=CodeQL",
        "-f", "per_page=100",
    ])
    alerts: list[dict] = []
    if isinstance(data, list):
        for a in data:
            inst = a.get("most_recent_instance") or {}
            loc = inst.get("location") or {}
            rule = a.get("rule") or {}
            alerts.append(
                {
                    "repo": repo,
                    "file": loc.get("path", ""),
                    "rule_id": rule.get("id", ""),
                    "rule_name": rule.get("name", ""),
                    "severity": rule.get("severity", ""),
                    "message": (inst.get("message") or {}).get("text", ""),
                    "start_line": loc.get("start_line"),
                    "tool_name": (a.get("tool") or {}).get("name", "CodeQL"),
                    "state": a.get("state", "open"),
                }
            )
    cache[repo] = alerts
    time.sleep(0.15)
    return alerts




def probe_code_scanning() -> str | None:
    if not load_github_token():
        return (
            "No GitHub token. Use gh auth login, GH_TOKEN, or ~/.tokens/github.token "
            "(public_repo + security_events for code-scanning/alerts)."
        )
    proc = gh_run(["api", "user", "-q", ".login"])
    if proc.returncode != 0:
        combined = (proc.stderr or "") + (proc.stdout or "")
        if "401" in combined or "403" in combined:
            return (
                "GitHub token rejected. Ensure PAT includes repo and security_events scopes, "
                "or use gh auth login."
            )
        return "GitHub API error: " + combined.strip()[:200]
    return None

def main() -> None:
    import argparse

    ap = argparse.ArgumentParser()
    ap.add_argument("--gold-only", action="store_true", help="Only gold-tier fixtures")
    ap.add_argument("--all-suite", action="store_true", help="All fixtures in benchmark-suite.json")
    args = ap.parse_args()

    err = probe_code_scanning()
    if err:
        raise SystemExit(err)
    suite = load_benchmark_suite()
    if args.all_suite:
        entries = suite.get("fixtures", [])
    elif args.gold_only:
        entries = suite_fixtures(suite, tier="gold")
    else:
        entries = suite.get("fixtures", [])
    con = sqlite3.connect(DB)
    con.execute(
        """
        CREATE TABLE IF NOT EXISTS code_scanning_matches (
            fixture_id TEXT NOT NULL,
            repo TEXT NOT NULL,
            changed_file TEXT NOT NULL,
            codeql_rule TEXT NOT NULL,
            codeql_rule_name TEXT,
            alert_state TEXT,
            tool_name TEXT,
            severity TEXT,
            start_line INTEGER,
            message TEXT,
            fetched_at_utc TEXT,
            UNIQUE(fixture_id, changed_file, codeql_rule)
        )
        """
    )
    cache: dict[str, list[dict]] = {}
    processed = 0
    matched_fixtures = 0
    total = 0
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
        alerts = fetch_alerts(repo, cache)
        processed += 1
        on_diff: list[dict] = []
        n = 0
        for alert in alerts:
            if alert["file"] not in changed:
                continue
            on_diff.append(
                {
                    "changed_file": alert["file"],
                    "codeql_rule": alert["rule_id"],
                    "codeql_rule_name": alert["rule_name"],
                    "severity": alert["severity"],
                    "message": alert["message"],
                    "start_line": alert["start_line"],
                }
            )
            con.execute(
                """
                INSERT OR IGNORE INTO code_scanning_matches
                (fixture_id, repo, changed_file, codeql_rule, codeql_rule_name,
                 alert_state, tool_name, severity, start_line, message, fetched_at_utc)
                VALUES (?,?,?,?,?,?,?,?,?,?,?)
                """,
                (
                    fid,
                    repo,
                    alert["file"],
                    alert["rule_id"],
                    alert["rule_name"],
                    alert["state"],
                    alert["tool_name"],
                    alert["severity"],
                    alert["start_line"],
                    alert["message"],
                    datetime.now(timezone.utc).isoformat(),
                ),
            )
            n += 1
            total += 1
        write_competitor_artifact(
            fid,
            "codeql.json",
            {
                "tool": "CodeQL",
                "harvested_at_utc": datetime.now(timezone.utc).isoformat(),
                "alerts": on_diff,
                "repo_open_alerts": len(alerts),
            },
        )
        if n:
            matched_fixtures += 1
            print(f"{fid}: {n} match(es)")
        else:
            print(f"{fid}: 0 matches ({len(alerts)} repo alerts)")
    con.commit()
    con.close()
    print(
        f"done processed={processed} fixtures_with_matches={matched_fixtures} total_matches={total} repos={len(cache)}"
    )


if __name__ == "__main__":
    main()


#!/usr/bin/env python3
"""Harvest Greptile / CodeRabbit / Qodo PR comments from upstream OSS PRs (gold tier)."""
from __future__ import annotations

import argparse
import json
import os
import subprocess
from datetime import datetime, timezone

from benchmark_lib import (
    load_github_token,
    load_suite,
    suite_fixtures,
    write_competitor_artifact,
)

ARTIFACT_BY_TOOL = {
    "greptile": ("Greptile", "greptile.json"),
    "coderabbit": ("CodeRabbit", "coderabbit.json"),
    "qodo": ("Qodo", "qodo.json"),
    "ellipsis": ("Ellipsis", "ellipsis.json"),
}


def gh_json(args: list[str]) -> list | dict | None:
    token = load_github_token()
    env = {}
    if token:
        env["GH_TOKEN"] = token
        env["GITHUB_TOKEN"] = token
    proc = subprocess.run(
        ["gh"] + args,
        capture_output=True,
        text=True,
        env={**os.environ, **env},
        check=False,
    )
    if proc.returncode != 0 or not proc.stdout.strip():
        return None
    return json.loads(proc.stdout)


def classify_login(login: str) -> str | None:
    low = (login or "").lower()
    for key in ARTIFACT_BY_TOOL:
        if key in low:
            return key
    if "code-rabbit" in low or "coderabbitai" in low:
        return "coderabbit"
    return None


def harvest_repo_pr(repo: str, pr: int) -> dict[str, list[dict]]:
    owner, name = repo.split("/", 1)
    base = f"repos/{owner}/{name}/pulls/{pr}"
    buckets: dict[str, list[dict]] = {v[1]: [] for v in ARTIFACT_BY_TOOL.values()}

    for endpoint in ("comments", "reviews"):
        data = gh_json(["api", f"{base}/{endpoint}", "-f", "per_page=100"])
        if not isinstance(data, list):
            continue
        for item in data:
            user = item.get("user") or {}
            login = user.get("login", "")
            tool_key = classify_login(login)
            if not tool_key or tool_key not in ARTIFACT_BY_TOOL:
                continue
            _display, artifact = ARTIFACT_BY_TOOL[tool_key]
            body = item.get("body") or item.get("review_body") or ""
            if not body.strip():
                continue
            buckets.setdefault(artifact, []).append(
                {
                    "author": login,
                    "body": body,
                    "created_at": item.get("created_at") or item.get("submitted_at"),
                    "url": item.get("html_url"),
                }
            )
    return buckets


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--gold-only", action="store_true", default=True)
    ap.add_argument("--all-suite", action="store_true")
    args = ap.parse_args()

    if not load_github_token():
        raise SystemExit("No GitHub token (gh auth login or GH_TOKEN)")

    suite = load_suite()
    entries = suite_fixtures(suite, tier="gold") if args.gold_only and not args.all_suite else suite.get("fixtures", [])
    harvested = 0

    for entry in entries:
        fid = entry["fixture_id"]
        repo = entry["repo"]
        pr = int(entry["pr_number"])
        buckets = harvest_repo_pr(repo, pr)
        any_bot = False
        for artifact, comments in buckets.items():
            if not comments:
                continue
            tool_name = next(n for k, (n, a) in ARTIFACT_BY_TOOL.items() if a == artifact)
            write_competitor_artifact(
                fid,
                artifact,
                {
                    "tool": tool_name,
                    "harvested_at_utc": datetime.now(timezone.utc).isoformat(),
                    "comments": comments,
                    "comment_count": len(comments),
                },
            )
            any_bot = True
        if any_bot:
            harvested += 1
            entry["competitor_harvest"] = "upstream"
        print(f"{fid}: " + ", ".join(f"{k}={len(v)}" for k, v in buckets.items() if v) or "no bot comments")

    print(f"done fixtures_with_bot_comments={harvested}")


if __name__ == "__main__":
    main()

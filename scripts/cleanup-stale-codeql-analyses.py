#!/usr/bin/env python3
"""Delete obsolete CodeQL analysis categories that cause NEUTRAL merge checks."""
from __future__ import annotations

import argparse
import json
import subprocess
import sys

STALE_CATEGORY = ".github/workflows/security.yml:codeql"


def gh_api(path: str, method: str = "GET") -> dict | list | None:
    cmd = ["gh", "api", path, "--method", method]
    try:
        out = subprocess.check_output(cmd, stderr=subprocess.STDOUT)
    except subprocess.CalledProcessError as exc:
        msg = exc.output.decode("utf-8", errors="replace")
        print(msg, file=sys.stderr)
        return None
    text = out.decode("utf-8").strip()
    if not text:
        return {}
    return json.loads(text)


def list_analyses(repo: str, ref: str) -> list[dict]:
    data = gh_api(f"repos/{repo}/code-scanning/analyses?ref={ref}&per_page=100")
    return data if isinstance(data, list) else []


def delete_stale(repo: str, ref: str, *, dry_run: bool) -> int:
    deleted = 0
    while True:
        analyses = list_analyses(repo, ref)
        stale = [a for a in analyses if a.get("category") == STALE_CATEGORY]
        if not stale:
            break
        aid = stale[0]["id"]
        if dry_run:
            print(f"[dry-run] would delete analysis {aid} ({STALE_CATEGORY})")
            break
        if gh_api(f"repos/{repo}/code-scanning/analyses/{aid}?confirm_delete", "DELETE") is None:
            print(f"Stopped after error deleting analysis {aid}", file=sys.stderr)
            break
        deleted += 1
        if deleted % 25 == 0:
            print(f"deleted {deleted}...")
    return deleted


def main() -> None:
    ap = argparse.ArgumentParser(description="Remove stale undifferentiated CodeQL analyses")
    ap.add_argument("--repo", default="EricCogen/GauntletCI", help="owner/repo")
    ap.add_argument("--ref", default="refs/heads/main", help="git ref for analyses")
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()

    before = sorted({a.get("category") for a in list_analyses(args.repo, args.ref)})
    print("categories before:", before)
    n = delete_stale(args.repo, args.ref, dry_run=args.dry_run)
    after = sorted({a.get("category") for a in list_analyses(args.repo, args.ref)})
    print("categories after:", after)
    print("deleted:", n)


if __name__ == "__main__":
    main()

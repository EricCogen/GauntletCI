#!/usr/bin/env python3
"""Replace gold fixtures without validated primary_rules using corpus backfill candidates."""
from __future__ import annotations

import sqlite3
from datetime import datetime, timezone
from pathlib import Path

from benchmark_lib import DB, load_suite, save_suite

REPO = Path(__file__).resolve().parents[1]
GT = REPO / "eval" / "ground-truth"
ANCHOR = "stackexchange-redis-pr-2995"


def resolve_fixture_path(path: str | None) -> Path | None:
    if not path:
        return None
    p = Path(path)
    if not p.is_absolute():
        p = REPO / path.replace("/", "\\").lstrip(".\\")
    return p.resolve()


def has_diff(path: str | None) -> bool:
    p = resolve_fixture_path(path)
    return p is not None and (p / "diff.patch").exists()


def path_in_repo(path: str | None) -> bool:
    p = resolve_fixture_path(path)
    if p is None:
        return False
    try:
        p.relative_to(REPO.resolve())
        return True
    except ValueError:
        return False


def main() -> None:
    suite = load_suite()
    fixtures = suite["fixtures"]
    anchor = [f for f in fixtures if f.get("suite_tier") == "anchor"]
    gold = [f for f in fixtures if f.get("suite_tier") == "gold"]
    other = [f for f in fixtures if f.get("suite_tier") not in ("anchor", "gold")]

    strong = [g for g in gold if g.get("primary_rules")]
    weak = [g for g in gold if not g.get("primary_rules")]
    if not weak:
        print("All gold fixtures have primary_rules")
        return
    if len(gold) > 80 and len(weak) > len(gold) * 0.5:
        print(
            f"Skip reconcile: {len(weak)}/{len(gold)} gold lack primary_rules "
            "(expected for unlabeled scale cohort; promote labels first)"
        )
        return

    keep_ids = {f["fixture_id"] for f in anchor + strong + other}
    dropped_ids = {w["fixture_id"] for w in weak}
    for w in weak:
        gt_path = GT / f"{w['fixture_id']}.json"
        if gt_path.exists():
            gt_path.unlink()
        print(f"drop weak gold: {w['fixture_id']}")

    con = sqlite3.connect(DB)
    candidates = []
    for row in con.execute(
        """
        SELECT fixture_id, repo, pr_number, path,
        (SELECT COUNT(*) FROM expected_findings e WHERE e.fixture_id=f.fixture_id AND e.should_trigger=1)
        FROM fixtures f
        """
    ):
        fid, repo, pr, path, pos = row
        if fid in keep_ids or fid in dropped_ids or fid == ANCHOR:
            continue
        if not has_diff(path):
            continue
        if int(pos or 0) < 1:
            continue
        candidates.append(
            {
                "fixture_id": fid,
                "repo": repo,
                "pr_number": pr,
                "corpus_path": path,
                "positive_labels": int(pos),
            }
        )
    con.close()
    candidates.sort(
        key=lambda x: (
            0 if path_in_repo(x["corpus_path"]) else 1,
            -x["positive_labels"],
            x["repo"],
        )
    )

    replacements: list[dict] = []
    used_repos: set[str] = {g["repo"] for g in strong}
    used_ids: set[str] = set()

    def try_add(c: dict) -> bool:
        if len(replacements) >= len(weak):
            return False
        if c["fixture_id"] in used_ids:
            return False
        replacements.append(
            {
                "fixture_id": c["fixture_id"],
                "repo": c["repo"],
                "pr_number": c["pr_number"],
                "suite_tier": "gold",
                "domain_profile": "library",
                "primary_rules": [],
                "defect_ids": [],
                "competitor_harvest": "none",
                "ci_regression": False,
                "corpus_path": c["corpus_path"],
            }
        )
        used_ids.add(c["fixture_id"])
        used_repos.add(c["repo"])
        print(f"add backfill gold: {c['fixture_id']} ({c['positive_labels']} labels)")
        return True

    for c in candidates:
        if c["repo"] in used_repos:
            continue
        try_add(c)
    for c in candidates:
        try_add(c)

    new_gold = strong + replacements
    suite["fixtures"] = anchor + new_gold + other
    suite["selection_report"]["selected_gold"] = len(new_gold)
    suite["generated_at_utc"] = datetime.now(timezone.utc).isoformat()
    save_suite(suite)
    print(f"Replaced {len(weak)} weak gold; suite now has {len(new_gold)} gold (run promote --refresh-all)")


if __name__ == "__main__":
    main()

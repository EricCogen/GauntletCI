#!/usr/bin/env python3
"""Select stratified benchmark fixtures from corpus.db into eval/benchmark-suite.json."""
from __future__ import annotations

import argparse
import json
import os
import sqlite3
from collections import Counter, defaultdict
from datetime import datetime, timezone
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
DEFAULT_DB = Path(os.environ.get("USERPROFILE", "")) / ".gauntletci" / "corpus.db"
DEFAULT_OUT = REPO / "eval" / "benchmark-suite.json"
ANCHOR_ID = "stackexchange-redis-pr-2995"


def fixture_slug(repo: str, pr: int) -> str:
    slug = repo.split("/", 1)[-1].lower().replace(".", "-")
    return f"{slug}-pr-{pr}"


def has_diff(path: str | None) -> bool:
    if not path:
        return False
    return (Path(path) / "diff.patch").exists()


def fetch_candidates(con: sqlite3.Connection) -> list[dict]:
    sql = (
        "SELECT f.fixture_id, f.repo, f.pr_number, f.tier, f.pr_size_bucket, f.path, "
        "(SELECT COUNT(*) FROM code_scanning_matches c WHERE c.fixture_id = f.fixture_id), "
        "(SELECT COUNT(*) FROM expected_findings e WHERE e.fixture_id = f.fixture_id AND e.should_trigger = 1) "
        "FROM fixtures f"
    )
    rows = con.execute(sql).fetchall()
    out: list[dict] = []
    for fid, repo, pr, tier, size, path, codescan, pos in rows:
        if not has_diff(path):
            continue
        out.append(
            {
                "fixture_id": fid or fixture_slug(repo, pr),
                "repo": repo,
                "pr_number": pr,
                "corpus_tier": tier,
                "pr_size_bucket": size or "unknown",
                "path": path,
                "codescan_hits": int(codescan or 0),
                "positive_labels": int(pos or 0),
            }
        )
    return out


def pick(candidates: list[dict], max_n: int, per_repo_cap: int, exclude: set[str]) -> list[dict]:
    chosen: list[dict] = []
    repo_counts: Counter[str] = Counter()
    pool = sorted(
        candidates,
        key=lambda x: (-x["codescan_hits"], -x["positive_labels"], x["repo"], x["pr_number"]),
    )
    for item in pool:
        if item["fixture_id"] in exclude:
            continue
        if len(chosen) >= max_n:
            break
        if repo_counts[item["repo"]] >= per_repo_cap:
            continue
        chosen.append(item)
        repo_counts[item["repo"]] += 1
    return chosen


def to_entry(c: dict, suite_tier: str) -> dict:
    return {
        "fixture_id": c["fixture_id"],
        "repo": c["repo"],
        "pr_number": c["pr_number"],
        "suite_tier": suite_tier,
        "domain_profile": "library",
        "primary_rules": [],
        "defect_ids": [],
        "competitor_harvest": "anchor" if suite_tier == "anchor" else "none",
        "ci_regression": suite_tier in ("anchor", "gold", "smoke"),
        "corpus_path": c.get("path"),
        "pr_size_bucket": c.get("pr_size_bucket"),
    }


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--db", type=Path, default=DEFAULT_DB)
    ap.add_argument("--out", type=Path, default=DEFAULT_OUT)
    ap.add_argument("--write", action="store_true")
    ap.add_argument("--gold-max", type=int, default=15)
    ap.add_argument("--silver-max", type=int, default=30)
    ap.add_argument("--smoke-max", type=int, default=8)
    ap.add_argument("--per-repo-cap", type=int, default=2)
    args = ap.parse_args()

    if not args.db.exists():
        raise SystemExit(f"Corpus DB not found: {args.db}")

    anchor_entry = {
        "fixture_id": ANCHOR_ID,
        "repo": "StackExchange/StackExchange.Redis",
        "pr_number": 2995,
        "suite_tier": "anchor",
        "domain_profile": "library",
        "primary_rules": ["GCI0058", "GCI0007"],
        "defect_ids": ["paired-implementation-inversion", "swallowed-handler-exception"],
        "competitor_harvest": "anchor",
        "ci_regression": True,
    }

    con = sqlite3.connect(args.db)
    candidates = fetch_candidates(con)
    candidates = [
        c for c in candidates
        if not (c["repo"] == "StackExchange/StackExchange.Redis" and c["pr_number"] == 2995)
    ]
    con.close()

    exclude = {ANCHOR_ID}
    candidates = [c for c in candidates if not (c["repo"] == "StackExchange/StackExchange.Redis" and c["pr_number"] == 2995)]
    gold_pool = [c for c in candidates if str(c.get("corpus_tier", "")).lower() == "gold" or c["positive_labels"] >= 2]
    silver_pool = [c for c in candidates if c["fixture_id"] not in {x["fixture_id"] for x in gold_pool}]
    smoke_pool = [c for c in candidates if c["pr_size_bucket"] in ("Tiny", "Small")]

    gold = pick(gold_pool, args.gold_max, args.per_repo_cap, exclude)
    exclude |= {c["fixture_id"] for c in gold}
    silver = pick(silver_pool, args.silver_max, args.per_repo_cap, exclude)
    exclude |= {c["fixture_id"] for c in silver}
    smoke = pick(smoke_pool, args.smoke_max, 3, exclude)

    fixtures = [anchor_entry]
    fixtures += [to_entry(c, "gold") for c in gold]
    fixtures += [to_entry(c, "silver") for c in silver]
    fixtures += [to_entry(c, "smoke") for c in smoke]

    report = {
        "candidates_with_diff": len(candidates),
        "selected_gold": len(gold),
        "selected_silver": len(silver),
        "selected_smoke": len(smoke),
        "repos": len({f["repo"] for f in fixtures}),
    }

    doc = {
        "schema_version": "1.0.0",
        "generated_at_utc": datetime.now(timezone.utc).isoformat(),
        "description": "Cross-repo competitive benchmark manifest",
        "corpus_db": str(args.db),
        "selection_report": report,
        "fixtures": fixtures,
    }

    print(json.dumps(report, indent=2))
    if args.write:
        args.out.write_text(json.dumps(doc, indent=2) + "\n", encoding="utf-8")
        print(f"Wrote {args.out} ({len(fixtures)} fixtures)")


if __name__ == "__main__":
    main()
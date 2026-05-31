"""Shared helpers for eval benchmark scripts."""
from __future__ import annotations

import json
import os
import sqlite3
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
EVAL = REPO / "eval"
SUITE_PATH = EVAL / "benchmark-suite.json"
DB = Path(os.environ.get("USERPROFILE", "")) / ".gauntletci" / "corpus.db"
FIXTURES_ROOT = REPO / "data" / "fixtures"
DIFFS = EVAL / "diffs"
RUNS = EVAL / "runs" / "gauntletci"
COMPETITOR = EVAL / "competitor-runs"


def load_json(path: Path) -> dict | list:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def load_suite() -> dict:
    return load_json(SUITE_PATH)


def suite_fixtures(
    suite: dict,
    *,
    tier: str | None = None,
    ci_regression: bool | None = None,
    primary_rules_only: bool = False,
) -> list[dict]:
    out: list[dict] = []
    for entry in suite.get("fixtures", []):
        if tier and entry.get("suite_tier") != tier:
            continue
        if ci_regression is not None and bool(entry.get("ci_regression")) != ci_regression:
            continue
        if primary_rules_only and not entry.get("primary_rules"):
            continue
        out.append(entry)
    return out


def resolve_diff(entry: dict) -> Path | None:
    fid = entry["fixture_id"]
    cached = DIFFS / f"{fid}.patch"
    if cached.exists():
        return cached
    cp = entry.get("corpus_path")
    if cp:
        p = REPO / cp.replace("/", os.sep) / "diff.patch"
        if p.exists():
            return p
    for tier in ("discovery", "silver", "gold", "Discovery", "Silver", "Gold"):
        p = FIXTURES_ROOT / tier.lower() / fid / "diff.patch"
        if p.exists():
            return p
    if DB.exists():
        con = sqlite3.connect(DB)
        row = con.execute("SELECT path FROM fixtures WHERE fixture_id=?", (fid,)).fetchone()
        con.close()
        if row and row[0]:
            p = Path(row[0]) / "diff.patch"
            if p.exists():
                return p
    return None


def parse_changed_cs(diff_path: Path) -> set[str]:
    paths: set[str] = set()
    if not diff_path.exists():
        return paths
    for line in diff_path.read_text(encoding="utf-8", errors="replace").splitlines():
        if line.startswith("+++ b/") and line.endswith(".cs"):
            paths.add(line[6:])
    return paths


def write_competitor_artifact(fixture_id: str, artifact: str, doc: dict) -> Path:
    dest = COMPETITOR / fixture_id
    dest.mkdir(parents=True, exist_ok=True)
    path = dest / artifact
    path.write_text(json.dumps(doc, indent=2) + "\n", encoding="utf-8")
    return path

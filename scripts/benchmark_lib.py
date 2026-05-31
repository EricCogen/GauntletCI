"""Shared helpers for eval benchmark scripts."""
from __future__ import annotations

import json
import os
import re
import sqlite3
from datetime import datetime, timezone
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
EVAL = REPO / "eval"
SUITE_PATH = EVAL / "benchmark-suite.json"
DB = Path(os.environ.get("USERPROFILE", "")) / ".gauntletci" / "corpus.db"
FIXTURES_ROOT = REPO / "data" / "fixtures"
DIFFS = EVAL / "diffs"
RUNS = EVAL / "runs" / "gauntletci"
COMPETITOR = EVAL / "competitor-runs"
TOKEN_DIR = Path(os.environ.get("USERPROFILE", "")) / ".tokens"


def load_json(path: Path) -> dict | list:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def load_suite() -> dict:
    return load_json(SUITE_PATH)


def save_suite(doc: dict) -> None:
    doc["generated_at_utc"] = datetime.now(timezone.utc).isoformat()
    SUITE_PATH.write_text(json.dumps(doc, indent=2) + "\n", encoding="utf-8")


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


def resolve_corpus_path(path: str | None) -> Path | None:
    if not path:
        return None
    p = Path(path)
    if not p.is_absolute():
        p = REPO / str(path).replace("/", os.sep).lstrip(".\\")
    return p.resolve()


def corpus_diff_exists(path: str | None) -> bool:
    p = resolve_corpus_path(path)
    return p is not None and (p / "diff.patch").exists()


def resolve_diff(entry: dict) -> Path | None:
    fid = entry["fixture_id"]
    cached = DIFFS / f"{fid}.patch"
    if cached.exists():
        return cached
    cp = entry.get("corpus_path")
    if cp:
        base = resolve_corpus_path(cp)
        if base:
            p = base / "diff.patch"
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


def parse_diff_hunk_files(diff_path: Path) -> dict[str, list[str]]:
    """Build per-.cs file content from diff hunks (context + added lines) for richer static replay."""
    by_file: dict[str, list[str]] = {}
    current: str | None = None
    for line in diff_path.read_text(encoding="utf-8", errors="replace").splitlines():
        if line.startswith("+++ b/"):
            path = line[6:]
            current = path if path.endswith(".cs") else None
            if current and current not in by_file:
                by_file[current] = []
            continue
        if not current:
            continue
        if line.startswith(("+++", "---", "@@")):
            continue
        if line.startswith("+") and not line.startswith("+++"):
            by_file[current].append(line[1:])
        elif line.startswith(" ") and not line.startswith("---"):
            by_file[current].append(line[1:])
    return by_file


def write_competitor_artifact(fixture_id: str, artifact: str, doc: dict) -> Path:
    dest = COMPETITOR / fixture_id
    dest.mkdir(parents=True, exist_ok=True)
    path = dest / artifact
    path.write_text(json.dumps(doc, indent=2) + "\n", encoding="utf-8")
    return path


def load_github_token() -> str | None:
    import subprocess

    for key in ("GH_TOKEN", "GITHUB_TOKEN"):
        val = os.environ.get(key)
        if val:
            return val.strip()
    try:
        proc = subprocess.run(
            ["gh", "auth", "token"],
            capture_output=True,
            text=True,
            timeout=15,
            check=False,
        )
        if proc.returncode == 0 and proc.stdout.strip():
            return proc.stdout.strip()
    except Exception:
        pass
    for name in ("github.token", "gh.token", "cursor_security.token"):
        path = TOKEN_DIR / name
        if path.exists():
            return path.read_text(encoding="utf-8").strip()
    return None


def load_optional_token(filename: str) -> str | None:
    env_key = filename.upper().replace(".", "_").replace("-", "_")
    if os.environ.get(env_key):
        return os.environ[env_key].strip()
    path = TOKEN_DIR / filename
    if path.exists():
        return path.read_text(encoding="utf-8").strip()
    return None


def enrich_finding_metadata(findings: list[dict], rule_id: str) -> dict:
    """First finding for rule with file/line/keywords for ground truth."""
    for f in findings:
        if f.get("RuleId") != rule_id:
            continue
        fp = f.get("FilePath") or ""
        line = f.get("Line")
        evidence = f.get("Evidence") or ""
        keywords = [rule_id]
        if fp:
            keywords.append(Path(fp).name)
            keywords.append(fp.replace("\\", "/"))
        summary = f.get("Summary") or ""
        for token in re.findall(r"[A-Za-z_][A-Za-z0-9_]{3,}", summary):
            if token not in keywords and len(keywords) < 8:
                keywords.append(token)
        return {
            "file": fp.replace("\\", "/"),
            "line": line,
            "match_keywords": keywords[:10],
            "evidence_excerpt": evidence[:240],
        }
    return {"file": "", "line": None, "match_keywords": [rule_id], "evidence_excerpt": ""}

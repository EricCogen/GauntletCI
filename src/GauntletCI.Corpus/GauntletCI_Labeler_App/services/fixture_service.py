from __future__ import annotations

import json
import re
from pathlib import Path
from typing import Any


def parse_json_file(path: Path) -> Any:
    if not path.exists():
        return None
    try:
        with path.open("r", encoding="utf-8") as fh:
            return json.load(fh)
    except Exception:
        return None


def read_text_file(path: Path) -> str:
    if not path.exists():
        return ""
    try:
        return path.read_text(encoding="utf-8", errors="replace")
    except Exception:
        return ""


def _safe_fixture_id(fixture_id: str) -> str:
    """Strip path-traversal sequences from a fixture ID."""
    sanitized = re.sub(r'[/\\]', '', fixture_id)
    return sanitized.strip('.')


def locate_fixture_dir(fixtures_root: str, fixture: dict[str, Any]) -> Path | None:
    root = Path(fixtures_root)
    if not root.exists():
        return None

    fixture_id = _safe_fixture_id(fixture["fixture_id"])
    tier = str(fixture.get("tier") or "").lower()

    candidates = [root / tier / fixture_id, root / fixture_id]
    raw_path = (fixture.get("path") or "").replace("\\", "/")
    if raw_path:
        path_obj = Path(raw_path)
        if path_obj.exists():
            candidates.insert(0, path_obj)
        else:
            if "fixtures/" in raw_path:
                suffix = raw_path.split("fixtures/", 1)[1]
                candidates.append(root.parent / "fixtures" / suffix)

    for candidate in candidates:
        if candidate.exists() and candidate.is_dir():
            return candidate

    for hit in root.rglob(fixture_id):
        if hit.is_dir():
            return hit
    return None


def load_fixture_artifacts(fixtures_root: str, fixture: dict[str, Any]) -> dict[str, Any]:
    fixture_dir = locate_fixture_dir(fixtures_root, fixture)
    if fixture_dir is None:
        return {
            "fixture_dir": None,
            "metadata": None,
            "notes": "",
            "diff_patch": "",
            "actual_json": None,
            "expected_json": None,
            "pr_json": None,
            "files_json": None,
            "review_comments": None,
        }

    raw_dir = fixture_dir / "raw"
    return {
        "fixture_dir": str(fixture_dir),
        "metadata": parse_json_file(fixture_dir / "metadata.json"),
        "notes": read_text_file(fixture_dir / "notes.md"),
        "diff_patch": read_text_file(fixture_dir / "diff.patch"),
        "actual_json": parse_json_file(fixture_dir / "actual.json"),
        "expected_json": parse_json_file(fixture_dir / "expected.json"),
        # Not needed for the redesigned task page. Keep keys for compatibility.
        "pr_json": None,
        "files_json": None,
        "review_comments": parse_json_file(raw_dir / "review-comments.json"),
    }


_SNIPPET_STOP = frozenset({
    "the", "a", "an", "in", "on", "at", "to", "for", "of", "and", "or",
    "is", "was", "found", "detected", "added", "changed", "new", "not",
    "with", "has", "from", "that", "this", "are", "been", "being",
})
_FILE_EXT_RE = re.compile(
    r"[\w/\\.\-]+\.(?:cs|ts|tsx|js|jsx|py|go|java|rb|cpp|hpp|c|h|json|yaml|yml|xml)",
    re.IGNORECASE,
)


def _snippet_terms(text: str) -> list[str]:
    seen: set[str] = set()
    terms: list[str] = []
    for t in re.findall(r"\b\w{4,}\b", text):
        low = t.lower()
        if low not in _SNIPPET_STOP and low not in seen:
            seen.add(low)
            terms.append(t)
        if len(terms) >= 8:
            break
    return terms


def _find_file_section(lines: list[str], file_path: str) -> int:
    """Return index of the `diff --git` header whose path matches file_path, or -1."""
    norm = file_path.replace("\\", "/").lstrip("/")
    for i, line in enumerate(lines):
        if line.startswith("diff --git ") and norm in line:
            return i
    # Looser match: just the filename
    name = norm.rsplit("/", 1)[-1]
    if name:
        for i, line in enumerate(lines):
            if line.startswith("diff --git ") and name in line:
                return i
    return -1


def _format_snippet(lines: list[str], start: int, end: int) -> str:
    header = f"... lines {start + 1}–{end} of {len(lines)} ..."
    return header + "\n" + "\n".join(lines[start:end])


def extract_diff_snippet(diff_patch: str, search_text: str, context_lines: int = 15) -> str:
    """Return a relevant portion of a unified diff.

    Strategies (in priority order):
    1. Locate the file section whose path appears in search_text — most accurate for
       multi-file diffs where keyword matching could land in the wrong file.
    2. Keyword-score every added/removed/hunk line; require ≥ 2 matches.
       Added lines are weighted 2× because that is what the rule flagged.
    3. Fall back to the first real code hunk (the first @@ block), which is always
       better than raw lines[:40] which shows only git/binary headers.
    """
    if not diff_patch:
        return ""

    lines = diff_patch.splitlines()

    # Strategy 1: file-path anchor
    for candidate in _FILE_EXT_RE.findall(search_text):
        idx = _find_file_section(lines, candidate)
        if idx >= 0:
            hunk_start = idx
            for j in range(idx, min(len(lines), idx + 10)):
                if lines[j].startswith("@@"):
                    hunk_start = max(0, j - 1)  # include the +++ b/... line
                    break
            end = min(len(lines), hunk_start + context_lines * 2 + 1)
            return _format_snippet(lines, hunk_start, end)

    # Strategy 2: keyword score — require ≥2 to avoid spurious matches
    terms = _snippet_terms(search_text)
    best, best_score = -1, 0
    for i, line in enumerate(lines):
        if not (line.startswith("+") or line.startswith("-") or line.startswith("@@")):
            continue
        score = sum(1 for t in terms if t.lower() in line.lower())
        if line.startswith("+") and not line.startswith("+++"):
            score *= 2  # prefer added lines (the rule flagged these)
        if score > best_score:
            best_score, best = score, i

    if best >= 0 and best_score >= 2:
        start = max(0, best - context_lines)
        end = min(len(lines), best + context_lines + 1)
        return _format_snippet(lines, start, end)

    # Strategy 3: first real hunk — always better than first N raw lines
    for i, line in enumerate(lines):
        if line.startswith("@@"):
            start = max(0, i - 1)  # include the +++ b/... path line
            end = min(len(lines), start + context_lines * 2 + 1)
            return _format_snippet(lines, start, end)

    return "\n".join(lines[:50])

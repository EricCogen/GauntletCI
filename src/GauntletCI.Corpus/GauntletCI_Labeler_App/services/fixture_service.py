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
_LINE_NUM_RE = re.compile(r'\b[Ll]ines?\s+(\d+)', re.IGNORECASE)
_HUNK_HEADER_RE = re.compile(r'^@@ -\d+(?:,\d+)? \+(\d+)(?:,\d+)? @@')


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


def _find_line_in_section(lines: list[str], section_start: int, target_new_line: int) -> int:
    """Walk hunks in one file section and return the lines[] index closest to target_new_line.

    Hunk headers (`@@ -A +N @@`) tell us the new-file starting line via `+N`.
    We track the running new-file counter and stop when we reach or pass the target.
    """
    current = 0
    best_idx = section_start
    best_dist: float = float("inf")
    i = section_start
    while i < len(lines):
        line = lines[i]
        if i > section_start and line.startswith("diff --git "):
            break
        m = _HUNK_HEADER_RE.match(line)
        if m:
            current = int(m.group(1))
            i += 1
            continue
        if (line.startswith("+") and not line.startswith("+++")) or line.startswith(" "):
            dist = abs(current - target_new_line)
            if dist < best_dist:
                best_dist, best_idx = dist, i
            if current >= target_new_line:
                return i  # reached or passed — closest line found
            current += 1
        # "-" lines don't advance the new-file counter
        i += 1
    return best_idx


def _format_snippet(lines: list[str], start: int, end: int) -> str:
    """Return the slice lines[start:end] prefixed with an accurate @@ header.

    For mid-hunk slices the raw hunk header has the wrong starting line (e.g.
    "+1" when the interesting code is at line 3 100).  We walk from the hunk
    body start up to `start`, counting how many new-file / old-file lines were
    consumed, then emit a *synthetic* adjusted header so the browser JS can
    initialise its line-number counters at the correct offset.
    """
    # Locate the nearest preceding @@ header
    hunk_idx = -1
    for k in range(start - 1, -1, -1):
        if lines[k].startswith("@@"):
            hunk_idx = k
            break

    header = f"... lines {start + 1}–{end} of {len(lines)} ..."

    if hunk_idx < 0:
        return "\n".join([header] + lines[start:end])

    m = re.match(r'^@@ -(\d+)(?:,\d+)? \+(\d+)(?:,\d+)? @@', lines[hunk_idx])
    if m:
        adj_old = int(m.group(1))
        adj_new = int(m.group(2))
        for k in range(hunk_idx + 1, start):
            ln = lines[k]
            if ln.startswith('+') and not ln.startswith('+++'):
                adj_new += 1
            elif ln.startswith('-') and not ln.startswith('---'):
                adj_old += 1
            else:
                # context line — advances both counters
                adj_old += 1
                adj_new += 1
        synth_hunk = f"@@ -{adj_old} +{adj_new} @@"
    else:
        synth_hunk = lines[hunk_idx]

    return "\n".join([synth_hunk, header] + lines[start:end])


def extract_diff_snippet(diff_patch: str, search_text: str, context_lines: int = 30) -> str:
    """Return a relevant portion of a unified diff.

    Strategies (in priority order):
    1. File-path anchor + line-number anchor: parse both from search_text (message +
       evidence JSON), find the exact hunk covering that line, center the snippet there.
    2. File-path anchor only: found the file section but no usable line number — show
       from the first hunk in that section.
    3. Keyword score: score every added/removed/hunk line; require ≥ 2 matches.
       Added lines are weighted 2× because that is what the rule flagged.
    4. First real hunk: always better than raw lines[:N] which shows git/binary headers.

    Short diffs (≤ 150 lines) are returned in full — no windowing needed.
    """
    if not diff_patch:
        return ""

    lines = diff_patch.splitlines()

    # Short diffs: show everything
    if len(lines) <= 150:
        return "\n".join(lines)

    # Strategy 1 + 2: file-path anchor
    for candidate in _FILE_EXT_RE.findall(search_text):
        section_start = _find_file_section(lines, candidate)
        if section_start < 0:
            continue

        # Locate the first hunk inside this file section (+++ b/... line included)
        first_hunk = section_start
        for j in range(section_start, min(len(lines), section_start + 15)):
            if lines[j].startswith("@@"):
                first_hunk = max(0, j - 1)
                break

        # Strategy 1: line-number anchor within the section
        line_nums = [int(m) for m in _LINE_NUM_RE.findall(search_text) if int(m) > 0]
        if line_nums:
            center = _find_line_in_section(lines, section_start, line_nums[0])
            start = max(0, center - context_lines)
            end = min(len(lines), center + context_lines + 1)
            return _format_snippet(lines, start, end)

        # Strategy 2: file found, no line number — show from first hunk
        end = min(len(lines), first_hunk + context_lines * 2 + 1)
        return _format_snippet(lines, first_hunk, end)

    # Strategy 3: keyword score — require ≥ 2 to avoid spurious matches
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

    # Strategy 4: first real hunk
    for i, line in enumerate(lines):
        if line.startswith("@@"):
            start = max(0, i - 1)
            end = min(len(lines), start + context_lines * 2 + 1)
            return _format_snippet(lines, start, end)

    return "\n".join(lines[:80])

from __future__ import annotations

import json
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


def locate_fixture_dir(fixtures_root: str, fixture: dict[str, Any]) -> Path | None:
    root = Path(fixtures_root)
    if not root.exists():
        return None

    fixture_id = fixture["fixture_id"]
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
        "pr_json": parse_json_file(raw_dir / "pr.json"),
        "files_json": parse_json_file(raw_dir / "files.json"),
        "review_comments": parse_json_file(raw_dir / "review-comments.json"),
    }

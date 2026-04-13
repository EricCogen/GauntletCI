from __future__ import annotations

import re
from pathlib import Path


def load_rule_definitions(rules_md_path: str) -> dict[str, dict]:
    """
    Parse docs/rules.md and return {rule_id: {name, what, why, action}}.
    Section format:
      ### GCI0001 · Rule Name
      **Confidence:** ...
      **What it detects:** ...
      **Why it matters:** ...
      **Suggested action:** ...
    """
    path = Path(rules_md_path)
    if not path.exists():
        return {}

    text = path.read_text(encoding="utf-8")
    result: dict[str, dict] = {}

    # Split on rule headings so each chunk is one rule's block
    chunks = re.split(r"(?=###\s+GCI\d+\s+[·•])", text)
    heading = re.compile(r"###\s+(GCI\d+)\s+[·•]\s+(.+)")
    field = re.compile(r"\*\*([^*]+):\*\*\s*(.+?)(?=\n\*\*|\n###|\n---|\Z)", re.DOTALL)

    for chunk in chunks:
        hm = heading.search(chunk)
        if not hm:
            continue
        rule_id = hm.group(1).strip()
        name = hm.group(2).strip()
        fields: dict[str, str] = {}
        for fm in field.finditer(chunk):
            key = fm.group(1).strip().lower()
            val = re.sub(r"\s+", " ", fm.group(2)).strip()
            fields[key] = val
        result[rule_id] = {
            "name": name,
            "what": fields.get("what it detects", ""),
            "why": fields.get("why it matters", ""),
            "action": fields.get("suggested action", ""),
        }

    return result

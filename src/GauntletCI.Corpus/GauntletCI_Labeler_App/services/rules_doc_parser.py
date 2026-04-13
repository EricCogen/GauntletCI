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
    pattern = re.compile(
        r"###\s+(GCI\d+)\s+[·•]\s+(.+?)\n"
        r"(?:.*?\*\*Confidence:\*\*.*?\n)?"
        r".*?\*\*What it detects:\*\*\s*(.+?)(?=\n\*\*|\Z)",
        re.DOTALL,
    )

    for m in pattern.finditer(text):
        rule_id = m.group(1).strip()
        name = m.group(2).strip()
        what = re.sub(r"\s+", " ", m.group(3)).strip()
        result[rule_id] = {"name": name, "what": what}

    return result

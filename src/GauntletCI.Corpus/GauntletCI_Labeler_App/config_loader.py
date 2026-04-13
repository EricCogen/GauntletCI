from __future__ import annotations

from pathlib import Path
from typing import Any
import yaml


def load_config(base_dir: Path) -> dict[str, Any]:
    config_path = base_dir / "config.yaml"
    if not config_path.exists():
        raise FileNotFoundError(
            f"Missing config.yaml at {config_path}. Copy config.example.yaml to config.yaml and set your paths."
        )
    with config_path.open("r", encoding="utf-8") as fh:
        data = yaml.safe_load(fh) or {}
    data.setdefault("secret_key", "gauntletci-labeler-dev")
    data.setdefault("reviewer_name", "reviewer")
    data.setdefault("queue", {})
    data["queue"].setdefault("default_limit", 200)
    data["queue"].setdefault("repeat_check_percent", 10)
    return data

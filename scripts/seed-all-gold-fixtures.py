#!/usr/bin/env python3
"""Seed all gold regression fixtures into ~/.gauntletci/corpus.db."""
from __future__ import annotations

import subprocess
import sys
from pathlib import Path

SCRIPTS = [
    "seed-redis-2995-gci0058-fixture.py",
    "seed-guard-deletion-remote-use-fixture.py",
]


def main() -> None:
    root = Path(__file__).resolve().parent
    for name in SCRIPTS:
        path = root / name
        if not path.exists():
            raise SystemExit(f"Missing seed script: {path}")
        print(f"Running {name}...")
        subprocess.check_call([sys.executable, str(path)])
    print("All gold fixtures seeded.")


if __name__ == "__main__":
    main()

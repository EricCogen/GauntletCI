#!/usr/bin/env python3
"""Update Formula/gauntletci.rb version and platform SHA256 lines from checksums.txt."""

from __future__ import annotations

import os
import re
import sys
from pathlib import Path


def parse_checksums(checksums_path: Path) -> dict[str, str]:
    sha_map: dict[str, str] = {}
    for line in checksums_path.read_text(encoding="utf-8").splitlines():
        parts = line.strip().split()
        if len(parts) != 2:
            continue
        sha, filename = parts
        for platform in ("osx-arm64", "osx-x64", "linux-arm64", "linux-x64"):
            if platform in filename:
                sha_map[platform] = sha
                break
    return sha_map


def update_formula(formula_path: Path, version: str, sha_map: dict[str, str]) -> None:
    content = formula_path.read_text(encoding="utf-8")
    content = re.sub(r'version "[^"]*"', f'version "{version}"', content)
    for platform, sha in sha_map.items():
        pattern = rf'(url "[^"]*{re.escape(platform)}[^"]*"\n\s*sha256 ")[^"]*(")'
        content = re.sub(pattern, rf"\g<1>{sha}\g<2>", content)
    formula_path.write_text(content, encoding="utf-8")
    print(formula_path.read_text(encoding="utf-8"))


def main() -> int:
    if len(sys.argv) != 4:
        print(
            "Usage: update-homebrew-tap-formula.py <checksums.txt> <Formula/gauntletci.rb> <version>",
            file=sys.stderr,
        )
        return 2

    checksums_path = Path(sys.argv[1])
    formula_path = Path(sys.argv[2])
    version = sys.argv[3]

    sha_map = parse_checksums(checksums_path)
    required = ("osx-arm64", "osx-x64", "linux-arm64", "linux-x64")
    missing = [platform for platform in required if platform not in sha_map]
    if missing:
        print(f"Missing SHA256 entries for: {', '.join(missing)}", file=sys.stderr)
        return 1

    update_formula(formula_path, version, sha_map)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

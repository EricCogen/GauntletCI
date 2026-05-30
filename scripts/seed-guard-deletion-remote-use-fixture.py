#!/usr/bin/env python3
"""Seed gold corpus fixture guard-deletion-remote-use into ~/.gauntletci/corpus.db.

After seeding, verify with:
  dotnet run --project src/GauntletCI.Cli -- corpus run \\
    --fixture guard-deletion-remote-use \\
    --db %USERPROFILE%\\.gauntletci\\corpus.db \\
    --fixtures %USERPROFILE%\\.gauntletci\\fixtures
"""
from __future__ import annotations

import json
import os
import sqlite3
import uuid
from datetime import datetime, timezone
from pathlib import Path

FIXTURE_ID = "guard-deletion-remote-use"
REPO = "GauntletCI/regression-fixtures"
PR_NUMBER = 0

DIFF_PATCH = """diff --git a/src/OrderService.cs b/src/OrderService.cs
index abc..def 100644
--- a/src/OrderService.cs
+++ b/src/OrderService.cs
@@ -10,12 +10,10 @@
 internal sealed class OrderService
 {
     internal bool TryCharge(Order? order)
     {
-        if (order == null)
-            return false;
         return order.Total > 0;
     }
 }
"""

EXPECTED = [
    {
        "RuleId": "GCI0059",
        "ShouldTrigger": True,
        "ExpectedConfidence": 0.95,
        "Reason": "Removed null guard on order but order.Total still used in TryCharge (guard-deletion-remote-use FN class).",
        "LabelSource": "Manual",
        "IsInconclusive": False,
    }
]

METADATA = {
    "FixtureId": FIXTURE_ID,
    "Tier": "Gold",
    "Repo": REPO,
    "PullRequestNumber": PR_NUMBER,
    "Language": "csharp",
    "RuleIds": ["GCI0059"],
    "Tags": ["guard-deletion", "null-guard", "regression", "fn-class"],
    "PrSizeBucket": "Tiny",
    "FilesChanged": 1,
    "HasTestsChanged": False,
    "HasReviewComments": False,
    "BaseSha": "",
    "HeadSha": "",
    "Source": "manual-regression-seed",
    "CreatedAtUtc": datetime.now(timezone.utc).isoformat(),
}


def main() -> None:
    home = Path(os.environ["USERPROFILE"]) / ".gauntletci"
    fixtures_root = home / "fixtures"
    fixture_dir = fixtures_root / "gold" / FIXTURE_ID
    fixture_dir.mkdir(parents=True, exist_ok=True)

    (fixture_dir / "metadata.json").write_text(json.dumps(METADATA, indent=2), encoding="utf-8")
    (fixture_dir / "diff.patch").write_text(DIFF_PATCH, encoding="utf-8")
    (fixture_dir / "expected.json").write_text(json.dumps(EXPECTED, indent=2), encoding="utf-8")

    db_path = home / "corpus.db"
    if not db_path.exists():
        raise SystemExit(f"Corpus database not found: {db_path}")

    rel_path = str(fixture_dir).replace("\\", "/")
    con = sqlite3.connect(db_path)
    con.execute(
        """
        INSERT INTO fixtures (
            id, fixture_id, tier, repo, pr_number, language, path,
            rule_ids_json, tags_json, pr_size_bucket, has_tests_changed,
            has_review_comments, source, created_at_utc
        ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
        ON CONFLICT(fixture_id) DO UPDATE SET
            tier=excluded.tier,
            repo=excluded.repo,
            pr_number=excluded.pr_number,
            language=excluded.language,
            path=excluded.path,
            rule_ids_json=excluded.rule_ids_json,
            tags_json=excluded.tags_json,
            pr_size_bucket=excluded.pr_size_bucket,
            has_tests_changed=excluded.has_tests_changed,
            has_review_comments=excluded.has_review_comments,
            source=excluded.source
        """,
        (
            str(uuid.uuid4()),
            FIXTURE_ID,
            "Gold",
            REPO,
            PR_NUMBER,
            "csharp",
            rel_path,
            json.dumps(["GCI0059"]),
            json.dumps(METADATA["Tags"]),
            "Tiny",
            0,
            0,
            METADATA["Source"],
            METADATA["CreatedAtUtc"],
        ),
    )

    con.execute("DELETE FROM expected_findings WHERE fixture_id = ?", (FIXTURE_ID,))
    for row in EXPECTED:
        con.execute(
            """
            INSERT INTO expected_findings (
                id, fixture_id, rule_id, should_trigger, expected_confidence,
                reason, label_source, is_inconclusive
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?)
            """,
            (
                str(uuid.uuid4()),
                FIXTURE_ID,
                row["RuleId"],
                1 if row["ShouldTrigger"] else 0,
                row["ExpectedConfidence"],
                row["Reason"],
                row["LabelSource"],
                1 if row["IsInconclusive"] else 0,
            ),
        )

    con.commit()
    con.close()
    print(f"Seeded {FIXTURE_ID} at {fixture_dir}")
    print(f"Updated {db_path}")


if __name__ == "__main__":
    main()

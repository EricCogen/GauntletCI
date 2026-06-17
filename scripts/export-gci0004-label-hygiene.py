import json
import re
import sqlite3
from pathlib import Path

db = Path.home() / ".gauntletci" / "corpus.db"
repo = Path(__file__).resolve().parents[1]
out = repo / "data" / "gci0004-label-hygiene-candidates.json"

conn = sqlite3.connect(db)
conn.row_factory = sqlite3.Row
obs = re.compile(r"\[Obsolete", re.I)

pairs = conn.execute(
    """
    WITH latest AS (
        SELECT fixture_id, id AS run_id FROM (
            SELECT fixture_id, id, ROW_NUMBER() OVER (PARTITION BY fixture_id ORDER BY completed_at_utc DESC, id DESC) rn
            FROM rule_runs WHERE UPPER(status)='COMPLETED'
        ) WHERE rn=1
    )
    SELECT ef.fixture_id, ef.label_source
    FROM expected_findings ef
    JOIN latest lr ON lr.fixture_id = ef.fixture_id
    LEFT JOIN actual_findings af ON af.fixture_id=ef.fixture_id AND af.rule_id=ef.rule_id AND af.run_id=lr.run_id
    WHERE ef.rule_id='GCI0004' AND ef.should_trigger=1 AND COALESCE(ef.is_inconclusive,0)=0
      AND COALESCE(af.did_trigger,0)=0
    GROUP BY ef.fixture_id
    """
).fetchall()


def load_diff(fixture_id: str) -> str:
    row = conn.execute(
        "SELECT diff_content, path FROM fixtures WHERE fixture_id=?", (fixture_id,)
    ).fetchone()
    if row and row["diff_content"]:
        return row["diff_content"]
    if row and row["path"]:
        p = Path(row["path"])
        if not p.is_absolute():
            p = (repo / p).resolve()
        if p.is_file():
            return p.read_text(encoding="utf-8", errors="replace")
        if p.is_dir():
            for name in ("patch.diff", "diff.patch"):
                f = p / name
                if f.is_file():
                    return f.read_text(encoding="utf-8", errors="replace")
    return ""


scope_mismatch = []
obsolete_fn = []
for p in pairs:
    text = load_diff(p["fixture_id"])
    if obs.search(text):
        obsolete_fn.append(p["fixture_id"])
    else:
        scope_mismatch.append(
            {
                "fixture_id": p["fixture_id"],
                "rule_id": "GCI0004",
                "is_inconclusive": True,
                "reason": "human label expects breaking-change signal; GCI0004 scope is [Obsolete] added/removed only (no Obsolete in patch)",
            }
        )

def classify_obsolete_fn(text: str, fixture_id: str) -> dict:
    added = [
        ln
        for ln in text.splitlines()
        if ln.startswith("+") and not ln.startswith("+++") and obs.search(ln)
    ]
    removed = [
        ln
        for ln in text.splitlines()
        if ln.startswith("-") and not ln.startswith("---") and obs.search(ln)
    ]
    if added or removed:
        if ".Tests" in text or "/Tests/" in text or "\\Tests\\" in text:
            reason = "[Obsolete] added/removed in test file; GCI0004 skips test files by design"
        else:
            reason = "[Obsolete] added/removed in patch but rule did not fire; needs manual review"
    else:
        reason = "[Obsolete] appears only on unchanged context lines; no added/removed Obsolete attribute in patch"
    return {
        "fixture_id": fixture_id,
        "rule_id": "GCI0004",
        "is_inconclusive": True,
        "reason": reason,
    }

obsolete_fn_overrides = []
for fid in obsolete_fn:
    text = load_diff(fid)
    obsolete_fn_overrides.append(classify_obsolete_fn(text, fid))

doc = {
    "scope_mismatch_count": len(scope_mismatch),
    "obsolete_fn_count": len(obsolete_fn),
    "obsolete_fn_fixture_ids": obsolete_fn,
    "overrides": scope_mismatch + obsolete_fn_overrides,
}
out.write_text(json.dumps(doc, indent=2), encoding="utf-8")
print(f"Wrote {out}")
print(f"scope_mismatch={len(scope_mismatch)} obsolete_fn={len(obsolete_fn)}")
conn.close()

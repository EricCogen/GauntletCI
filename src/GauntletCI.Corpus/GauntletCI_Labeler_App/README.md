# GauntletCI Labeler

A small local Flask app for labeling GauntletCI corpus fixtures at the grouped `fixture + rule` level.

This app was built around the workflow we defined together:
- local-first, no hosted dependency
- Python rather than ASP.NET Core for faster iteration
- DB-first but fixture-aware
- grouped review units at `fixture_id + rule_id`
- truth labeling kept separate from usefulness scoring
- queue seeding that supports both fired and non-fired cases
- fixture artifacts loaded from your existing fixture folders
- your database and fixtures stay external to the app package

## What this app does

- Reads your existing GauntletCI SQLite corpus database
- Auto-creates local app tables for `label_queue` and `rule_rubrics`
- Groups noisy raw findings into a single review unit per `fixture_id + rule_id`
- Shows fixture artifacts if your fixture folder exists:
  - `diff.patch`
  - `metadata.json`
  - `notes.md`
  - `raw/pr.json`
  - `raw/files.json`
  - `raw/review-comments.json`
- Saves truth labels into `expected_findings`
- Saves reviewer usefulness scores into `evaluations`
- Computes a lightweight evaluation view with TP / FP / FN and keep / rewrite / kill guidance
- Works even if fixture artifacts are missing, though the task page is much better when they are present

## What this app does not do

- It does not modify your original fixture files
- It does not require you to move or rename your database
- It does not require the DB and fixtures to be bundled with the app
- It does not try to label individual raw finding rows one at a time

## Labeling model baked into the app

The app follows the labeling workflow we designed:

### Truth pass
For each grouped `fixture + rule` task, choose one:
- **Yes** = the rule should trigger
- **No** = the rule should not trigger
- **Inconclusive** = the diff does not provide enough evidence

The app writes this into `expected_findings`.

### Value pass
Separately, score usefulness from 0 to 5:
- `0` = useless noise
- `1` = weak
- `2` = somewhat useful
- `3` = useful
- `4` = strong
- `5` = must-have

The app writes this into `evaluations`.

### Why the split matters
Truth and usefulness are intentionally separate. A finding can be:
- correct but not useful
- useful but only partly justified
- wrong and useless

That separation is one of the core design decisions from the workflow.

## Queue design in this app

The queue is seeded from two kinds of work:
- **fired** cases, where the rule actually triggered
- **non-fired probe** cases, where the rule did not trigger and you want to test recall / false negatives

This follows the earlier guidance that you should not only label fired findings. Otherwise precision becomes visible while recall stays fake.

The queue is stored in `label_queue`, which the app creates automatically.

## Rule rubrics in this app

The app also creates `rule_rubrics`, so each rule can have:
- intent
- trigger conditions
- non-trigger conditions
- inconclusive conditions
- examples

This supports the rubric-first workflow we discussed to prevent label drift.

## Requirements

- Python 3.10+
- Your existing `gauntletci-corpus.db`
- Your existing `fixtures` folder

## Package contents

This zip intentionally contains only the app and setup files.
It does **not** include your DB or fixtures because you already have them.

## Setup

### 1. Create a virtual environment

Windows PowerShell:

```powershell
python -m venv .venv
.\.venv\Scripts\Activate.ps1
```

macOS / Linux:

```bash
python3 -m venv .venv
source .venv/bin/activate
```

### 2. Install dependencies

```bash
pip install -r requirements.txt
```

### 3. Create config.yaml

Copy `config.example.yaml` to `config.yaml` and set your paths.

Example:

```yaml
database_path: C:/path/to/gauntletci-corpus.db
fixtures_root: C:/path/to/fixtures
secret_key: change-me
reviewer_name: Eric
queue:
  default_limit: 200
  repeat_check_percent: 10
```

Notes:
- `fixtures_root` should point at the `fixtures` directory itself, not its parent.
- The app will search under `fixtures/<tier>/<fixture_id>` and `fixtures/<fixture_id>` automatically.
- Keep your DB and fixtures wherever they already live. The app reads them by path.

### 4. Seed the queue

Seed a starter queue with both fired and non-fired work:

```bash
python seed_queue.py --limit 150 --include-nonfired 30
```

Target a few rules intentionally, which matches the workflow we discussed:

```bash
python seed_queue.py --rules GCI0010,GCI0008,GCI0018 --limit 90 --include-nonfired 15
```

What this does:
- adds high-signal fired tasks first
- adds non-fired probe tasks for recall testing
- keeps the labeling unit at grouped `fixture + rule`

### 5. Run the app

```bash
python app.py
```

Open:

```text
http://127.0.0.1:5000
```

## Recommended first workflow

This reflects the process we laid out earlier.

1. Seed a small queue for 3 to 5 rules
2. Add rule rubrics from the Rules page
3. Label 20 to 30 tasks per rule
4. Mix fired and non-fired cases rather than only labeling hits
5. Treat inconclusive as a real outcome, not a failure
6. Watch the Evaluations page for:
   - Precision trend
   - Recall trend
   - Inconclusive rate
   - Average usefulness
7. Keep, rewrite, or demote rules based on evidence

## App screens

### Dashboard
Shows:
- fixtures
- rules
- findings
- queue counts
- basic evaluation snapshot

### Queue
Shows grouped review units with filters for:
- status
- rule
- queue bucket

### Task page
Shows:
- grouped findings for one `fixture + rule`
- rule rubric summary
- PR metadata from fixture artifacts
- changed files from `raw/files.json`
- diff preview from `diff.patch`
- review comments from `raw/review-comments.json`
- truth label controls and usefulness scoring

### Rules
Shows rule overview and lets you edit rubrics.

### Evaluations
Shows:
- labeled cases
- TP / FP / FN
- precision
- recall
- inconclusive count
- usefulness average
- rough keep / rewrite / kill_or_demote verdict

## Keyboard shortcuts on the task page

- `Y` = Yes
- `N` = No
- `I` = Inconclusive
- `S` = Save and move on

## Tables created automatically

The app creates these in your existing SQLite DB if needed:
- `label_queue`
- `rule_rubrics`

It reuses your existing corpus tables:
- `fixtures`
- `actual_findings`
- `expected_findings`
- `evaluations`
- `aggregates`

## Metric notes

The Evaluations page computes metrics only from labeled rows in `expected_findings`.

- Precision = TP / (TP + FP)
- Recall = TP / (TP + FN)
- Inconclusive rows are tracked separately
- Usefulness is averaged from `evaluations`

This follows the earlier plan to move from unlabeled discovery metrics to evidence-backed evaluation.

## Important design choices carried forward from our earlier discussion

- The review unit is **fixture + rule**, not raw finding row
- The app is **DB-first but fixture-aware**
- Truth is kept separate from usefulness
- Non-fired tasks are included so recall can be measured
- Inconclusive is treated as first-class
- Rubrics are stored in the app so label drift can be reduced
- The package excludes your DB and fixtures on purpose

## Suggested next improvements after you start using it

- silent repeat-case insertion for self-consistency checks
- duplicate finding collapse metrics for noisy rules like GCI0010
- side-by-side diff chunk navigation
- richer rule-level sampling controls
- exportable evaluation reports

# GauntletCI Copilot Efficiency Playbook

## Purpose
Minimize Copilot request usage while maximizing output per prompt.

---

## Core Strategy

Treat Copilot as a batch processor, not a conversation.

Each prompt should:
- Perform multiple steps
- Produce final output
- Avoid follow-up prompts

---

## Model Usage Strategy

### Model Selection by Task Type

Switch models via `/model` in the main conversation, or set `model:` in sub-agent `task` calls.

| Task Type | Model | Why |
|-----------|-------|-----|
| Codebase exploration, file search, quick lookup | `claude-haiku-4.5` | Fastest, cheapest — explore agent default |
| Build / test / lint execution | `claude-haiku-4.5` | Only needs to run a command and report pass/fail |
| Rule implementation (single file) | `claude-sonnet-4.6` | Default — handles C# pattern matching well |
| Refactoring, CLI wiring, test generation | `claude-sonnet-4.6` | Default — multi-step code tasks |
| PR comment resolution (review fixes) | `claude-sonnet-4.6` | Default — targeted surgical changes |
| Corpus pipeline (multi-file, 5+ files) | `claude-sonnet-4.6` | General-purpose agent default |
| Architecture decisions, design review | `claude-opus-4.5` | Premium — cross-cutting reasoning |
| Complex debugging (root cause unknown) | `claude-opus-4.5` | Premium — deep trace reasoning |
| Scoring / evaluation algorithm design | `claude-opus-4.5` | Premium — mathematical precision matters |

### Sub-Agent Model Override Examples

```
# Fast exploration (haiku — cheap)
task(agent_type="explore", model="claude-haiku-4.5", ...)

# Standard implementation (sonnet — default, no override needed)
task(agent_type="general-purpose", ...)

# Hard architecture problem (opus — premium)
task(agent_type="general-purpose", model="claude-opus-4.5", ...)
```

### Rules of Thumb

- One file, spec is clear → no agent, no model switch (stay on Sonnet)
- Multi-file, >3 files → background agent on Sonnet
- Search-only, no code changes → explore agent on Haiku
- "Why is this broken?" + no obvious answer → switch to Opus
- Opus for the whole session → never; switch back to Sonnet after the hard question

---

## High-Efficiency Prompt Templates

### Rule Implementation

Implement a GauntletCI rule:

Rule ID: GCI00XX
Purpose: [what risk it detects]

Trigger:
- [exact condition]

Must NOT trigger:
- [false positive case]

Input:
- DiffContext (added/removed lines)

Output:
- Use CreateFinding()
- Include summary, evidence, whyItMatters, suggestedAction

Return:
- Complete C# class inheriting RuleBase
- No explanation

---

### Multi-Step Refactor

Refactor this file:

Goals:
1. Improve readability
2. Remove duplication
3. Preserve behavior exactly

Also:
- Fix any obvious bugs
- Keep method signatures unchanged
- Ensure it compiles

Return:
- Full updated file
- No explanation

---

### Test Generation

Generate xUnit tests for this class:

Requirements:
- Cover happy path
- Cover edge cases
- Cover failure scenarios

Use:
- realistic test names
- Arrange/Act/Assert

Return:
- Complete test file
- No explanation

---

### Bug Hunt (Premium)

Analyze this code and identify:

1. The most likely bug
2. Why it occurs
3. Minimal fix

Then:
- Show corrected code snippet only
- No extra commentary

---

### Corpus Pipeline Task

Implement the following in C#:

1. PullRequestCandidate model
2. HydratedPullRequest model
3. SQLite schema for both

Constraints:
- Use clean POCOs
- Include all required fields
- No business logic

Return:
- All code in one response

---

### Repo Understanding

Read these files:
- [file list]

Return:
1. What the system does
2. Top 3 design flaws
3. One concrete improvement

Be concise

---

## Pre-Flight Checklist (Run Before Every Todo)

Before starting any todo, answer these 5 questions:

| Question | → Agent | → Direct |
|---|---|---|
| Does it touch >3 files? | Yes | No |
| Is the spec clear (interfaces exist, behavior obvious)? | Yes | No → discuss first |
| Can build + tests verify it automatically? | Yes | No |
| Is it independent of in-progress work? | Yes | No → wait |
| Are there sibling tasks to parallelize? | Yes → multi-agent | No → single agent or direct |

Decision outputs:
- All Yes → background agent(s), possibly parallel
- Spec unclear → discuss first, then agent
- Small + clear → do it directly
- Blocked by dep → wait, work on something else

Apply this checklist when new todos are added too.

---

## Todo State Management

### Durable DB (source of truth)
`C:\Users\ericc\.gauntletci\copilot\todos.db` — never use `todos-seed.sql` (retired).

After every status change, update BOTH the session SQL and the durable DB:
```powershell
python sync-todos.py --update <id> <status>
```

### Restore procedure (new session)
Export from the durable DB and INSERT via the `sql` tool:
```powershell
cd "C:\Users\ericc\.gauntletci\copilot"
python -c "import sqlite3,json; con=sqlite3.connect('todos.db'); con.row_factory=sqlite3.Row; todos=[dict(r) for r in con.execute('SELECT id,title,description,status FROM todos ORDER BY id')]; deps=[dict(r) for r in con.execute('SELECT todo_id,depends_on FROM todo_deps')]; con.close(); open('todos-export.json','w').write(json.dumps({'todos':todos,'deps':deps})); print(len(todos),'todos')"
```
Then verify: `SELECT status, COUNT(*) FROM todos GROUP BY status;`

---

## Anti-Patterns to Avoid

### Avoid conversational prompts
- “what do you think?”
- “any suggestions?”
- “can you improve this?”

### Avoid tiny step prompts
- writing small pieces iteratively

### Avoid unnecessary agent usage
- Do not use agent for small tasks

### Avoid blind exploration
- Always scope file list

---

## Optimized Workflow

### Rule Development
1. Use rule template
2. Paste result
3. One fix pass max

### Corpus Development
1. Generate models + schema in one prompt
2. Generate CLI commands in one prompt
3. Generate hydration logic in one prompt

### Debugging
1. Use premium
2. Ask for root cause + minimal fix

### Repo Review
Ask for:
- Top 3 problems only
- No style issues

---

## Expected Outcome

- 50–70% fewer prompts
- 70–90% fewer premium requests
- Reduced iteration cycles

---

## Mental Model

Bad:
Think with Copilot

Good:
Batch work through Copilot

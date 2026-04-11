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

### Included Models (Default)
Use for:
- Rule implementation
- Refactoring
- Test generation
- CLI wiring
- File-level analysis

### Premium Models (Use Sparingly)
Use for:
- Architecture decisions
- Cross-file reasoning
- Complex debugging
- Corpus/scoring design

Rule:
If the task fits in one file, do NOT use premium.

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

### Seed Sync (Option B — always)
After every `UPDATE todos SET status = '...' WHERE id = '...'`:
1. Immediately make a surgical `edit` to the matching row in `todos-seed.sql`
2. Update the `-- Status:` header counts in the seed when they change

Never let the seed drift from the DB.

### Seed file location
`C:\Users\ericc\.gauntletci\copilot\todos-seed.sql`

### Restore procedure (new session)
1. Run the `ALTER TABLE` lines (errors harmless if columns exist)
2. Run the full `INSERT OR IGNORE` block
3. Run the deps `INSERT OR IGNORE` block
4. Verify: `SELECT status, COUNT(*) FROM todos GROUP BY status;`

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

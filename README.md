[![GitHub last commit](https://img.shields.io/github/last-commit/EricCogen/GauntletCI)](https://github.com/EricCogen/GauntletCI/commits/main)
[![GitHub stars](https://img.shields.io/github/stars/EricCogen/GauntletCI?style=social)](https://github.com/EricCogen/GauntletCI/stargazers)
[![License](https://img.shields.io/badge/License-Elastic_2.0-blue.svg)](LICENSE)

<div><img src="GauntletCI.png" alt="GauntletCI Logo" width="200" align="right"/></div>

# GauntletCI - Pre-commit change-risk detection for pull request diffs

**GauntletCI** is a .NET CLI tool that analyzes pull request diffs or as a pre-commit audit that detects **behavioral change-risk before code is merged**.

It answers one question:

> Did this change introduce behavior that is not properly validated?

---

## 📖 Why This Exists

Even experienced developers miss things in diffs.

Not because they lack skill — but because diffs are deceptive.

A small change can silently alter behavior:

- A new null check changes execution flow
- A guard clause introduces new exceptions
- A method signature changes without test updates
- A dependency call is modified without validation
- A conditional branch shifts logic in subtle ways

These are not syntax errors.  
They are **behavior changes** — and they regularly slip through code review.

GauntletCI exists to catch them **before they reach production**.

Read the full [STORY.md](story) behind GauntletCI.

---

## 🧠 Philosophy & Principles

GauntletCI is built on a clear set of principles defined in the **[GauntletCI Charter](CHARTER.md)**:

- **Coverage is Not Correctness** — Tests prove execution, not survival.
- **Falsification Over Verification** — We seek to disprove safety, not confirm compliance.
- **Intent is Material Context** — We cross-reference PR diffs with linked issues to detect semantic drift.
- **Privacy is Absolute** — All reasoning happens locally; no code ever leaves your machine.
- **Determinism Anchors Intelligence** — The local AI *explains*; deterministic Roslyn rules *enforce*.

---

## 🔍 What GauntletCI is (and is not)

### ✅ What it is
- Diff-aware change-risk detector
- Pre-commit / pre-merge safety layer
- Focused on behavior, not style
- **Intent-aware** — Cross-references implementation with linked issues (GitHub/Jira)

### ❌ What it is not
- Not a linter
- Not a test runner
- Not a static analysis replacement
- Not a code formatter

GauntletCI complements your existing tools — it does not replace them.

---

## 🚀 What it does

- Analyzes only **what changed** in a diff
- Detects **unvalidated behavior changes**
- Flags **missing or weak test coverage**
- Identifies **execution flow changes** (guards, exceptions, branching)
- Surfaces **API and contract changes**
- **Intent Alignment:** Compares PR diff with linked GitHub Issue to detect when the implementation drifts from the stated goal.
- Outputs actionable findings with file paths and line numbers

---

## 📦 Installation

```bash
dotnet tool install -g GauntletCI

# GauntletCI

[![License: Elastic-2.0](https://img.shields.io/badge/License-Elastic_2.0-blue.svg)](LICENSE)

**GauntletCI is a pre-commit change-risk gate.**

It evaluates your diff before it lands and answers one question:

> **Did this change introduce behavior that is not properly validated?**

---

## Why This Exists

Code review catches intent.  
Tests catch correctness.  

What’s missing is a system that verifies:

> **When behavior changes, is that change actually validated?**

---

## How It Works

### 1. Deterministic gates (blocking, fast)

- Branch must be up to date  
- Tests must pass  

---

### 2. Single-pass diff analysis

- Evaluates only what changed  
- Detects behavior shifts and validation gaps  
- One pass, no loops, no hidden steps  

---

### 3. Structured findings

Each finding explains:

- What changed  
- Why it matters  
- What to do next  

---

## Real Examples

These examples are derived from real change scenarios in the benchmark corpus.

### Example: Behavior Change

Before:
if (user == null) return;

After:
if (user == null) throw new ArgumentNullException(nameof(user));

Finding:
Behavior change detected. Method now throws instead of returning. No test validates this new path.

Derived from real benchmark fixture  
→ See full analysis: ./benchmarks/README.md#example-1-behavior-change

---

### Example: Async Regression

Before:
await service.ProcessAsync(data);

After:
service.ProcessAsync(data).Wait();

Finding:
Async behavior changed to blocking execution. Potential deadlock risk. No validation of sync path.

Derived from real benchmark fixture  
→ See full analysis: ./benchmarks/README.md#example-2-async-blocking-regression

---

## What It Catches

- Behavior changes (logic, control flow, exceptions)
- Validation gaps (missing or weak tests)
- Silent risk (null handling, async, edge cases)

---

## Why Not Existing Tools

Traditional tools focus on code quality and patterns.

They do not answer:

> “Did this specific change introduce behavior that is not validated?”

GauntletCI focuses on the diff and the risk introduced by that change.

---

## Core Capabilities

- Pre-commit enforcement via Git hooks  
- Deterministic pre-flight checks  
- Single-pass evaluation  
- Structured JSON output  
- CLI and PR integration  

---

## Usage

gauntletci
gauntletci --full
gauntletci --fast
gauntletci install

---

## Positioning

GauntletCI is the last line of defense before your code becomes someone else’s problem.

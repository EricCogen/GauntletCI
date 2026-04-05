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

## Real Examples

### Example: Behavior Change

Before:
if (user == null) return;

After:
if (user == null) throw new ArgumentNullException(nameof(user));

Finding:
Behavior change detected. Method now throws instead of returning. No test validates this new path.

Derived from real benchmark fixture  
→ See full analysis: benchmarks/README.md#example-1-behavior-change

---

### Example: Async Regression

Before:
await service.ProcessAsync(data);

After:
service.ProcessAsync(data).Wait();

Finding:
Async behavior changed to blocking execution. Potential deadlock risk.

Derived from real benchmark fixture  
→ See full analysis: benchmarks/README.md#example-2-async-blocking-regression

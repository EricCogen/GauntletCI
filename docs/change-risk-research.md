# Change Risk Research

[GauntletCI](../README.md) | [View Thesis](./change-risk-thesis.md)

## Hypothesis

Changes to code frequently alter behavior without corresponding updates to validation (tests or equivalent proof), creating unverified risk.

---

## Method

Public pull requests from established open-source repositories were analyzed.

Focus:

- Identify behavioral changes in code
- Compare those changes against validation updates (tests, assertions, verification steps)
- Observe whether behavioral changes were explicitly validated

---

## Observation

### Repository
Files

### Pull Request
https://github.com/files-community/Files/pull/17564

### Observed Change

A code path involving unmanaged object creation was introduced:

- A call to CoCreateInstance was made
- The result of the call (HRESULT) was not validated
- The returned pointer was used directly

### Validation Present

- Manual validation steps were documented in the PR
- These steps focused on expected application behavior (happy path scenarios)

### Validation Not Present

- No explicit validation of failure paths for CoCreateInstance
- No automated tests covering failure scenarios
- No assertion or guard ensuring the object was valid before use

---

## Findings

Across the analyzed PR:

- Behavioral changes were introduced
- Validation focused on expected behavior rather than failure modes
- Edge-case risks were identified during review, not through validation artifacts

---

## Conclusion

Observed evidence supports the hypothesis:

Behavioral changes are not consistently accompanied by corresponding validation updates, particularly for edge cases and failure paths.

---

Generated: 04/05/2026

[GauntletCI](../README.md) | [View Thesis](./change-risk-thesis.md)

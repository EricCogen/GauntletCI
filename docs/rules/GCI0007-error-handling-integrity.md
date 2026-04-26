# GCI0007: Error Handling Integrity

## What it detects

Detects two patterns:

1. Newly added catch blocks that contain no meaningful content: no rethrow, no logging, no error recording.
2. Error-level log calls removed from exception-handling blocks without replacement.

Also incorporates Roslyn static analysis findings for CA1031 (do not catch general exception types).

## Why it matters

Empty catch blocks make failures invisible. When an exception is swallowed, the application continues running in a potentially invalid state with no evidence of what went wrong. Removing error logs from catch blocks eliminates critical context that would otherwise be available during incident triage.

## Example diff

```diff
 try
 {
     await _paymentClient.ChargeAsync(order);
 }
+catch
+{
+}
```

## Example finding

```text
[GCI0007] Error Handling Integrity
Silent catch block introduced around payment call.
```

## What to validate

- Should the exception be logged?
- Should the exception be rethrown or wrapped?
- Should callers receive a failure result?
- Is there a test for the failure path?

## False positive notes

This may be acceptable if the exception is intentionally ignored and the code includes explicit documentation or compensating behavior (for example, a fire-and-forget background task where failure is expected and logged elsewhere).

## How to suppress

Add `.gauntletci.json` to your repository and list this rule ID in the suppression configuration. See the [Configuration Reference](../DEVELOPMENT.md) for syntax.

## Status

Stable

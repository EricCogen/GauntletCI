# GCI0003: Behavioral Change Detection

## What it detects

Detects two patterns:

1. Three or more control-flow lines (conditionals, returns, boolean operators) removed from non-test files without corresponding test file changes in the same diff.
2. Method signatures that changed between the removed and added lines of the same file.

## Why it matters

Deleting conditional logic without updating tests can silently break behavior that was previously protected by test coverage. Signature changes can break callers that were not included in the diff.

## Example diff

```diff
-if (user.IsActive && user.HasSubscription)
-{
-    return AccessResult.Granted;
-}
+if (user.IsActive)
+{
+    return AccessResult.Granted;
+}
```

## Example finding

```text
[GCI0003] Behavioral Change Detection
3 control-flow lines removed without test file changes.
```

## What to validate

- Are the removed logic paths intentionally gone?
- Do tests still cover all branches after the change?
- Have all callers of changed signatures been updated?
- Is a backward-compatible overload needed?

## False positive notes

This rule may fire on legitimate refactors that do not change observable behavior. If the removed lines were duplication or dead code, the finding can be acknowledged as intentional.

## How to suppress

Add `.gauntletci.json` to your repository and list this rule ID in the suppression configuration. See the [Configuration Reference](../DEVELOPMENT.md) for syntax.

## Status

Stable

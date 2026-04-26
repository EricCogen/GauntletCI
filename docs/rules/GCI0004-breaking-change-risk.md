# GCI0004: Breaking Change Risk

## What it detects

Detects two patterns:

1. Public members (methods, classes, interfaces, structs, records, enums, properties) removed or changed in the diff without a corresponding addition of the same member name.
2. Deprecation markers removed from members that have not been fully deleted.

## Why it matters

Removing or changing a public member is a breaking change for any code that depends on it, including external library consumers. Removing a deprecation marker without completing the removal leaves consumers with no migration warning.

## Example diff

```diff
-public UserRecord GetUser(int id)
+public UserRecord GetUser(int id, bool includeDeleted = false)
```

## Example finding

```text
[GCI0004] Breaking Change Risk
Public API changed: GetUser(int id) -> GetUser(int id, bool includeDeleted)
```

## What to validate

- Are all callers of the changed member updated in this diff?
- Is this change coordinated with a major version increment?
- Should the old signature be kept as an overload for backward compatibility?
- If a deprecation marker was removed, have all consumers migrated?

## False positive notes

Adding optional parameters with defaults does not break existing callers at the call site, but it may break callers using named arguments or reflection. The rule flags the shape change as a signal to verify caller compatibility.

## How to suppress

Add `.gauntletci.json` to your repository and list this rule ID in the suppression configuration. See the [Configuration Reference](../DEVELOPMENT.md) for syntax.

## Status

Stable

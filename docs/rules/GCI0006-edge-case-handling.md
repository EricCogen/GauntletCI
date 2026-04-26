# GCI0006: Edge Case Handling

## What it detects

Detects two patterns:

1. New code that accesses `.Value` on a nullable type without a null check within the preceding five lines.
2. New methods with string or object parameters that do not include argument validation near the top of the method body.

Also incorporates Roslyn static analysis findings for CA1062 (validate arguments of public methods).

## Why it matters

Accessing a nullable value without a guard throws `InvalidOperationException` at runtime. Unvalidated parameters can cause null reference errors deep in the call stack, far from where the issue was introduced.

## Example diff

```diff
+var name = customer.Address.Value.StreetLine1;
```

## Example finding

```text
[GCI0006] Edge Case Handling
.Value accessed without preceding null check on customer.Address.
```

## What to validate

- Is a null check or null-coalescing expression needed before accessing `.Value`?
- Does the method validate its string or object parameters before using them?
- Would a null or empty input cause incorrect behavior downstream?

## False positive notes

This may fire on code where a null check exists earlier in the method but outside the five-line window. It may also fire on patterns that use the null-forgiving operator (`!`) when the developer is certain the value is non-null.

## How to suppress

Add `.gauntletci.json` to your repository and list this rule ID in the suppression configuration. See the [Configuration Reference](../DEVELOPMENT.md) for syntax.

## Status

Stable

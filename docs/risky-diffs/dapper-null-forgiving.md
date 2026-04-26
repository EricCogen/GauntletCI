# Example: Null-forgiving operator on nullable return

## Project

DapperLib/Dapper

## What changed

A null-forgiving operator (`!`) was added to a method return value that could legitimately return null.

## Why it matters

The null-forgiving operator tells the compiler to suppress a nullable warning. It does not add a runtime check. If the value is actually null at runtime, the code throws a NullReferenceException at the point of first use rather than at the assertion site, making the failure harder to trace.

## Example shape

```diff
-return _cache.Get(key);
+return _cache.Get(key)!;
```

## GauntletCI signal

Edge case handling risk flagged by GCI0006. The `.Value` or null-forgiving pattern indicates a nullable access without a guard.

## Validation to consider

- Can the underlying method actually return null?
- If it can, is the caller prepared to handle null?
- Would a null-coalescing fallback (`?? defaultValue`) or an explicit null check be safer?
- Is this suppression documented with a comment explaining the guarantee?

## Notes

Keep this page factual. Link to public PRs only when the example is public and safe to reference.

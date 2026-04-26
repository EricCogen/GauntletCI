# Example: Integer overflow on size calculation

## Project

adamhathcock/sharpcompress

## What changed

A size or length calculation was changed in a way that could overflow an integer type.

## Why it matters

Integer overflow in size calculations can produce negative or wrapping values. In archive or compression code, an overflowed size can cause incorrect buffer allocation, silent data truncation, or a security vulnerability if the size is used to control memory operations.

## Example shape

```diff
-long totalSize = checked((long)entry.Size * compressionFactor);
+int totalSize = (int)entry.Size * compressionFactor;
```

## GauntletCI signal

Data integrity risk flagged by GCI0022 (Numeric Precision and Overflow) based on the removal of `checked` arithmetic and the narrowing cast to `int`.

## Validation to consider

- Can the multiplication result exceed the range of `int` (2,147,483,647)?
- Should `checked` arithmetic be used to catch overflow explicitly?
- Is a `long` or `ulong` type more appropriate for this calculation?
- Are there tests covering inputs at or near the boundary?

## Notes

Keep this page factual. Link to public PRs only when the example is public and safe to reference.

# Example: LINQ in loop performance risk

## Project

dotnet/efcore

## What changed

A LINQ query was introduced inside a loop body.

## Why it matters

Executing a LINQ query or database lookup inside a loop creates O(n) or O(n^2) behavior depending on the data source. In a hot path or with large datasets, this can cause severe performance degradation or query storms against a database.

## Example shape

```diff
 foreach (var item in items)
 {
+    var match = allItems.Where(x => x.Id == item.Id).FirstOrDefault();
 }
```

## GauntletCI signal

Performance hotpath risk flagged by GCI0008 (Complexity Control) or GCI0006 (Edge Case Handling) depending on context.

## Validation to consider

- Benchmark the affected path with representative data volumes.
- Add tests or measurements for large input sizes.
- Replace repeated lookup with a dictionary or precomputed map if appropriate.
- Check whether the LINQ source is in-memory or executes against a database.

## Notes

Keep this page factual. Link to public PRs only when the example is public and safe to reference.

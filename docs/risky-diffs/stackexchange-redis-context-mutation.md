# Example: Shared context mutation across requests

## Project

StackExchange/StackExchange.Redis

## What changed

A field on a shared object was mutated inside a path that could be reached concurrently by multiple requests.

## Why it matters

Mutating shared state without synchronization can produce race conditions: two requests may read stale data, interleave writes, or corrupt the state of the shared object. This class of bug is often intermittent and hard to reproduce under normal test conditions.

## Example shape

```diff
 public void Process(RequestContext ctx)
 {
+    _sharedState.CurrentRequestId = ctx.Id;
     DoWork(ctx);
 }
```

## GauntletCI signal

Concurrency safety risk flagged by GCI0016 (Concurrency Safety) or GCI0003 (Behavioral Change Detection) depending on the mutation pattern.

## Validation to consider

- Is `_sharedState` accessed from multiple threads or async continuations?
- Should this field be stored per-request rather than shared?
- If shared state is required, is access synchronized with a lock, semaphore, or thread-safe collection?
- Do load or concurrency tests cover this code path?

## Notes

Keep this page factual. Link to public PRs only when the example is public and safe to reference.

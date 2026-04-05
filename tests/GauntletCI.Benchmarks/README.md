# Benchmark Corpus

## Example 1: Behavior Change

### Change
if (user == null) return;
→
if (user == null) throw new ArgumentNullException(nameof(user));

### Finding
Behavior change detected. No test validates exception path.

---

## Example 2: Async Blocking Regression

### Change
await service.ProcessAsync(data);
→
service.ProcessAsync(data).Wait();

### Finding
Blocking call introduced. Potential deadlock risk.

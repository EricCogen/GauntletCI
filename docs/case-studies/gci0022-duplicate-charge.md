# GCI0022 Case Study: Idempotency Failures in Distributed Systems

## The Problem

A payment processing service used MongoDB with automatic retry logic for network failures. The service's logic assumed that re-executing the same database operation twice would be safe if the first attempt failed. However, the retry handler didn't include proper guards - it would blindly retry even on operations that had already succeeded silently.

## The Vulnerability

```csharp
public async Task ProcessPayment(Order order)
{
    try
    {
        // Insert charge record without idempotency check
        var result = await _db.InsertAsync(new ChargeRecord 
        {
            OrderId = order.Id,
            Amount = order.Total,
            Status = "pending"
        });
    }
    catch (TimeoutException ex)
    {
        // Automatic retry - but did the first insert succeed?
        await _db.InsertAsync(new ChargeRecord 
        {
            OrderId = order.Id,
            Amount = order.Total,
            Status = "pending"
        });
    }
}
```

When a network timeout occurred after the database acknowledged the insert but before the acknowledgment reached the client, the retry would insert a duplicate charge record. This resulted in customers being double-charged.

## The Real-World Impact

- **Feb 2024**: Platform detected 147 duplicate charges over 3 days
- **Root cause**: Transient network partition that caused silent retries
- **Customer impact**: $23K in duplicate charges, requiring manual refunds
- **Operational cost**: 8 hours incident response, customer support tickets

## How GCI0022 Catches This

GCI0022 flags raw INSERT operations that lack idempotency guards:

```
Rule: GCI0022 - Idempotency & Retry Safety
  Detects: Direct INSERT/UPDATE without upsert, idempotency key, or version guards
  Guards: Skips intentional raw INSERTs in migration/seed files
  
Finding: Raw INSERT without upsert guard
Location: PaymentService.cs:45
Risk: Duplicate records on retry
```

The guard clause prevents false positives for intentional raw operations in database migrations or seed data contexts (where duplicates don't matter).

## The Fix

```csharp
public async Task ProcessPayment(Order order)
{
    // Use upsert with idempotency key
    var result = await _db.UpdateAsync(
        filter: Builders<ChargeRecord>.Filter.Eq(c => c.IdempotencyKey, order.TransactionId),
        update: Builders<ChargeRecord>.Update
            .SetOnInsert(c => c.OrderId, order.Id)
            .SetOnInsert(c => c.Amount, order.Total)
            .SetOnInsert(c => c.Status, "pending")
            .Set(c => c.LastRetry, DateTime.UtcNow),
        options: new UpdateOptions { IsUpsert = true }
    );
}
```

Key improvements:
- **Idempotency key**: Unique constraint on `TransactionId` prevents duplicates
- **Upsert semantics**: Safe to retry - either inserts once or updates
- **Retry tracking**: Logs retry attempts for debugging

## Detection & Remediation

- **Detection**: GCI0022 flags any retry handler with raw INSERT inside catch blocks
- **Manual review**: Determines if INSERT is intentional (migrations) or a safety issue
- **Automated**: Guards skip migration files automatically; only flags application code

---

**Lesson**: In distributed systems, assume every operation can timeout after partial success. Design all state changes as idempotent upserts, not raw inserts.

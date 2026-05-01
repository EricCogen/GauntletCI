# Case Study: GCI0055 - Breaking Method Signature Changes

## The Problem

Public method signatures are contracts. When you change the signature of a public method, consuming code breaks. Breaking changes include: removing parameters, adding required parameters without defaults, changing return types, or changing parameter types. These changes violate the Liskov Substitution Principle and break API compatibility.

In libraries and frameworks, breaking changes force all consumers to update their code. In internal APIs, they cascade through teams. In open-source projects, they cause maintenance nightmares. Version numbers change (MAJOR bump in semantic versioning), but often consumers don't update until forced.

## Real-World Failure

**.NET 10.0.7 regression (April 2026)**: Microsoft released a DataProtection API update that changed a method signature. Specifically, the `Decrypt` method return type changed and a parameter was removed. This broke ASP.NET Core applications that relied on the old signature. The regression was security-critical, forcing a patch release in 10.0.8. Applications that had compiled against 10.0.7 continued to fail until they updated to 10.0.8 AND recompiled.

**EF Core migration helper (2024)**: A helper method signature in Entity Framework Core changed from `void ApplyMigration(string)` to `Task ApplyMigrationAsync(string)`. Thousands of upgrade scripts broke silently. Existing code called the old method and compiled, but migrations never ran. Data corruption resulted from stale schema.

## How Failures Happen

```csharp
// v1.0 - Original API
public class PaymentProcessor
{
    public bool ProcessPayment(Order order)
    {
        // Implementation
        return true;
    }
}

// Consumer code (works fine in v1.0)
public class OrderService
{
    public void CheckoutOrder(Order order)
    {
        var processor = new PaymentProcessor();
        bool success = processor.ProcessPayment(order); // Works
        if (success) {
            CompleteOrder(order);
        }
    }
}

// v2.0 - Breaking change introduced
public class PaymentProcessor
{
    // BREAKING CHANGE 1: Return type changed bool -> Task<bool>
    // BREAKING CHANGE 2: Added required parameter without default
    public async Task<bool> ProcessPayment(Order order, IPaymentValidator validator)
    {
        if (!validator.IsValid(order))
            return false;
        // Implementation
        return true;
    }
}

// Consumer code (now BROKEN in v2.0)
public class OrderService
{
    public void CheckoutOrder(Order order)
    {
        var processor = new PaymentProcessor();
        bool success = processor.ProcessPayment(order); // COMPILATION ERROR!
        // Missing second parameter validator
        // Wrong return type (not Task<bool>)
        // Can't assign Task<bool> to bool
    }
}
```

## GauntletCI Detection

GCI0055 detects signature changes in public methods and flags breaking changes:
1. Return type changes (e.g., `void` -> `Task`, `bool` -> `Task<bool>`)
2. Removed parameters
3. Added required parameters (without default values)
4. Parameter type changes

The rule has low tolerance for change: a single removed parameter or return type change triggers a finding.

## False Positives

**Not flagged** (safe changes):
```csharp
// Adding optional parameter with default - NOT breaking
public void ProcessOrder(Order order, string notes = null) // OK - default provided

// Narrowing return type (covariance) - safe in some contexts
public Task ProcessOrderAsync(Order order) // Allowed (async wrapper)

// Overload (different signature) - NOT breaking
public void ProcessOrder(Order order) // Original
public void ProcessOrder(Order order, bool validateOnly) // New overload - OK
```

**Flagged** (breaking):
```csharp
// Removing parameter - BREAKING
public void ProcessOrder(Order order, IValidator validator) // Was required, now gone

// Adding required parameter without default - BREAKING
public void ProcessOrder(Order order, string correlationId) // No default, breaks callers

// Changing return type - BREAKING
public async Task<bool> ProcessOrder(Order order) // Was void, now Task<bool>
```

## When It Fires

GCI0055 fires when:
1. A public method signature is modified (not private)
2. The change includes:
   - Return type modification
   - Parameter removal
   - Required parameter addition (without default)
   - Parameter type change

It does NOT fire on:
- Private methods
- New method additions (not breaking)
- Adding optional parameters with defaults
- Overloading (new signature, old signature intact)

## Remediation

**Before** (Breaking):
```csharp
public class OrderService
{
    // Original signature
    public bool CancelOrder(int orderId)
    {
        var order = _repository.GetOrder(orderId);
        _repository.Delete(order);
        return true;
    }

    // BREAKING CHANGE: Added required parameter without default
    public bool CancelOrder(int orderId, string reason)
    {
        var order = _repository.GetOrder(orderId);
        order.CancelledReason = reason;
        _repository.Update(order);
        return true;
    }
}
```

**After** (Safe):
```csharp
public class OrderService
{
    // Original signature unchanged (callers not broken)
    public bool CancelOrder(int orderId)
    {
        return CancelOrder(orderId, "No reason provided");
    }

    // New overload with extended functionality
    public bool CancelOrder(int orderId, string reason)
    {
        var order = _repository.GetOrder(orderId);
        order.CancelledReason = reason;
        _repository.Update(order);
        return true;
    }
}

// Or: Add optional parameter
public bool CancelOrder(int orderId, string reason = "No reason provided")
{
    var order = _repository.GetOrder(orderId);
    order.CancelledReason = reason;
    _repository.Update(order);
    return true;
}
```

## Async Migrations

A common breaking pattern is converting sync to async:

**Before** (Breaking):
```csharp
public class DataService
{
    public List<Customer> GetCustomers() // Sync, returns List
    {
        return _db.Customers.ToList();
    }

    // BREAKING CHANGE: async with Task<>
    public async Task<List<Customer>> GetCustomersAsync()
    {
        return await _db.Customers.ToListAsync();
    }
}
```

**After** (Safe):
```csharp
public class DataService
{
    // Keep the sync version for backward compatibility
    public List<Customer> GetCustomers()
    {
        return _db.Customers.ToList();
    }

    // Add async variant alongside
    public async Task<List<Customer>> GetCustomersAsync()
    {
        return await _db.Customers.ToListAsync();
    }
}

// OR: Deprecate the sync version gradually
[Obsolete("Use GetCustomersAsync instead", false)]
public List<Customer> GetCustomers()
{
    return _db.Customers.ToList();
}

public async Task<List<Customer>> GetCustomersAsync()
{
    return await _db.Customers.ToListAsync();
}
```

## Semantic Versioning

- **PATCH (1.0.0 -> 1.0.1)**: Bug fixes only, no signature changes
- **MINOR (1.0 -> 1.1)**: New features (new methods, optional parameters), backward compatible
- **MAJOR (1 -> 2)**: Breaking changes allowed, must increment because consumers will fail

Breaking signature changes require MAJOR version bumps.

## Key Takeaway

**Preserve backward compatibility in public APIs.** Use overloads, optional parameters, and new method names to extend functionality without breaking existing code. If you must break signatures, increment the MAJOR version and provide clear migration guides.

## References

- Semantic Versioning: https://semver.org/
- .NET Breaking Changes: Documenting decisions
- Liskov Substitution Principle (LSP) violations
- Microsoft's API design guidelines
